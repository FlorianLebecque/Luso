#nullable enable
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;

namespace SyncoStronbo.Features.Rooms.Networking {
    internal sealed class UdpRoomDiscovery : IDisposable {
        public const int UdpPort = 5557;

        private UdpClient? _broadcaster;
        private UdpClient? _listener;
        private CancellationTokenSource? _announceCts;
        private CancellationTokenSource? _listenCts;

        public event EventHandler<RoomAnnouncement>? OnRoomDiscovered;

        public void StartAnnouncing(string roomName, string roomId, int tcpPort, int intervalMs = 2000) {
            StopAnnouncing();

            _announceCts = new CancellationTokenSource();
            var token = _announceCts.Token;
            string hostIp = GetLocalIpAddress();

            var payload = JsonSerializer.SerializeToUtf8Bytes(new {
                Type = "ANNOUNCE",
                RoomId = roomId,
                RoomName = roomName,
                TcpPort = tcpPort,
                HostIp = hostIp
            });

            _broadcaster = new UdpClient { EnableBroadcast = true };
            var endpoint = new IPEndPoint(IPAddress.Broadcast, UdpPort);

            _ = Task.Run(async () => {
                while (!token.IsCancellationRequested) {
                    try {
                        await _broadcaster.SendAsync(payload, payload.Length, endpoint);
                        await Task.Delay(intervalMs, token);
                    } catch (OperationCanceledException) {
                        break;
                    } catch {
                    }
                }
            }, token);
        }

        public void StopAnnouncing() {
            _announceCts?.Cancel();
            _announceCts = null;
            _broadcaster?.Dispose();
            _broadcaster = null;
        }

        public void StartListening() {
            StopListening();

            _listenCts = new CancellationTokenSource();
            var token = _listenCts.Token;
            _listener = new UdpClient(UdpPort) { EnableBroadcast = true };

            _ = Task.Run(async () => {
                while (!token.IsCancellationRequested) {
                    try {
                        UdpReceiveResult result = await _listener.ReceiveAsync(token);
                        HandleDatagram(result);
                    } catch (OperationCanceledException) {
                        break;
                    } catch {
                    }
                }
            }, token);
        }

        public void StopListening() {
            _listenCts?.Cancel();
            _listenCts = null;
            _listener?.Dispose();
            _listener = null;
        }

        private void HandleDatagram(UdpReceiveResult result) {
            try {
                using var doc = JsonDocument.Parse(result.Buffer);
                var root = doc.RootElement;

                if (!root.TryGetProperty("Type", out var typeProp) || typeProp.GetString() != "ANNOUNCE")
                    return;

                var announcement = new RoomAnnouncement(
                    RoomId: root.GetProperty("RoomId").GetString()!,
                    RoomName: root.GetProperty("RoomName").GetString()!,
                    HostIp: root.TryGetProperty("HostIp", out var hip) && hip.GetString() is { Length: > 0 } h
                        ? h
                        : result.RemoteEndPoint.Address.ToString(),
                    TcpPort: root.GetProperty("TcpPort").GetInt32()
                );

                OnRoomDiscovered?.Invoke(this, announcement);
            } catch {
            }
        }

        private static string GetLocalIpAddress() {
            foreach (NetworkInterface iface in NetworkInterface.GetAllNetworkInterfaces()) {
                if (iface.OperationalStatus != OperationalStatus.Up) continue;
                foreach (UnicastIPAddressInformation ip in iface.GetIPProperties().UnicastAddresses) {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip.Address))
                        return ip.Address.ToString();
                }
            }
            return "127.0.0.1";
        }

        public void Dispose() {
            StopAnnouncing();
            StopListening();
        }
    }
}
