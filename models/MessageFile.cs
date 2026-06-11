using System.Text.Json;
using System.Text.Json.Serialization;

sealed class MessageFile
{
    public string? Id { get; set; }
    [JsonPropertyName("sessionID")]
    public string? SessionId { get; set; }
    public string? Role { get; set; }
    [JsonPropertyName("parentID")]
    public string? ParentId { get; set; }
    [JsonPropertyName("modelID")]
    public string? ModelId { get; set; }
    [JsonPropertyName("providerID")]
    public string? ProviderId { get; set; }
    public string? Mode { get; set; }
    public string? Agent { get; set; }
    public decimal? Cost { get; set; }
    public string? Finish { get; set; }
    public TimeFile? Time { get; set; }
    public JsonElement? Summary { get; set; }
    public MessageModelFile? Model { get; set; }
    public TokenUsage? Tokens { get; set; }

    public OpencodeMessage? ToMessage(string sourcePath)
    {
        if (Id is null || SessionId is null)
        {
            return null;
        }

        return new OpencodeMessage
        {
            Id = Id,
            SessionId = SessionId,
            SourcePath = sourcePath,
            Role = Role,
            ParentId = ParentId,
            Agent = Agent,
            Mode = Mode,
            ProviderId = ProviderId ?? Model?.ProviderId,
            ModelId = ModelId ?? Model?.ModelId,
            Finish = Finish,
            Cost = Cost,
            CreatedAt = JsonHelpers.FromUnixMilliseconds(Time?.Created),
            CompletedAt = JsonHelpers.FromUnixMilliseconds(Time?.Completed),
            Summary = JsonHelpers.ParseMessageSummary(Summary),
            Tokens = Tokens
        };
    }
}
