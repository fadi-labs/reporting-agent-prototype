namespace reporting.agent.core.Models;

public enum Universe
{
    Stocks,
}

public static class UniverseMap
{
    public static readonly IReadOnlyDictionary<Universe, string> DisplayName =
        new Dictionary<Universe, string>
        {
            [Universe.Stocks] = "Stocks",
        };

    public static readonly IReadOnlyDictionary<Universe, string> FileStem =
        new Dictionary<Universe, string>
        {
            [Universe.Stocks] = "stocks",
        };

    public static readonly IReadOnlyDictionary<string, Universe> ByFileStem =
        FileStem.ToDictionary(kv => kv.Value, kv => kv.Key);

    public static readonly IReadOnlyDictionary<string, Universe> ByDisplayName =
        DisplayName.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    public static bool TryParse(string value, out Universe universe)
    {
        if (ByDisplayName.TryGetValue(value, out universe))
        {
            return true;
        }
        if (ByFileStem.TryGetValue(value.Trim().ToLowerInvariant(), out universe))
        {
            return true;
        }
        universe = default;
        return false;
    }
}
