// REQ-OPS-001: Infrastructure helper — converts System.Text.Json types to Firestore-compatible maps.

using System.Text.Json;

namespace ZenoHR.Infrastructure.Seeding;

/// <summary>
/// Converts a <see cref="JsonElement"/> tree into a <see cref="Dictionary{TKey,TValue}"/>
/// that Google.Cloud.Firestore can accept in SetAsync / CreateAsync.
/// Firestore natively stores: string, long, double, bool, null, List, Dictionary.
/// REQ-OPS-001
/// </summary>
internal static class FirestoreJsonConverter
{
    /// <summary>Converts a JSON object element to a Firestore-compatible dictionary.</summary>
    public static Dictionary<string, object?> ToFirestoreMap(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new ArgumentException($"Expected JSON Object but got {element.ValueKind}.");

        var map = new Dictionary<string, object?>();
        foreach (var property in element.EnumerateObject())
            map[property.Name] = ToFirestoreValue(property.Value);

        return map;
    }

    /// <summary>Recursively converts any JsonElement to its Firestore-native equivalent.</summary>
    public static object? ToFirestoreValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => ToFirestoreMap(element),
        JsonValueKind.Array => element.EnumerateArray()
            .Select(ToFirestoreValue)
            .ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var l) => l,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element.ToString()
    };
}
