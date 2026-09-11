using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Extraction.Contracts;

namespace CarExpenseCalculator.CodexExtractor;

internal interface IListingContentParser
{
    Task<RetrievedListingContent> ParseAsync(RetrievedPage page, CancellationToken cancellationToken);
}

internal sealed partial class BlocketContentParser : IListingContentParser
{
    public async Task<RetrievedListingContent> ParseAsync(RetrievedPage page, CancellationToken cancellationToken)
    {
        using var document = await new HtmlParser().ParseDocumentAsync(page.Html, cancellationToken);
        var main = document.QuerySelector("main");
        if (document.QuerySelector("[id*=captcha],iframe[src*=captcha],form[action*=challenge]") is not null)
            throw new ListingSourceException(CodexExecutionFailure.SourceBlocked);
        if (main is null) throw Invalid();
        var title = Plain(Unique(main.QuerySelectorAll("h1")));
        if (title is null) throw Invalid();
        var headings = main.QuerySelectorAll("h2");
        IElement? Heading(string name) => Unique(headings.Where(x => Plain(x) == name));
        IElement? Section(string name) => Heading(name)?.Closest("section");
        var info = Heading("Annonsinformation")?.ParentElement ?? throw Invalid();
        string? Info(string label) => Plain(Unique(info.QuerySelectorAll("p").Where(x => Plain(x) == label))?.NextElementSibling);
        var id = Info("Annons-ID");
        if (id is null || id != page.Url.EscapedPath.Split('/')[^1]) throw Invalid();
        var descriptionSection = Section("Beskrivning");
        var descriptionNode = descriptionSection?.QuerySelector("[data-testid=expandable-section] > div");
        if (descriptionSection is not null && descriptionNode is null) throw Invalid();
        var description = MaskContacts(Plain(descriptionNode));
        var specsSection = Section("Specifikationer");
        ExtractedListingSpecification[]? specs = null;
        if (specsSection is not null)
        {
            var terms = specsSection.QuerySelectorAll("dt");
            if (terms.Length == 0 || terms.Length != specsSection.QuerySelectorAll("dd").Length) throw Invalid();
            specs = terms.Select(term => new ExtractedListingSpecification(
                Plain(term) ?? throw Invalid(),
                Plain(Unique(term.ParentElement!.QuerySelectorAll("dd"))) ?? throw Invalid())).ToArray();
        }
        var equipmentSection = Section("Utrustning");
        var equipment = equipmentSection?.QuerySelectorAll("li").Select(x => Plain(x) ?? throw Invalid()).ToArray();
        if (equipment?.Length == 0) equipment = null;
        var questionsSection = Section("Säljarens kännedom om bilen");
        var answers = questionsSection?.Children.Where(x => x.LocalName == "p").ToArray();
        if (answers?.Length % 2 == 1) throw Invalid();
        var pairs = answers is null || answers.Length == 0 ? null : Enumerable.Range(0, answers.Length / 2)
            .Select(i => new ExtractedSellerAnswer(Plain(answers[i * 2]) ?? throw Invalid(), Plain(answers[i * 2 + 1]) ?? throw Invalid())).ToArray();
        var price = Unique(main.QuerySelectorAll("p").Where(x => Plain(x) == "Totalt pris"));
        var subtitle = main.QuerySelector("h1")?.NextElementSibling;
        // Only the actual profile-panel marker establishes type; never infer private from
        // a missing dealer panel, a business name, or text inside the description.
        bool ProfileContainer(IElement element) =>
            element.Closest("[data-testid=expandable-section], [hidden], [aria-hidden=true]") is null;
        bool ProfileHeading(IElement heading, string label) => Plain(heading) == label && ProfileContainer(heading);
        var dealer = main.QuerySelectorAll("w-box > div > h2")
            .Any(x => ProfileHeading(x, "Återförsäljarens uppgifter"));
        var privateSeller = main.QuerySelectorAll("#trust-ad-profile-card-podlet-isolated > h2")
            .Any(x => ProfileHeading(x, "Användarprofil"));
        // Blocket also renders the private profile in a declarative shadow root.
        // Inspect only that known template's inert DOM; no scripts/resources run.
        privateSeller |= main.QuerySelectorAll("trust-ad-profile-card-podlet-isolated > template[shadowrootmode=open]")
            .OfType<IHtmlTemplateElement>().Where(ProfileContainer)
            .SelectMany(x => x.Content.QuerySelectorAll("#trust-ad-profile-card-podlet-isolated > h2"))
            .Any(x => ProfileHeading(x, "Användarprofil"));
        if (dealer && privateSeller) throw Invalid();
        var content = new RetrievedListingContent(page.Url.Value, title,
            subtitle?.LocalName == "p" ? Plain(subtitle) : null, id, Plain(price?.NextElementSibling), description,
            specs, equipment, pairs, Plain(Heading("Plats")?.ParentElement?.QuerySelector("w-button[href]")), Info("Uppdaterad"),
            dealer ? "dealer" : privateSeller ? "private" : null);
        Validate(content);
        cancellationToken.ThrowIfCancellationRequested();
        return content;
    }

