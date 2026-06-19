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

    public static DateTimeOffset? FromIso8601(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;
    }

    public static DateTimeOffset? FromUnixSeconds(long? value)
    {
        if (value is null) return null;

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(value.Value);
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
