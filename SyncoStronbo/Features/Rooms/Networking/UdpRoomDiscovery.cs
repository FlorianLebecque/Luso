#nullable enable
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SyncoStronbo.Features.Rooms.Networking {
    /// <summary>
    /// SSP/1.0 §3 — UDP broadcast discovery.
    /// Host sends ANNC datagrams every 2 s; Guests listen and raise OnRoomDiscovered.
    /// Encoding: CBOR (RFC 7049), one item per datagram.
    /// </summary>
    internal sealed class UdpRoomDiscovery : IDisposable {
        public const int UdpPort = 5557;

        private UdpClient? _broadcaster;
        private UdpClient? _listener;
        private CancellationTokenSource? _announceCts;
        private CancellationTokenSource? _presenceCts;
        private CancellationTokenSource? _listenCts;

        public event EventHandler<RoomAnnouncement>? OnRoomDiscovered;
        public event EventHandler<GuestPresenceAnnouncement>? OnGuestPresenceDiscovered;
        public event EventHandler<RoomInvite>? OnInviteReceived;
        public event EventHandler<InviteRefusal>? OnInviteRefused;

        public void StartAnnouncing(string roomName, string roomId, int tcpPort, int intervalMs = 2000) {
            StopAnnouncing();

            _announceCts = new CancellationTokenSource();
            var token    = _announceCts.Token;
            string hostIp = GetLocalIpAddress();

            // Build CBOR ANNC payload once (fields do not change during announcement).
            byte[] payload = SspCbor.Annc(roomId, roomName, hostIp, tcpPort);

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
                        // Swallow transient errors; keep announcing.
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

        public void StartGuestPresence(string guestId, string guestName, int intervalMs = 3000) {
            StopGuestPresence();

            _presenceCts = new CancellationTokenSource();
            var token = _presenceCts.Token;
            string guestIp = GetLocalIpAddress();
            byte[] payload = SspCbor.Pres(guestId, guestName, guestIp);

            _broadcaster ??= new UdpClient { EnableBroadcast = true };
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

        public void StopGuestPresence() {
            _presenceCts?.Cancel();
            _presenceCts = null;
        }

        public async Task SendInviteAsync(RoomInvite invite, string guestIp) {
            using var sender = new UdpClient();
            byte[] payload = SspCbor.Invi(invite.InviteId, invite.RoomId, invite.RoomName, invite.HostIp, invite.TcpPort);
            await sender.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Parse(guestIp), UdpPort));
        }

        public async Task SendInviteRefusalAsync(string inviteId, string guestId, string reason, string hostIp) {
            using var sender = new UdpClient();
            byte[] payload = SspCbor.Invr(inviteId, guestId, reason);
            await sender.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Parse(hostIp), UdpPort));
        }

        public void StartListening() {
            StopListening();

            _listenCts = new CancellationTokenSource();
            var token  = _listenCts.Token;
            _listener  = new UdpClient(UdpPort) { EnableBroadcast = true };

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
                var msg = SspCbor.ParseMap(result.Buffer);
                switch (SspCbor.Tag(msg)) {
                    case "ANNC": {
                        string ip = msg.TryGetValue("ip", out var ipObj) && ipObj is string s && s.Length > 0
                            ? s
                            : result.RemoteEndPoint.Address.ToString();

                        var announcement = new RoomAnnouncement(
                            RoomId:   (string)msg["id"]!,
                            RoomName: (string)msg["nm"]!,
                            HostIp:   ip,
                            TcpPort:  (int)(ulong)msg["pt"]!
                        );
                        OnRoomDiscovered?.Invoke(this, announcement);
                        break;
                    }
                    case "PRES": {
                        var presence = new GuestPresenceAnnouncement(
                            GuestId: msg.TryGetValue("gid", out var gid) ? (string)gid! : string.Empty,
                            GuestName: msg.TryGetValue("nm", out var nm) ? (string)nm! : "Unknown",
                            GuestIp: msg.TryGetValue("ip", out var gip) ? (string)gip! : result.RemoteEndPoint.Address.ToString(),
                            ProtocolVersion: msg.TryGetValue("pv", out var pv) ? (string)pv! : string.Empty,
                            Available: msg.TryGetValue("av", out var av) && av is bool b && b
                        );
                        OnGuestPresenceDiscovered?.Invoke(this, presence);
                        break;
                    }
                    case "INVI": {
                        var invite = new RoomInvite(
                            InviteId: msg.TryGetValue("iid", out var iid) ? (string)iid! : Guid.NewGuid().ToString("N"),
                            RoomId: (string)msg["id"]!,
                            RoomName: (string)msg["nm"]!,
                            HostIp: msg.TryGetValue("ip", out var hip) ? (string)hip! : result.RemoteEndPoint.Address.ToString(),
                            TcpPort: (int)(ulong)msg["pt"]!,
                            ProtocolVersion: msg.TryGetValue("pv", out var ipv) ? (string)ipv! : string.Empty
                        );
                        OnInviteReceived?.Invoke(this, invite);
                        break;
                    }
                    case "INVR": {
                        var refusal = new InviteRefusal(
                            InviteId: msg.TryGetValue("iid", out var iid) ? (string)iid! : string.Empty,
                            GuestId: msg.TryGetValue("gid", out var gid) ? (string)gid! : string.Empty,
                            Reason: msg.TryGetValue("rsn", out var rsn) ? (string)rsn! : string.Empty,
                            FromIp: result.RemoteEndPoint.Address.ToString()
                        );
                        OnInviteRefused?.Invoke(this, refusal);
                        break;
                    }
                }
            } catch {
                // Malformed datagrams are silently dropped.
            }
        }

        private static string GetLocalIpAddress() {
            foreach (NetworkInterface iface in NetworkInterface.GetAllNetworkInterfaces()) {
                if (iface.OperationalStatus != OperationalStatus.Up) continue;
                foreach (UnicastIPAddressInformation ip in iface.GetIPProperties().UnicastAddresses) {
                    if (ip.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(ip.Address))
                        return ip.Address.ToString();
                }
            }
            return "127.0.0.1";
        }

        public void Dispose() {
            StopAnnouncing();
            StopGuestPresence();
            StopListening();
        }
    }
}
