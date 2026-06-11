using System.Text.Json;

static class JsonHelpers
{
    public static DateTimeOffset? FromUnixMilliseconds(long? value)
    {
        if (value is null)
        {
            return null;
        }

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
        {
            return null;
        }

        return value.TryGetProperty(propertyName, out var property)
               && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    public static MessageSummary? ParseMessageSummary(JsonElement? element)
    {
        if (element is not { ValueKind: JsonValueKind.Object } value)
        {
            return null;
        }

        var title = value.TryGetProperty("title", out var titleProperty) &&
                    titleProperty.ValueKind == JsonValueKind.String
            ? titleProperty.GetString()
            : null;

        List<DiffSummary>? diffs = null;
        if (value.TryGetProperty("diffs", out var diffsProperty) && diffsProperty.ValueKind == JsonValueKind.Array)
        {
            diffs = [];
            foreach (var diff in diffsProperty.EnumerateArray())
            {
                if (diff.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                diffs.Add(new DiffSummary(
                    TryGetString(diff, "file"),
                    TryGetInt(diff, "additions"),
                    TryGetInt(diff, "deletions"),
                    TryGetString(diff, "status")));
            }
        }

        return new MessageSummary(title, diffs);
    }

    private static int? TryGetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value) ? value : null;
    }
}