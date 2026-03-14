#nullable enable
using SyncoStronbo.Features.Rooms.Networking;

namespace SyncoStronbo.Features.Rooms.Domain {
    internal sealed class Room : IDisposable {
        public string RoomId { get; private set; } = string.Empty;
        public string RoomName { get; private set; } = string.Empty;
        public string HostIp { get; private set; } = string.Empty;
        public bool IsHost { get; private set; }

        public event EventHandler<FlashCommand>? OnFlashCommand;
        public event EventHandler<string>? OnGuestConnected;
        public event EventHandler<string>? OnGuestDisconnected;
        public event EventHandler<GuestPingArgs>? OnGuestPingUpdated;
        public event EventHandler<RoomAnnouncement>? OnRoomDiscovered;
        public event EventHandler? OnHostDisconnected;

        private SocketRoomHost? _host;
        private SocketRoomGuest? _guest;
        private UdpRoomDiscovery? _discovery;

        private Room() { }

        public static Room Create(string roomName) {
            var room = new Room {
                RoomId = Guid.NewGuid().ToString("N")[..8],
                RoomName = roomName,
                HostIp = "localhost",
                IsHost = true
            };

            room._host = new SocketRoomHost(roomName, room.RoomId);
            room._host.OnGuestConnected += (s, ip) => room.OnGuestConnected?.Invoke(room, ip);
            room._host.OnGuestDisconnected += (s, ip) => room.OnGuestDisconnected?.Invoke(room, ip);
            room._host.OnGuestPingUpdated += (s, arg) => room.OnGuestPingUpdated?.Invoke(room, arg);
            room._host.OnFlashScheduled += (s, cmd) => room.OnFlashCommand?.Invoke(room, cmd);

            room._discovery = new UdpRoomDiscovery();
            room._discovery.StartAnnouncing(roomName, room.RoomId, SocketRoomHost.TcpPort);
            return room;
        }

        public void StartDiscovery() {
            _discovery ??= new UdpRoomDiscovery();
            _discovery.OnRoomDiscovered += (s, ann) => OnRoomDiscovered?.Invoke(this, ann);
            _discovery.StartListening();
        }

        public void StopDiscovery() => _discovery?.StopListening();

        public static async Task<Room> JoinAsync(RoomAnnouncement announcement) {
            var room = new Room {
                RoomId = announcement.RoomId,
                RoomName = announcement.RoomName,
                HostIp = announcement.HostIp,
                IsHost = false
            };

            room._guest = await SocketRoomGuest.ConnectAsync(announcement);
            room._guest.OnFlashCommand += (s, cmd) => room.OnFlashCommand?.Invoke(room, cmd);
            room._guest.OnDisconnected += (s, e) => room.OnHostDisconnected?.Invoke(room, e);
            return room;
        }

        public async Task FlashAsync(string action = "on") {
            if (!IsHost || _host is null)
                throw new InvalidOperationException("Only the room host can trigger a flash.");

            await _host.FlashAsync(action);
        }

        public int GuestCount => _host?.GuestCount ?? 0;

        public IReadOnlyList<(string Ip, int RttMs)> GetGuests()
            => _host?.GetGuests() ?? Array.Empty<(string, int)>();

        public void Dispose() {
            _host?.Dispose();
            _guest?.Dispose();
            _discovery?.Dispose();
        }
    }
}
