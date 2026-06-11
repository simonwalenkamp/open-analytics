using System.Text.Json;

namespace OpenAnalytics.models;

internal sealed class OpencodePart
{
    public required string Id { get; init; }
    public required string MessageId { get; init; }
    public string? Tool { get; init; }
    public string? ToolStatus { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public JsonElement? State { get; init; }
}