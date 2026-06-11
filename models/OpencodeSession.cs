namespace OpenAnalytics.models;

internal sealed class OpencodeSession
{
    public required string Id { get; init; }
    public string? ParentId { get; init; }
    public string? Agent { get; init; }
    public bool Reverted { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public SessionSummary? Summary { get; init; }
    public List<OpencodeMessage> Messages { get; } = [];
}