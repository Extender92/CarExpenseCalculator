using System.Text.Json;
using System.Text.Json.Nodes;
using CarExpenseCalculator.Extraction.Contracts;

namespace CarExpenseCalculator.CodexExtractor.UnitTests;

public sealed class CodexJsonlParserTests
{
    private readonly CodexJsonlParser parser;

    public CodexJsonlParserTests()
    {
        parser = new CodexJsonlParser(new ExtractionOutputValidator(TestData.CreateOptions()));
    }

    [Fact]
    public void Parse_keeps_only_concrete_opened_sources_in_first_seen_order()
    {
        var lines = TestData.SuccessfulJsonl(
            TestData.EmptyDraftJson(),
            TestData.WebEvent("search"),
            TestData.WebEvent("open_page", "HTTPS://EXAMPLE.COM/item/1#details"),
            TestData.WebEvent("find_in_page", "https://example.com/item/1"),
            TestData.WebEvent("open_page", "https://example.com/item/2?ci=2"),
            TestData.WebEvent("open_page", "http://127.0.0.1/private"));

        var parsed = parser.TryParse(lines, out var output, out var runtimeFailure);

        Assert.True(parsed);
        Assert.Null(runtimeFailure);
        Assert.Equal(
            ["https://example.com/item/1", "https://example.com/item/2?ci=2"],
            output!.Sources);
    }

    [Fact]
    public void Parse_rejects_model_authored_sources_as_additional_output()
    {
        using var document = JsonDocument.Parse(TestData.EmptyDraftJson());
        var values = document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone());
        var serialized = JsonSerializer.Serialize(values);
        var json = serialized[..^1]
            + ",\"sources\":[\"https://example.com/item/1\"]}";

        var parsed = parser.TryParse(
            TestData.SuccessfulJsonl(json),
            out _,
            out _);

        Assert.False(parsed);
    }

    [Theory]
    [InlineData("{not-json")]
    [InlineData("{\"type\":\"thread.started\"}")]
    [InlineData("{\"type\":\"turn.failed\",\"error\":{\"message\":\"provider failed\"}}")]
    public void Parse_rejects_malformed_or_incomplete_event_streams(string line)
    {
        Assert.False(parser.TryParse([line], out _, out _));
    }

    [Fact]
    public void Parse_preserves_known_empty_collections_and_nulls()
    {
        var json = JsonSerializer.Serialize(
            new ExtractedListingDraft
            {
                Equipment = [],
                FuelTypes = null,
                SellerClaims = [],
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.True(parser.TryParse(TestData.SuccessfulJsonl(json), out var output, out _));
        Assert.Empty(output!.Draft.Equipment!);
        Assert.Null(output.Draft.FuelTypes);
        Assert.Empty(output.Draft.SellerClaims!);
    }

    [Fact]
    public void Parse_rejects_missing_additional_and_numeric_enum_properties()
    {
        var missing = JsonNode.Parse(TestData.EmptyDraftJson())!.AsObject();
        missing.Remove("make");
        var additional = JsonNode.Parse(TestData.EmptyDraftJson())!.AsObject();
        additional["sourceUrl"] = "https://model-authored.example/item/1";
        var numericEnum = JsonNode.Parse(TestData.EmptyDraftJson())!.AsObject();
        numericEnum["sellerType"] = 1;

        Assert.False(parser.TryParse(TestData.SuccessfulJsonl(missing.ToJsonString()), out _, out _));
        Assert.False(parser.TryParse(TestData.SuccessfulJsonl(additional.ToJsonString()), out _, out _));
        Assert.False(parser.TryParse(TestData.SuccessfulJsonl(numericEnum.ToJsonString()), out _, out _));
    }

    [Fact]
    public void Parse_requires_v2_locality_and_county_and_rejects_legacy_location()
    {
        var legacy = JsonNode.Parse(TestData.EmptyDraftJson())!.AsObject();
        legacy.Remove("locality");
        legacy.Remove("county");
        legacy["location"] = "Tenhult";

        Assert.False(parser.TryParse(TestData.SuccessfulJsonl(legacy.ToJsonString()), out _, out _));

        var current = JsonNode.Parse(TestData.EmptyDraftJson())!.AsObject();
        current["locality"] = "Tenhult";
        current["county"] = "Jönköpings län";
        Assert.True(parser.TryParse(TestData.SuccessfulJsonl(current.ToJsonString()), out var output, out _));
        Assert.Equal("Tenhult", output!.Draft.Locality);
        Assert.Equal("Jönköpings län", output.Draft.County);
    }

    [Fact]
    public void Parse_requires_a_completed_turn_after_the_final_message()
    {
        var lines = TestData.SuccessfulJsonl(TestData.EmptyDraftJson()).ToList();
        lines.RemoveAt(lines.Count - 1);

        Assert.False(parser.TryParse(lines, out _, out _));
    }
}
