using SyncoStronbo.Devices.Socket;
#nullable enable

namespace SyncoStronbo
{

    /// <summary>
    /// Represents a synchronisation session.
    ///
    /// Use <see cref="Create"/> to start a new room as the host, or
    /// use <see cref="StartDiscovery"/> + <see cref="JoinAsync"/> to join an existing one.
    ///
    /// Flash lifecycle
    /// ───────────────
    ///  Host:  calls <see cref="FlashAsync"/> → sends a timestamped command to every guest
    ///         AND fires <see cref="OnFlashCommand"/> locally so the host flashes too.
    ///  Guest: receives the command via TCP → fires <see cref="OnFlashCommand"/> with the
    ///         same timestamp → caller schedules the flash accordingly.
    /// </summary>
    internal sealed class Room : IDisposable
    {

        // ── Public state ─────────────────────────────────────────────────────────

        public string RoomId { get; private set; } = string.Empty;
        public string RoomName { get; private set; } = string.Empty;
        public bool IsHost { get; private set; }

        // ── Events ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Raised on both the host and guests when a flash should be scheduled.
        /// <see cref="FlashCommand.AtUnixMs"/> is the absolute UTC timestamp at which
        /// the flash must fire.
        /// </summary>
        public event EventHandler<FlashCommand>? OnFlashCommand;

        /// <summary>Raised when a guest connects (host only). Arg = guest IP.</summary>
        public event EventHandler<string>? OnGuestConnected;

        /// <summary>Raised when a guest disconnects (host only). Arg = guest IP.</summary>
        public event EventHandler<string>? OnGuestDisconnected;

        /// <summary>Raised after each heartbeat round-trip (host only). Contains the guest IP and RTT in ms.</summary>
        public event EventHandler<GuestPingArgs>? OnGuestPingUpdated;

        /// <summary>Raised while discovering rooms (guest path). Arg = the announcement.</summary>
        public event EventHandler<RoomAnnouncement>? OnRoomDiscovered;

        /// <summary>Raised when the guest loses connection to the host unexpectedly.</summary>
        public event EventHandler? OnHostDisconnected;

        // ── Private fields ───────────────────────────────────────────────────────

        private SocketRoomHost? _host;
        private SocketRoomGuest? _guest;
        private UdpRoomDiscovery? _discovery;

        private Room() { }

        // ── Factory methods ──────────────────────────────────────────────────────

        /// <summary>
        /// Create a new room and become the host.
        /// Immediately starts accepting TCP connections and broadcasting UDP announcements.
        /// </summary>
        public static Room Create(string roomName)
        {
            var room = new Room
            {
                RoomId = Guid.NewGuid().ToString("N")[..8],
                RoomName = roomName,
                IsHost = true
            };

            room._host = new SocketRoomHost(roomName, room.RoomId);
            room._host.OnGuestConnected    += (s, ip)  => room.OnGuestConnected?.Invoke(room, ip);
            room._host.OnGuestDisconnected += (s, ip)  => room.OnGuestDisconnected?.Invoke(room, ip);
            room._host.OnGuestPingUpdated  += (s, arg) => room.OnGuestPingUpdated?.Invoke(room, arg);
            room._host.OnFlashScheduled    += (s, cmd) => room.OnFlashCommand?.Invoke(room, cmd);

            room._discovery = new UdpRoomDiscovery();
            room._discovery.StartAnnouncing(roomName, room.RoomId, SocketRoomHost.TcpPort);

            return room;
        }

        /// <summary>
        /// Start listening for UDP room announcements.
        /// Subscribe to <see cref="OnRoomDiscovered"/> to populate a room list in the UI,
        /// then call <see cref="JoinAsync"/> with the chosen announcement.
        /// </summary>
        public void StartDiscovery()
        {
            _discovery ??= new UdpRoomDiscovery();
            _discovery.OnRoomDiscovered += (s, ann) => OnRoomDiscovered?.Invoke(this, ann);
            _discovery.StartListening();
        }

        public void StopDiscovery() => _discovery?.StopListening();

        /// <summary>
        /// Join an existing room as a guest.
        /// </summary>
        public static async Task<Room> JoinAsync(RoomAnnouncement announcement)
        {
            var room = new Room
            {
                RoomId = announcement.RoomId,
                RoomName = announcement.RoomName,
                IsHost = false
            };

            room._guest = await SocketRoomGuest.ConnectAsync(announcement);
            room._guest.OnFlashCommand += (s, cmd) => room.OnFlashCommand?.Invoke(room, cmd);
            room._guest.OnDisconnected += (s, e) => room.OnHostDisconnected?.Invoke(room, e);

            return room;
        }

        // ── Commands (host only) ─────────────────────────────────────────────────

        /// <summary>
        /// Schedule a synchronised flash across all connected guests and the host itself.
        /// Throws <see cref="InvalidOperationException"/> if called on a guest instance.
        /// </summary>
        /// <param name="action">"on" or "off"</param>
        public async Task FlashAsync(string action = "on")
        {
            if (!IsHost || _host is null)
                throw new InvalidOperationException("Only the room host can trigger a flash.");

            await _host.FlashAsync(action);
        }

        public int GuestCount => _host?.GuestCount ?? 0;

        /// <summary>Snapshot of all connected guests with their latest RTT (host only).</summary>
        public IReadOnlyList<(string Ip, int RttMs)> GetGuests()
            => _host?.GetGuests() ?? Array.Empty<(string, int)>();

        // ── Lifecycle ────────────────────────────────────────────────────────────

        public void Dispose()
        {
            _host?.Dispose();
            _guest?.Dispose();
            _discovery?.Dispose();
        }
    }
}
