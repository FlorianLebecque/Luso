#nullable enable
using System.Collections.Concurrent;
using Luso.Features.Rooms.Domain.Commands;
using Luso.Features.Rooms.Domain.Devices;
using Luso.Features.Rooms.Domain.Targets;
using Luso.Features.Rooms.Domain.Technologies;

namespace Luso.Features.Rooms.Domain
{
    /// <summary>
    /// A synchronized room session — pure domain, zero protocol knowledge.
    ///
    /// Construction is delegated to <c>RoomFactory</c> in the infrastructure layer.
    /// <c>Room</c> never imports <c>Luso.Infrastructure</c> or any SSP type;
    /// it only works with domain interfaces injected by the factory.
    ///
    /// Command dispatch flows: Room → IDevice → ITarget.ExecuteAsync → protocol delegate.
    /// </summary>
    internal sealed class Room : IDisposable
    {
        public string RoomId { get; }
        public string RoomName { get; }
        public bool IsHost { get; }
        public LocalDevice? LocalDevice { get; }

        /// <summary>The discovered room info used to join (guest-only, null for host).</summary>
        public IDiscoveredRoom? SourceDiscovery { get; }

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>A nearby device is advertising itself (host-only).</summary>
        public event EventHandler<IDiscoveredDevice>? OnCandidateDiscovered;
        /// <summary>A remote device completed joining and is ready for commands.</summary>
        public event EventHandler<IDevice>? OnDeviceConnected;
        /// <summary>A remote device left or was removed.</summary>
        public event EventHandler<IDevice>? OnDeviceDisconnected;
        /// <summary>Host closed the session or connection was lost (guest-only).</summary>
        public event EventHandler? OnHostDisconnected;
        /// <summary>Host explicitly kicked this device (guest-only).</summary>
        public event EventHandler? OnKicked;

        // ── Sessions (injected by RoomFactory) ────────────────────────────────

        private readonly List<IRoomHostSession> _hostSessions = new();
        private readonly List<IInviteSession> _inviteSessionsList = new();
        private readonly List<IRoomAnnouncer> _announcers = new();
        private readonly Dictionary<string, IInviteSession> _inviteByTech =
            new(StringComparer.OrdinalIgnoreCase);
        private IRoomGuestSession? _guestSession;

        // ── Connected devices ─────────────────────────────────────────────────

        private readonly ConcurrentDictionary<string, IDevice> _devices = new();

        // ── Constructor ───────────────────────────────────────────────────────

        internal Room(string roomId, string roomName, bool isHost, LocalDevice? localDevice, IDiscoveredRoom? sourceDiscovery = null)
        {
            RoomId = roomId;
            RoomName = roomName;
            IsHost = isHost;
            LocalDevice = localDevice;
            SourceDiscovery = sourceDiscovery;
        }

        // ── Builder methods (called by RoomFactory) ───────────────────────────

        internal void AddHostSession(IRoomHostSession session)
        {
            _hostSessions.Add(session);
            session.OnGuestConnected += (_, d) => { _devices[d.DeviceId] = d; OnDeviceConnected?.Invoke(this, d); };
            session.OnGuestDisconnected += (_, d) => { _devices.TryRemove(d.DeviceId, out IDevice? __); OnDeviceDisconnected?.Invoke(this, d); };
        }

        internal void AddInviteSession(string technologyId, IInviteSession session)
        {
            _inviteSessionsList.Add(session);
            _inviteByTech[technologyId] = session;
            session.OnDevicePresenceDiscovered += (_, d) => OnCandidateDiscovered?.Invoke(this, d);
        }

        internal void SetGuestSession(IRoomGuestSession session)
        {
            _guestSession = session;
            session.OnHostDisconnected += (_, e) => OnHostDisconnected?.Invoke(this, e);
            session.OnKicked += (_, e) => OnKicked?.Invoke(this, e);
        }

        internal void AddAnnouncer(IRoomAnnouncer announcer)
        {
            _announcers.Add(announcer);
        }

        // ── StartAsync (called by UI or factory after build) ──────────────────

