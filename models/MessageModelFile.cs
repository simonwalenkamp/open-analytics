using System.Text.Json.Serialization;

sealed class MessageModelFile
{
    [JsonPropertyName("providerID")]
    public string? ProviderId { get; set; }
    [JsonPropertyName("modelID")]
    public string? ModelId { get; set; }
}
