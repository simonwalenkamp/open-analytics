using System.Text.Json;

namespace OpenAnalytics.Helpers;

internal static class JsonHelpers
{
    public static DateTimeOffset? FromUnixMilliseconds(long? value)
    {
        if (value is null) return null;

        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(value.Value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public static string? TryGetString(JsonElement? element, string propertyName)
    {
        if (element is not { ValueKind: JsonValueKind.Object } value)
            return null;

        return value.TryGetProperty(propertyName, out var property)
               && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    public static string? ParseErrorName(JsonElement? element) =>
        TryGetString(element, "name");
}
