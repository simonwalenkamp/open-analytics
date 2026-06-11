using System.Text.Json.Serialization;

namespace OpenAnalytics.models;

internal sealed class MessageModelFile
{
    [JsonPropertyName("providerID")]
    public string? ProviderId { get; set; }
    [JsonPropertyName("modelID")]
    public string? ModelId { get; set; }
}
