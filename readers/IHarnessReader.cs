using OpenAnalytics.models;

namespace OpenAnalytics.Readers;

/// <summary>
/// Reads one coding agent's local usage data and normalizes it into the
/// harness-agnostic <see cref="Session"/> model the metrics engine consumes.
/// Each reader owns its own discovery (where the data lives), its own native
/// format, and its own error handling — a single unreadable file or missing
/// harness must never abort the whole report.
/// </summary>
internal interface IHarnessReader
{
    /// <summary>Stable harness identifier used as the first key segment, e.g. "opencode".</summary>
    string Harness { get; }

    /// <summary>True when this harness's data exists on the current machine.</summary>
    bool IsAvailable();

    /// <summary>
    /// Reads and normalizes all sessions. Parse failures are appended to
    /// <paramref name="errors"/> rather than thrown.
    /// </summary>
    IReadOnlyList<Session> Read(List<ReadError> errors);
}
