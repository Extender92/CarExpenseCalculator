using System.Text.Json;
using CarExpenseCalculator.Extraction.Contracts;

namespace CarExpenseCalculator.CodexExtractor.UnitTests;

internal static class TestData
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string EmptyDraftJson() => JsonSerializer.Serialize(new ExtractedListingDraft(), JsonOptions);

    public static CodexExtractorOptions CreateOptions(string? codexHome = "C:\\safe-codex-home") =>
        new()
        {
            CodexHome = codexHome,
            CodexExecutable = "codex",
            WorkRoot = Path.Combine(Path.GetTempPath(), "car-expense-codex-tests"),
            SchemaPath = Path.Combine(AppContext.BaseDirectory, "Schemas", "listing-extraction-v2.schema.json"),
        };

    public static IReadOnlyList<string> SuccessfulJsonl(
        string draftJson,
        params string[] itemEvents)
    {
        return
        [
            "{\"type\":\"thread.started\",\"thread_id\":\"thread-1\"}",
            "{\"type\":\"turn.started\"}",
            .. itemEvents,
            JsonSerializer.Serialize(
                new
                {
                    type = "item.completed",
                    item = new { id = "message-1", type = "agent_message", text = draftJson },
                },
                JsonOptions),
            "{\"type\":\"turn.completed\",\"usage\":{\"input_tokens\":1,\"cached_input_tokens\":0,\"output_tokens\":1}}",
        ];
    }

    public static string WebEvent(string action, string? url = null) =>
        JsonSerializer.Serialize(
            new
            {
                type = "item.completed",
                item = new
                {
                    id = Guid.NewGuid().ToString("N"),
                    type = "web_search",
                    query = "ignored",
                    action = new { type = action, url },
                },
            },
            JsonOptions);
}
