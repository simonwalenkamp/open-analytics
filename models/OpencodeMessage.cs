sealed class OpencodeMessage
{
    public required string Id { get; init; }
    public required string SessionId { get; init; }
    public required string SourcePath { get; init; }
    public string? Role { get; init; }
    public string? ParentId { get; init; }
    public string? Agent { get; init; }
    public string? Mode { get; init; }
    public string? ProviderId { get; init; }
    public string? ModelId { get; init; }
    public string? Finish { get; init; }
    public decimal? Cost { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public MessageSummary? Summary { get; init; }
    public TokenUsage? Tokens { get; init; }
    public List<OpencodePart> Parts { get; } = [];
}
