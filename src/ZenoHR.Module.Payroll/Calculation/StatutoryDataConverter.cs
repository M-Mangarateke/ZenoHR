// REQ-HR-003: Helper that safely converts Firestore-stored numeric values to decimal.
// CTL-SARS-001: Firestore stores JSON numbers as long (integer) or double (float).
// All statutory rule set adaptors use this converter — never cast directly.

namespace ZenoHR.Module.Payroll.Calculation;

/// <summary>
/// Converts Firestore-stored values (read from <c>StatutoryRuleSet.RuleData</c>) to
/// <see cref="decimal"/>. Handles the three numeric types that the Firestore SDK
/// and <c>FirestoreJsonConverter</c> produce:
/// <list type="bullet">
///   <item><c>long</c> — JSON integers (e.g., min/max bracket amounts, base_tax integers)</item>
///   <item><c>double</c> — JSON floats (e.g., rates: 0.18, 0.01)</item>
///   <item><c>string</c> — Unlikely but safe fallback for pre-processed values</item>
/// </list>
/// CTL-SARS-001: Every statutory calculation must go through this converter.
/// Using direct casting (e.g., <c>(decimal)(double)value</c>) is forbidden outside this class.
/// </summary>
internal static class StatutoryDataConverter
{
    /// <summary>
    /// Converts an <see cref="object"/> read from <c>StatutoryRuleSet.RuleData</c> to
    /// <see cref="decimal"/>. Throws <see cref="InvalidOperationException"/> on null or
    /// unrecognised type.
    /// </summary>
    public static decimal ToDecimal(object? value) => value switch
    {
        long l => (decimal)l,
        double d => (decimal)d,
        int i => (decimal)i,
        decimal dec => dec,
        string s => decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture),
        null => throw new InvalidOperationException(
            "Cannot convert null to decimal in statutory rule data."),
        _ => throw new InvalidOperationException(
            $"Cannot convert {value.GetType().Name} '{value}' to decimal in statutory rule data."),
    };

    /// <summary>Extracts a nested <c>List&lt;object?&gt;</c> by key.</summary>
    public static List<object?> GetList(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is not List<object?> list)
            throw new InvalidOperationException(
                $"Missing or invalid '{key}' in statutory rule data. Expected a JSON array.");
        return list;
    }

    /// <summary>Extracts a nested dictionary by key.</summary>
    public static IDictionary<string, object?> GetDict(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value is not IDictionary<string, object?> dict)
            throw new InvalidOperationException(
                $"Missing or invalid '{key}' in statutory rule data. Expected a JSON object.");
        return dict;
    }
}
