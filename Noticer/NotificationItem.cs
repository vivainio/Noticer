using System.Text.Json.Serialization;

namespace Noticer;

public class NotificationItem
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [JsonPropertyName("level")]
    public string Level { get; set; } = "info";

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("transient")]
    public bool Transient { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.Now;
}
