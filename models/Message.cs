namespace OpenAnalytics.models;

internal sealed class Message
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
    /// records interruptions as an error with name "MessageAbortedError"; the
    /// other readers normalize their own interruption signal to the same name so
    /// the metrics engine has a single notion of "aborted".
    /// </summary>
    public bool WasAborted => ErrorName == MessageAbortedErrorName;

    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public TokenUsage? Tokens { get; init; }
    public List<Part> Parts { get; } = [];
}
