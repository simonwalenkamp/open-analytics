using System.Text.Json;
using System.Text.Json.Serialization;

sealed class PartFile
{
    public string? Id { get; set; }
    [JsonPropertyName("sessionID")]
    public string? SessionId { get; set; }
    [JsonPropertyName("messageID")]
    public string? MessageId { get; set; }
    public string? Type { get; set; }
    public string? Text { get; set; }
    [JsonPropertyName("callID")]
    public string? CallId { get; set; }
    public string? Tool { get; set; }
    public string? Reason { get; set; }
    public string? Snapshot { get; set; }
    public decimal? Cost { get; set; }
    public TokenUsage? Tokens { get; set; }
    public PartTimeFile? Time { get; set; }
    public JsonElement? State { get; set; }
    public JsonElement? Input { get; set; }
    public JsonElement? Output { get; set; }

    public OpencodePart? ToPart(string sourcePath)
    {
        if (Id is null || SessionId is null || MessageId is null)
        {
            return null;
        }

        return new OpencodePart
        {
            Id = Id,
            SessionId = SessionId,
            MessageId = MessageId,
            SourcePath = sourcePath,
            Type = Type,
            Text = Text,
            Tool = Tool,
            CallId = CallId,
            ToolStatus = JsonHelpers.TryGetString(State, "status"),
            Reason = Reason,
            Snapshot = Snapshot,
            Cost = Cost,
            Tokens = Tokens,
            StartedAt = JsonHelpers.FromUnixMilliseconds(Time?.Start),
            EndedAt = JsonHelpers.FromUnixMilliseconds(Time?.End),
            State = State,
            Input = Input,
            Output = Output
        };
    }
}
