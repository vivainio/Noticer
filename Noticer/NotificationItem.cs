namespace Noticer;

public class NotificationItem
{
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Level { get; set; } = "info";
    public string? Source { get; set; }
    public bool Transient { get; set; }
    public bool Slim { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.Now;

    public static NotificationItem? Parse(string text)
    {
        var item = new NotificationItem();
        foreach (var line in text.Split('\n'))
        {
            var idx = line.IndexOf('=');
            if (idx < 0) continue;
            var key = line.Substring(0, idx).Trim();
            var value = line.Substring(idx + 1).Trim();
            switch (key)
            {
                case "title":     item.Title = value; break;
                case "message":   item.Message = value; break;
                case "level":     item.Level = value; break;
                case "source":    item.Source = value; break;
                case "transient": item.Transient = value == "true"; break;
                case "slim":      item.Slim = value == "true"; break;
            }
        }
        return string.IsNullOrWhiteSpace(item.Title) ? null : item;
    }
}
