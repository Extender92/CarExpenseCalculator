using System.Text.Json;
using System.Text.Json.Serialization;
using CarExpenseCalculator.Core.Listings;
using CarExpenseCalculator.Extraction.Contracts;

namespace CarExpenseCalculator.CodexExtractor;

internal sealed class CodexJsonlParser(IExtractionOutputValidator outputValidator)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public bool TryParse(
        IReadOnlyList<string> lines,
        out ParsedCodexOutput? output,
        out string? runtimeFailure)
    {
        ArgumentNullException.ThrowIfNull(lines);

        output = null;
        runtimeFailure = null;
        var sources = new List<string>();
        var seenSources = new HashSet<string>(StringComparer.Ordinal);
        string? finalMessage = null;
        var sawThread = false;
        var sawTurn = false;
        var sawCompletedTurn = false;
        var eventCount = 0;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            eventCount++;
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(line);
            }
            catch (JsonException)
            {
                return false;
            }

            using (document)
            {
                var root = document.RootElement;
                if (!TryGetString(root, "type", out var eventType))
                {
                    return false;
                }

                switch (eventType)
                {
                    case "thread.started":
                        sawThread = true;
                        break;
                    case "turn.started":
                        sawTurn = true;
                        break;
                    case "turn.completed":
                        sawCompletedTurn = true;
                        break;
                    case "turn.failed":
                    case "error":
                        runtimeFailure ??= ReadFailureMessage(root);
                        break;
                    case "item.completed":
                        ReadCompletedItem(root, sources, seenSources, ref finalMessage, ref runtimeFailure);
                        break;
                }
            }
        }

        if (runtimeFailure is not null
            || !sawThread
            || !sawTurn
            || !sawCompletedTurn
            || string.IsNullOrWhiteSpace(finalMessage)
            || !outputValidator.IsValid(finalMessage))
        {
            return false;
        }

        try
        {
            var draft = JsonSerializer.Deserialize<ExtractedListingDraft>(finalMessage, SerializerOptions);
            if (draft is null)
            {
                return false;
            }

            output = new ParsedCodexOutput(Array.AsReadOnly(sources.ToArray()), draft, eventCount);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void ReadCompletedItem(
        JsonElement root,
        ICollection<string> sources,
        ISet<string> seenSources,
        ref string? finalMessage,
        ref string? runtimeFailure)
    {
        if (!root.TryGetProperty("item", out var item)
            || item.ValueKind != JsonValueKind.Object
            || !TryGetString(item, "type", out var itemType))
        {
            return;
        }

        if (itemType == "agent_message" && TryGetString(item, "text", out var text))
        {
            finalMessage = text;
            return;
        }

        if (itemType == "error")
        {
            runtimeFailure ??= ReadFailureMessage(item);
            return;
        }

        if (itemType != "web_search"
            || !item.TryGetProperty("action", out var action)
            || action.ValueKind != JsonValueKind.Object
            || !TryGetString(action, "type", out var actionType)
            || (actionType != "open_page" && actionType != "find_in_page")
            || !TryGetString(action, "url", out var sourceValue)
            || !ListingUrl.TryParse(sourceValue, out var source))
        {
            return;
        }

        if (seenSources.Add(source!.Value))
        {
            sources.Add(source.Value);
        }
    }

    private static string ReadFailureMessage(JsonElement element)
    {
        if (TryGetString(element, "message", out var message))
        {
            return message;
        }

        if (element.TryGetProperty("error", out var error)
            && error.ValueKind == JsonValueKind.Object
            && TryGetString(error, "message", out message))
        {
            return message;
        }

        return "Codex runtime failure";
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        if (element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString()!;
            return true;
        }

        value = string.Empty;
        return false;
    }
}
