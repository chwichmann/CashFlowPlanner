using System.Text.Json.Nodes;

namespace CashFlowPlanner.Storage.Json.Tests;

/// <summary>
/// Structural JSON helpers. These compare parsed trees rather than text so that formatting,
/// property order and numeric representation never make a test pass or fail for the wrong reason.
/// </summary>
internal static class JsonAssert
{
    /// <summary>
    /// Asserts that every property present in <paramref name="expectedSubset"/> is also present in
    /// <paramref name="actual"/> with an equal value, at every nesting level. Extra properties in
    /// <paramref name="actual"/> are allowed (a newer build may add defaults); missing or changed
    /// ones are reported with their full JSON path.
    /// </summary>
    public static void ContainsAllOf(JsonNode? expectedSubset, JsonNode? actual)
    {
        var losses = new List<string>();

        Compare(expectedSubset, actual, "$", losses);

        if (losses.Count > 0)
        {
            Assert.Fail(
                "Round trip lost or changed data:" +
                Environment.NewLine +
                string.Join(Environment.NewLine, losses));
        }
    }

    private static void Compare(
        JsonNode? expected,
        JsonNode? actual,
        string path,
        List<string> losses)
    {
        if (expected is JsonObject expectedObject)
        {
            if (actual is not JsonObject actualObject)
            {
                losses.Add($"{path}: expected an object, found {Describe(actual)}.");
                return;
            }

            foreach (var (key, expectedValue) in expectedObject)
            {
                if (!actualObject.TryGetPropertyValue(key, out var actualValue))
                {
                    losses.Add($"{path}.{key}: dropped (expected {Describe(expectedValue)}).");
                    continue;
                }

                Compare(expectedValue, actualValue, $"{path}.{key}", losses);
            }

            return;
        }

        if (expected is JsonArray expectedArray)
        {
            if (actual is not JsonArray actualArray)
            {
                losses.Add($"{path}: expected an array, found {Describe(actual)}.");
                return;
            }

            if (expectedArray.Count != actualArray.Count)
            {
                losses.Add(
                    $"{path}: expected {expectedArray.Count} items, found {actualArray.Count}.");
                return;
            }

            for (var i = 0; i < expectedArray.Count; i++)
            {
                Compare(expectedArray[i], actualArray[i], $"{path}[{i}]", losses);
            }

            return;
        }

        if (!JsonNode.DeepEquals(expected, actual))
        {
            losses.Add($"{path}: expected {Describe(expected)}, found {Describe(actual)}.");
        }
    }

    private static string Describe(JsonNode? node)
    {
        return node?.ToJsonString() ?? "null";
    }
}
