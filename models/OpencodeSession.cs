sealed class OpencodeSession
{
    public required string Id { get; init; }
    public required string SourcePath { get; init; }
    public string? Slug { get; init; }
    public string? Version { get; init; }
    public string? ProjectId { get; init; }
    public string? Directory { get; init; }
    public string? Title { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public SessionSummary? Summary { get; init; }
    public List<OpencodeMessage> Messages { get; } = [];
}
