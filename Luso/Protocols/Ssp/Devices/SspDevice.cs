#nullable enable
using Luso.Features.Rooms.Domain.Commands;
using Luso.Features.Rooms.Domain.Devices;
using Luso.Features.Rooms.Domain.Targets;

namespace Luso.Features.Rooms.Networking.Ssp
{
    /// <summary>
    /// <see cref="IDevice"/> implementation for a remote guest connected over SSP/1.0.
    ///
    /// Created by <c>SocketRoomHost</c> after the JOIN/JACK handshake completes.
    /// The host-side room subscribes to <see cref="SocketRoomHost.OnGuestPingUpdated"/>
    /// and forwards updates via <see cref="UpdateLatency"/>; when the guest disconnects
    /// the room calls <see cref="SetStatus"/> with <see cref="DeviceStatus.Disconnected"/>.
    ///
    /// <see cref="Targets"/> is populated from the <see cref="GuestCapabilities"/> received
    /// in the JOIN message and is immutable for the lifetime of the session.
    /// </summary>
    internal sealed class SspDevice : IDevice
    {
        private volatile DeviceStatus _status;
        private volatile int _latencyMs = -1;
        private readonly Func<Task<bool>> _disconnectSelf;

        // ── IDevice ───────────────────────────────────────────────────────────

        /// <summary>The guest's IP address (primary key used by <c>SocketRoomHost</c>).</summary>
        public string DeviceId { get; }
        public string DeviceName { get; }

        public DeviceStatus Status => _status;
        public int LatencyMs => _latencyMs;
        public IReadOnlyList<ITarget> Targets { get; }

        public event EventHandler<DeviceStatus>? OnStatusChanged;
        public event EventHandler<int>? OnLatencyUpdated;

        // ── Constructor ───────────────────────────────────────────────────────

        internal SspDevice(
            string ip, string name, GuestCapabilities capabilities,
            Func<FlashCommand, Task> flashGuest,
            Func<Task<bool>> disconnectSelf)
        {
            DeviceId = ip;
            DeviceName = name;
            _disconnectSelf = disconnectSelf;
            Targets = BuildTargets(capabilities, flashGuest);
            _status = DeviceStatus.Ready;
        }

        public async Task DisconnectAsync() => await _disconnectSelf();

        // ── Internal state updaters (called by Room / SocketRoomHost) ─────────

        /// <summary>Updates round-trip latency and raises <see cref="OnLatencyUpdated"/>.</summary>
        internal void UpdateLatency(int rttMs)
        {
            _latencyMs = rttMs;
            OnLatencyUpdated?.Invoke(this, rttMs);
        }

        /// <summary>Transitions the device to a new status and raises <see cref="OnStatusChanged"/>.</summary>
        internal void SetStatus(DeviceStatus status)
        {
            _status = status;
            OnStatusChanged?.Invoke(this, status);
        }

        private static IReadOnlyList<ITarget> BuildTargets(
            GuestCapabilities caps,
            Func<FlashCommand, Task> flashGuest)
        {
            var targets = new List<ITarget>(3);

            if (caps.HasFlashlight)
                targets.Add(new SspRemoteTarget(
                    "flashlight", TargetKind.Flashlight, "Flashlight",
                    cmd => flashGuest(cmd)));

            if (caps.HasScreen)
                targets.Add(new SspRemoteTarget(
                    "screen", TargetKind.Screen, "Screen",
                    cmd => flashGuest(cmd)));

            if (caps.HasVibration)
                targets.Add(new SspRemoteTarget(
                    "vibration", TargetKind.Vibration, "Vibration",
                    cmd => flashGuest(cmd)));

            return targets.AsReadOnly();
        }
    }
}
