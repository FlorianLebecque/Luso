#nullable enable
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SyncoStronbo.Devices.Socket {

    /// <summary>
    /// Handles LAN room discovery via UDP broadcast.
    ///
    /// HOST side  → call <see cref="StartAnnouncing"/> to periodically broadcast the room's
    ///              existence on the LAN (no connection required from guests).
    ///
    /// GUEST side → call <see cref="StartListening"/> to collect incoming announcements;
    ///              subscribe to <see cref="OnRoomDiscovered"/> to react to each one.
    /// </summary>
    internal sealed class UdpRoomDiscovery : IDisposable {

        public const int UdpPort = 5557;

        private UdpClient?             _broadcaster;
        private UdpClient?             _listener;
        private CancellationTokenSource? _announceCts;
        private CancellationTokenSource? _listenCts;

        /// <summary>Raised on the thread-pool each time a room announcement is received.</summary>
        public event EventHandler<RoomAnnouncement>? OnRoomDiscovered;

        // ── Host side ────────────────────────────────────────────────────────────

        /// <summary>
        /// Start broadcasting a room announcement every <paramref name="intervalMs"/> ms.
        /// </summary>
        public void StartAnnouncing(string roomName, string roomId, int tcpPort, int intervalMs = 2000) {
            StopAnnouncing();

            _announceCts = new CancellationTokenSource();
            var token = _announceCts.Token;

            string hostIp = GetLocalIpAddress();

            // Build the JSON payload once.
            var payload = JsonSerializer.SerializeToUtf8Bytes(new {
                Type     = "ANNOUNCE",
                RoomId   = roomId,
                RoomName = roomName,
                TcpPort  = tcpPort,
                HostIp   = hostIp
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
                        // network errors are transient; keep announcing
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

        // ── Guest side ───────────────────────────────────────────────────────────

        /// <summary>
        /// Start listening for room announcements on UDP port <see cref="UdpPort"/>.
        /// Fires <see cref="OnRoomDiscovered"/> for every valid announcement received.
        /// </summary>
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
                        // malformed packet or socket error – continue
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

        // ── Internals ────────────────────────────────────────────────────────────

        private void HandleDatagram(UdpReceiveResult result) {
            try {
                using var doc = JsonDocument.Parse(result.Buffer);
                var root = doc.RootElement;

                if (!root.TryGetProperty("Type", out var typeProp) || typeProp.GetString() != "ANNOUNCE")
                    return;

                var announcement = new RoomAnnouncement(
                    RoomId:   root.GetProperty("RoomId").GetString()!,
                    RoomName: root.GetProperty("RoomName").GetString()!,
                    // Prefer the IP embedded in the packet; fall back to the UDP sender address.
                    HostIp:   root.TryGetProperty("HostIp", out var hip) && hip.GetString() is { Length: > 0 } h
                                  ? h
                                  : result.RemoteEndPoint.Address.ToString(),
                    TcpPort:  root.GetProperty("TcpPort").GetInt32()
                );

                OnRoomDiscovered?.Invoke(this, announcement);
            } catch {
                // ignore unparseable datagrams
            }
        }

        private static string GetLocalIpAddress() {
            foreach (NetworkInterface iface in NetworkInterface.GetAllNetworkInterfaces()) {
                if (iface.OperationalStatus != OperationalStatus.Up) continue;
                foreach (UnicastIPAddressInformation ip in iface.GetIPProperties().UnicastAddresses) {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(ip.Address)) {
                        return ip.Address.ToString();
                    }
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
