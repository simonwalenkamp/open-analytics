namespace OpenAnalytics.models;

internal sealed class OpencodeMessage
{
    private const string MessageAbortedErrorName = "MessageAbortedError";

    public required string Id { get; init; }
    public required string SessionId { get; init; }
    public string? Role { get; init; }
    public string? ProviderId { get; init; }
    public string? ModelId { get; init; }
    public string? Mode { get; init; }
    public string? ErrorName { get; init; }

    /// <summary>
    /// True when the user interrupted/aborted this assistant response. opencode
    /// records interruptions as an error with name "MessageAbortedError"; this is
    /// the only reliable interruption signal in the database (the finish reason is
    /// usually empty for aborts but also empty for many non-aborted messages).
    /// </summary>
    public bool WasAborted => ErrorName == MessageAbortedErrorName;

    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public TokenUsage? Tokens { get; init; }
    public List<OpencodePart> Parts { get; } = [];
}