    internal static void Validate(RetrievedListingContent content)
    {
        if (content.SellerType is not (null or "dealer" or "private")) throw Invalid();
        Check(content.Title, ListingDetailLimits.TitleLength);
        Check(content.Subtitle, ListingDetailLimits.TitleLength);
        Check(content.Description, ListingDetailLimits.DescriptionLength);
        Check(content.ListingId, ListingDetailLimits.LabelLength);
        Check(content.Price, ListingDetailLimits.EntryTextLength);
        Check(content.Location, ListingDetailLimits.EntryTextLength);
        Check(content.Updated, ListingDetailLimits.EntryTextLength);
        if (content.Specifications?.Count > ListingDetailLimits.Entries || content.SellerAnswers?.Count > ListingDetailLimits.Entries || content.Equipment?.Count > ListingDetailLimits.Entries)
            throw Invalid();
        foreach (var entry in content.Specifications ?? []) { Check(entry.Name, ListingDetailLimits.LabelLength); Check(entry.Value, ListingDetailLimits.EntryTextLength); }
        foreach (var entry in content.SellerAnswers ?? []) { Check(entry.Question, ListingDetailLimits.EntryTextLength); Check(entry.Answer, ListingDetailLimits.EntryTextLength); }
        foreach (var entry in content.Equipment ?? []) Check(entry, ListingDetailLimits.LabelLength);
    }

    private static void Check(string? value, int maximum) { if (value?.Length > maximum) throw Invalid(); }
    private static ListingSourceException Invalid() => new(CodexExecutionFailure.SourceInvalidContent);
    private static IElement? Unique(IEnumerable<IElement> elements)
    {
        var values = elements.Take(2).ToArray();
        return values.Length > 1 ? throw Invalid() : values.FirstOrDefault();
    }

    private static string? Plain(INode? node)
    {
        if (node is null) return null;
        var value = new StringBuilder();
        Append(node, value);
        var text = BlankLines().Replace(value.ToString().Replace('\u00a0', ' ').Replace("\r\n", "\n").Normalize(NormalizationForm.FormC).Trim(), "\n\n");
        return text.Length == 0 ? null : text;
    }

    private static void Append(INode node, StringBuilder target)
    {
        if (node is IText text) { target.Append(text.Data); return; }
        if (node is not IElement element) return;
        if (element.LocalName is "script" or "style" or "noscript" or "svg" or "w-icon" or "w-attention" or "button" ||
            element.HasAttribute("hidden") || element.GetAttribute("aria-hidden") == "true") return;
        if (element.LocalName == "br") { target.Append('\n'); return; }
        if (element.LocalName is "p" or "li") target.Append("\n\n");
        foreach (var child in element.ChildNodes) Append(child, target);
        if (element.LocalName is "p" or "li") target.Append("\n\n");
    }

    internal static string? MaskContacts(string? value) => value is null ? null :
        ContactPhone().Replace(Email().Replace(value, "[kontaktuppgift borttagen]"), "[kontaktuppgift borttagen]");

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex BlankLines();
    [GeneratedRegex(@"(?i)(?:mailto:)?[\p{L}\d._%+\-]+@[\p{L}\d.\-]+\.[\p{L}]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex Email();
    // Swedish telephone prefixes plus explicit telephone links. Boundaries prevent matches inside VINs/dates.
    [GeneratedRegex(@"(?ix)(?<![\p{L}\d])(?:tel:\+?\d[\d\s()\-]{6,}\d|(?:\+46[\s()\-]*[1-9]|0(?:7[02369]|8|[1-6]\d))[\s()\-]*(?:\d[\s()\-]*){5,7}\d)(?![\p{L}\d])", RegexOptions.CultureInvariant)]
    private static partial Regex ContactPhone();
}
