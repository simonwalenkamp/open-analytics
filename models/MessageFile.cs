using System.Text.Json;
using System.Text.Json.Serialization;
using OpenAnalytics.Helpers;

namespace OpenAnalytics.models;

internal sealed class MessageFile
{
    public string? Id { get; set; }
    [JsonPropertyName("sessionID")]
    public string? SessionId { get; set; }
    public string? Role { get; set; }
    [JsonPropertyName("modelID")]
    public string? ModelId { get; set; }
    [JsonPropertyName("providerID")]
    public string? ProviderId { get; set; }
    public string? Mode { get; set; }
    public JsonElement? Error { get; set; }
    public TimeFile? Time { get; set; }
    public MessageModelFile? Model { get; set; }
    public TokenUsage? Tokens { get; set; }

    public Message? ToMessage()
    {
        if (Id is null || SessionId is null)
        {
            return null;
        }

        return new Message
        {
            Id = Id,
            SessionId = SessionId,
            Role = Role,
            ProviderId = ProviderId ?? Model?.ProviderId,
            ModelId = ModelId ?? Model?.ModelId,
            Mode = Mode,
            ErrorName = JsonHelpers.ParseErrorName(Error),
            CreatedAt = JsonHelpers.FromUnixMilliseconds(Time?.Created),
            CompletedAt = JsonHelpers.FromUnixMilliseconds(Time?.Completed),
            Tokens = Tokens
        };
    }
}
