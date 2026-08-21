using System.Text.Json;
using System.Text.Json.Serialization;

namespace CashFlowPlanner.Storage.Json;

public static class CashFlowPlanJsonOptions
{
    public static JsonSerializerOptions Create()
    {
        return Create(writeIndented: true);
    }

    /// <summary>
    /// The same format either way; only the whitespace differs. Indented output is for the file
    /// the user owns and diffs, compact output is for the browser working copy, where the
    /// indentation is pure quota cost (about 25-30% of the payload).
    /// </summary>
    public static JsonSerializerOptions Create(bool writeIndented)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = writeIndented,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        options.Converters.Add(new JsonStringEnumConverter());

        return options;
    }
}