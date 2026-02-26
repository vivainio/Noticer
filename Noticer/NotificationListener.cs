using System.Net.Sockets;
using System.Text;

namespace Noticer;

public class NotificationListener : IDisposable
{
    private readonly UdpClient _udpClient;
    private CancellationTokenSource? _cts;

    public const int Port = 49152;

    public event Action<NotificationItem>? NotificationReceived;

    public NotificationListener()
    {
        _udpClient = new UdpClient(Port);
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => ReceiveLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _udpClient.Close();
    }

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = await _udpClient.ReceiveAsync();
                var text = Encoding.UTF8.GetString(result.Buffer);
                var item = NotificationItem.Parse(text);
                if (item != null)
                {
                    item.ReceivedAt = DateTime.Now;
                    NotificationReceived?.Invoke(item);
                }
            }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { break; }
            catch { /* malformed packet, ignore */ }
        }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        _udpClient.Dispose();
    }
}
