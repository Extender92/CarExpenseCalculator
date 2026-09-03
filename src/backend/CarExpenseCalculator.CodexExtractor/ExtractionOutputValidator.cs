using System.Text.Json;
using Json.Schema;

namespace CarExpenseCalculator.CodexExtractor;

internal interface IExtractionOutputValidator
{
    bool IsValid(string json);
}

internal sealed class ExtractionOutputValidator : IExtractionOutputValidator
{
    private readonly JsonSchema schema;

    public ExtractionOutputValidator(CodexExtractorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        schema = JsonSchema.FromText(File.ReadAllText(options.SchemaPath));
    }

    public bool IsValid(string json)
    {
        try
        {
            using var instance = JsonDocument.Parse(json);
            var result = schema.Evaluate(
                instance.RootElement,
                new EvaluationOptions { OutputFormat = OutputFormat.List });
            return result.IsValid;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
