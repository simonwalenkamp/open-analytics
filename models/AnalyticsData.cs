namespace OpenAnalytics.models;

internal sealed record AnalyticsData(IReadOnlyList<Session> Sessions, IReadOnlyList<ReadError> Errors)
{
    public int MessageCount => Sessions.Sum(x => x.Messages.Count);
    public int PartCount => Sessions.Sum(x => x.Messages.Sum(m => m.Parts.Count));
}
