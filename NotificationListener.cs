using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Noticer;

public class NotificationListener : IDisposable
{
    private readonly TcpListener _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public const int Port = 5556;

    public event Action<NotificationItem>? NotificationReceived;

    public NotificationListener()
    {
        _listener = new TcpListener(IPAddress.Loopback, Port);
    }

    public void Start()
    {
        _listener.Start();
        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener.Stop();
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleClientAsync(client, ct), ct);
            }
            catch (OperationCanceledException) { break; }
            catch { /* listener stopped */ }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        using (var reader = new StreamReader(client.GetStream(), Encoding.UTF8))
        {
            try
            {
                string? line;
                while ((line = await reader.ReadLineAsync(ct)) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var item = JsonSerializer.Deserialize<NotificationItem>(line);
                    if (item != null)
                    {
                        item.ReceivedAt = DateTime.Now;
                        NotificationReceived?.Invoke(item);
                    }
                }
            }
            catch { /* client disconnected */ }
        }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
