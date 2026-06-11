namespace OpenAnalytics.models;

internal sealed class ModelScore
{
    public int Messages { get; set; }
    public HashSet<string> Sessions { get; } = [];

    public double TotalLatencySeconds { get; set; }
    public int LatencySamples { get; set; }

    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public EditQuality Quality { get; } = new();
    public int AssistantMessages { get; set; }
    public int Interrupts { get; set; }
    public int Reverts { get; set; }
    public int ZeroLanded { get; set; }
    public int TodoSessions { get; set; }
    public int NoTodoSessions { get; set; }
    public int TodoNotDone { get; set; }
    public int TodoRevised { get; set; }
    public int PlanModeSessions { get; set; }
    public int NoPlanModeSessions { get; set; }
    public int PlanModeNotDone { get; set; }
    public int PlanModeRevised { get; set; }
    public double TotalSessionDurationSeconds { get; set; }
    public int SessionDurationSamples { get; set; }

    public double? AvgLatencySeconds => LatencySamples == 0 ? null : TotalLatencySeconds / LatencySamples;
    public double? AvgSessionDurationSeconds => SessionDurationSamples == 0 ? null : TotalSessionDurationSeconds / SessionDurationSamples;
    public double MessagesPerSession => Sessions.Count == 0 ? 0 : (double)Messages / Sessions.Count;
    public double InputTokensPerSession => Sessions.Count == 0 ? 0 : (double)InputTokens / Sessions.Count;
    public double OutputTokensPerSession => Sessions.Count == 0 ? 0 : (double)OutputTokens / Sessions.Count;
    public double EditsPerSession => Sessions.Count == 0 ? 0 : (double)Quality.Edits / Sessions.Count;
    public double? TodoToNoTodoRatio => NoTodoSessions == 0 ? null : (double)TodoSessions / NoTodoSessions;
    public double? PlanModeSessionPercentage =>
        PlanModeSessions + NoPlanModeSessions == 0
            ? null
            : (double)PlanModeSessions / (PlanModeSessions + NoPlanModeSessions) * 100;

    public double? PlanModeNotDonePercentage =>
        PlanModeSessions == 0 ? null : (double)PlanModeNotDone / PlanModeSessions * 100;

    public double? PlanModeRevisedPercentage =>
        PlanModeSessions == 0 ? null : (double)PlanModeRevised / PlanModeSessions * 100;
}
