using System.Text.Json.Nodes;
using CarExpenseCalculator.Core.Listings;
using Xunit;

namespace CarExpenseCalculator.CodexExtractor.UnitTests;

public sealed class CompleteListingSchemaTests
{
    [Theory]
    [InlineData("audi-a4", 18, 4, "1999-11-24")]
    [InlineData("skoda-roomster", 15, 2, "2007-12-05")]
    public void Reference_contract_preserves_complete_input_without_source_events(string name, int equipment, int owners, string firstRegistration)
    {
        var reference = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"References",name+".json")))!;
        var json = reference["draft"]!.ToJsonString();
        var parser = new CodexJsonlParser(new ExtractionOutputValidator(TestData.CreateOptions()));
        Assert.True(parser.TryParse(TestData.SuccessfulJsonl(json), out var parsed, out _));
        Assert.Empty(parsed!.Sources);
        Assert.Equal(equipment, parsed.Draft.Equipment!.Count);
        Assert.Equal(owners, parsed.Draft.OwnerCount);
        Assert.Equal(firstRegistration, parsed.Draft.FirstRegistrationDate);
        Assert.Equal("Begagnad bil till salu", parsed.Draft.Details!.SaleForm);
        Assert.Null(parsed.Draft.RegistrationNumber);
        Assert.Null(parsed.Draft.Details.UpdatedTimeZone);
        Assert.Null(parsed.Draft.Details.UpdatedUtcOffsetMinutes);
    }

    [Fact]
    public void Schema_uses_the_core_content_limits_and_does_not_truncate_multibyte_text()
    {
        var node = JsonNode.Parse(TestData.EmptyDraftJson())!;
        node["details"] = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"References","audi-a4.json")))!["draft"]!["details"]!.DeepClone();
        var validator = new ExtractionOutputValidator(TestData.CreateOptions());
        node["details"]!["description"] = new string('å', ListingDetailLimits.DescriptionLength);
        node["details"]!["specifications"] = new JsonArray(Enumerable.Range(0,ListingDetailLimits.Entries).Select(i => (JsonNode)new JsonObject { ["name"]="Egenskap",["value"]=i.ToString() }).ToArray());
        Assert.True(validator.IsValid(node.ToJsonString()));
        node["details"]!["description"] = new string('å', ListingDetailLimits.DescriptionLength+1);
        Assert.False(validator.IsValid(node.ToJsonString()));
        node["details"]!["description"] = null;
        node["details"]!["specifications"]!.AsArray().Add(new JsonObject { ["name"]="För mycket",["value"]="Sista" });
        Assert.False(validator.IsValid(node.ToJsonString()));
    }
}
