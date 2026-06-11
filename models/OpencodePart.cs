using System.Text.Json;

sealed class OpencodePart
{
    public required string Id { get; init; }
    public required string SessionId { get; init; }
    public required string MessageId { get; init; }
    public required string SourcePath { get; init; }
    public string? Type { get; init; }
    public string? Text { get; init; }
    public string? Tool { get; init; }
    public string? CallId { get; init; }
    public string? ToolStatus { get; init; }
    public string? Reason { get; init; }
    public string? Snapshot { get; init; }
    public decimal? Cost { get; init; }
    public TokenUsage? Tokens { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? EndedAt { get; init; }
    public JsonElement? State { get; init; }
    public JsonElement? Input { get; init; }
    public JsonElement? Output { get; init; }
}
