#nullable enable
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Luso.Features.Rooms.Networking.Ssp {
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
                string remoteIp = result.RemoteEndPoint.Address.ToString();
                switch (SspCbor.Tag(msg)) {
                    case "ANNC": {
                        var announcement = new RoomAnnouncement(
                            RoomId:   (string)msg["id"]!,
                            RoomName: (string)msg["nm"]!,
                            HostIp:   remoteIp,
                            TcpPort:  (int)(ulong)msg["pt"]!
                        );
                        OnRoomDiscovered?.Invoke(this, announcement);
                        break;
                    }
                    case "PRES": {
                        var presence = new GuestPresenceAnnouncement(
                            GuestId: msg.TryGetValue("gid", out var gid) ? (string)gid! : string.Empty,
                            GuestName: msg.TryGetValue("nm", out var nm) ? (string)nm! : "Unknown",
                            GuestIp: remoteIp,
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
                            HostIp: remoteIp,
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
            static bool IsPreferredLan(NetworkInterface iface)
                => iface.NetworkInterfaceType is NetworkInterfaceType.Wireless80211 or NetworkInterfaceType.Ethernet;

            static bool IsLikelyVirtual(NetworkInterface iface) {
                string n = iface.Name.ToLowerInvariant();
                string d = iface.Description.ToLowerInvariant();
                return n.Contains("docker") || n.Contains("veth") || n.Contains("br-") || n.Contains("wg") ||
                       d.Contains("docker") || d.Contains("hyper-v") || d.Contains("virtual") || d.Contains("vpn") || d.Contains("tunnel");
            }

            IEnumerable<NetworkInterface> ordered = NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(iface => iface.OperationalStatus == OperationalStatus.Up)
                .OrderByDescending(IsPreferredLan)
                .ThenBy(iface => IsLikelyVirtual(iface));

            foreach (NetworkInterface iface in ordered) {
                foreach (UnicastIPAddressInformation ip in iface.GetIPProperties().UnicastAddresses) {
                    if (ip.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(ip.Address)) continue;
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