        /// <summary>
        /// Starts all added host sessions, invite sessions and announcers.
        /// For a guest room, starts the guest session (TCP connect).
        /// </summary>
        public async Task StartAsync()
        {
            foreach (var s in _hostSessions)
                await s.StartAsync();

            foreach (var s in _inviteSessionsList)
                s.Start();

            foreach (var a in _announcers)
                await a.StartAsync();

            if (_guestSession is not null)
                await _guestSession.StartAsync();
        }

        // ── Commands ── dispatched through IDevice → ITarget ──────────────────

        /// <summary>Broadcasts a flash command to all devices including the local device.</summary>
        public Task FlashAsync(FlashAction action = FlashAction.On, TargetKind kind = TargetKind.Flashlight)
        {
            if (!IsHost) throw new InvalidOperationException("Only the room host can trigger a flash.");
            var cmd = new FlashCommand(action, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 50);

            IEnumerable<IDevice> allDevices = _devices.Values;
            if (LocalDevice is not null)
                allDevices = allDevices.Prepend(LocalDevice);

            return Task.WhenAll(allDevices
                .SelectMany(d => d.Targets)
                .Where(t => t.Kind == kind)
                .Select(t => t.ExecuteAsync(cmd)));
        }

        /// <summary>Sends a flash command to one specific device (including the local device by ID).</summary>
        public Task FlashDeviceAsync(string deviceId, FlashAction action = FlashAction.On, TargetKind kind = TargetKind.Flashlight)
        {
            if (!IsHost) throw new InvalidOperationException("Only the room host can trigger a flash.");

            IDevice? device;
            if (LocalDevice is not null && LocalDevice.DeviceId == deviceId)
                device = LocalDevice;
            else if (!_devices.TryGetValue(deviceId, out device))
                return Task.CompletedTask;

            var cmd = new FlashCommand(action, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 50);
            return Task.WhenAll(device.Targets
                .Where(t => t.Kind == kind)
                .Select(t => t.ExecuteAsync(cmd)));
        }

        /// <summary>Sends a flash command to one specific target on a device (by TargetId).</summary>
        public Task FlashTargetAsync(string deviceId, FlashAction action, string targetId)
        {
            if (!IsHost) throw new InvalidOperationException("Only the room host can trigger a flash.");

            IDevice? device;
            if (LocalDevice is not null && LocalDevice.DeviceId == deviceId)
                device = LocalDevice;
            else if (!_devices.TryGetValue(deviceId, out device))
                return Task.CompletedTask;

            var target = device.Targets.FirstOrDefault(t => t.TargetId == targetId);
            if (target is null) return Task.CompletedTask;

            var cmd = new FlashCommand(action, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 50);
            return target.ExecuteAsync(cmd);
        }

        /// <summary>Removes a device from the session.</summary>
        public async Task<bool> KickDeviceAsync(string deviceId)
        {
            if (!IsHost) throw new InvalidOperationException("Only the room host can kick devices.");
            if (!_devices.TryGetValue(deviceId, out var device)) return false;
            await device.DisconnectAsync();
            return true;
        }

        // ── Invite ────────────────────────────────────────────────────────────

        /// <summary>Sends an invite to the given candidate, routing through the correct technology.</summary>
        public Task SendInviteAsync(IDiscoveredDevice device)
        {
            if (!IsHost) throw new InvalidOperationException("Only the room host can send invites.");
            if (!_inviteByTech.TryGetValue(device.TechnologyId, out var session))
                throw new InvalidOperationException($"No invite session for technology '{device.TechnologyId}'.");
            return session.SendInviteAsync(device, RoomId, RoomName);
        }

        // ── Queries ───────────────────────────────────────────────────────────

        /// <summary>Number of remote devices currently in the session (host-only).</summary>
        public int DeviceCount => _devices.Count;

        /// <summary>Returns a snapshot of all remote devices in the session (host-only).</summary>
        public IReadOnlyList<IDevice> GetDevices() => _devices.Values.ToList();

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public void Dispose()
        {
            foreach (var a in _announcers) { try { a.StopAsync().GetAwaiter().GetResult(); a.Dispose(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Room] Announcer dispose error: {ex.Message}"); } }
            foreach (var s in _inviteSessionsList) { try { s.Stop(); s.Dispose(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Room] InviteSession dispose error: {ex.Message}"); } }
            foreach (var s in _hostSessions) { try { s.Dispose(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Room] HostSession dispose error: {ex.Message}"); } }
            _guestSession?.Dispose();
        }
    }
}
