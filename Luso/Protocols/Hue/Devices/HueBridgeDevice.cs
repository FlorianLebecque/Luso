#nullable enable
using Luso.Features.Rooms.Domain.Devices;
using Luso.Features.Rooms.Domain.Targets;

namespace Luso.Features.Rooms.Networking.Hue
{
    /// <summary>
    /// <see cref="IDevice"/> that represents a single Philips Hue Bridge in the room.
    ///
    /// Created by <see cref="HueInviteSession"/> after a successful pairing and light
    /// enumeration. Each light on the bridge becomes one <see cref="HueLightTarget"/>
    /// in <see cref="Targets"/> with <see cref="TargetKind.Screen"/>.
    ///
    /// The bridge is permanently connected while in the room; no network heartbeat is
    /// required, so <see cref="LatencyMs"/> is always -1.
    ///
    /// Calling <see cref="DisconnectAsync"/> removes the bridge from the host session
    /// via the <c>onDisconnect</c> callback injected at construction.
    /// </summary>
    internal sealed class HueBridgeDevice : IDevice
    {
        private volatile DeviceStatus _status;
        private readonly Action<string> _onDisconnect;

        // ── IDevice ───────────────────────────────────────────────────────────

        public string DeviceId { get; }
        public string DeviceName { get; }
        public DeviceStatus Status => _status;
        public int LatencyMs => -1;
        public IReadOnlyList<ITarget> Targets { get; }

        public event EventHandler<DeviceStatus>? OnStatusChanged;
        public event EventHandler<int>? OnLatencyUpdated;   // never raised for Hue

        // ── Constructor ───────────────────────────────────────────────────────

        internal HueBridgeDevice(
            string bridgeId,
            string ipAddress,
            IReadOnlyList<ITarget> lights,
            Action<string> onDisconnect)
        {
            DeviceId = bridgeId;
            DeviceName = $"Hue Bridge ({ipAddress})";
            Targets = lights;
            _onDisconnect = onDisconnect;
            _status = DeviceStatus.Ready;
        }

        // ── Internal state updater ────────────────────────────────────────────

        internal void SetStatus(DeviceStatus status)
        {
            _status = status;
            OnStatusChanged?.Invoke(this, status);
        }

        // ── IDevice ───────────────────────────────────────────────────────────

        public Task DisconnectAsync()
        {
            SetStatus(DeviceStatus.Disconnected);
            _onDisconnect(DeviceId);
            return Task.CompletedTask;
        }
    }
}
