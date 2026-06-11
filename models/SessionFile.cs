using System.Text.Json.Serialization;

sealed class SessionFile
{
    public string? Id { get; set; }
    public string? Slug { get; set; }
    public string? Version { get; set; }
    [JsonPropertyName("projectID")]
    public string? ProjectId { get; set; }
    public string? Directory { get; set; }
    public string? Title { get; set; }
    public TimeFile? Time { get; set; }
    public SessionSummary? Summary { get; set; }

    public OpencodeSession? ToSession(string sourcePath)
    {
        if (Id is null)
        {
            return null;
        }

        return new OpencodeSession
        {
            Id = Id,
            SourcePath = sourcePath,
            Slug = Slug,
            Version = Version,
            ProjectId = ProjectId,
            Directory = Directory,
            Title = Title,
            CreatedAt = JsonHelpers.FromUnixMilliseconds(Time?.Created),
            UpdatedAt = JsonHelpers.FromUnixMilliseconds(Time?.Updated),
            Summary = Summary
        };
    }
}
