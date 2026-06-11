using System.Text.Json;
using System.Text.Json.Serialization;
using OpenAnalytics.Helpers;

namespace OpenAnalytics.models;

internal sealed class PartFile
{
    public string? Id { get; set; }
    [JsonPropertyName("messageID")]
    public string? MessageId { get; set; }
    public string? Tool { get; set; }
    public PartTimeFile? Time { get; set; }
    public JsonElement? State { get; set; }

    public OpencodePart? ToPart()
    {
        if (Id is null || MessageId is null)
        {
            return null;
        }

        return new OpencodePart
        {
            Id = Id,
            MessageId = MessageId,
            Tool = Tool,
            ToolStatus = JsonHelpers.TryGetString(State, "status"),
            StartedAt = JsonHelpers.FromUnixMilliseconds(Time?.Start),
            State = State
        };
    }
}
