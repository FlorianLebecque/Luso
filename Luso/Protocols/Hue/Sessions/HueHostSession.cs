#nullable enable
using System.Collections.Concurrent;
using Luso.Features.Rooms.Domain.Devices;
using Luso.Features.Rooms.Domain.Technologies;

namespace Luso.Features.Rooms.Networking.Hue
{
    /// <summary>
    /// Host-side session for the Hue technology.
    ///
    /// Unlike SSP, Hue bridges are not TCP peers — there is no inbound connection to
    /// accept. This session is therefore passive: it only tracks devices added via
    /// <see cref="AddDevice"/> (called by <see cref="HueInviteSession"/> after pairing)
    /// and raises the standard <see cref="IRoomHostSession"/> lifecycle events so the
    /// <see cref="Luso.Features.Rooms.Domain.Room"/> remains protocol-agnostic.
    ///
    /// <see cref="StartAsync"/> and <see cref="StopAsync"/> are no-ops.
    /// <see cref="CloseAsync"/> disconnects all tracked bridges.
    /// </summary>
    internal sealed class HueHostSession : IRoomHostSession
    {
        private readonly ConcurrentDictionary<string, HueBridgeDevice> _devices = new();

        public event EventHandler<IDevice>? OnGuestConnected;
        public event EventHandler<IDevice>? OnGuestDisconnected;
        public event EventHandler<IDevice>? OnGuestLatencyUpdated;  // never raised for Hue

        public int DeviceCount => _devices.Count;

        // ── IRoomHostSession lifecycle ─────────────────────────────────────────

        public Task StartAsync() => Task.CompletedTask;
        public Task StopAsync() => Task.CompletedTask;

        public async Task CloseAsync()
        {
            foreach (var device in _devices.Values.ToList())
                await device.DisconnectAsync();
        }

        public IReadOnlyList<IDevice> GetDevices() => _devices.Values.ToList<IDevice>();

        public void Dispose() { /* no managed resources to release */ }

        // ── Internal device management (called by HueInviteSession) ──────────

        /// <summary>Registers a newly paired bridge and raises <see cref="OnGuestConnected"/>.</summary>
        internal void AddDevice(HueBridgeDevice device)
        {
            _devices[device.DeviceId] = device;
            OnGuestConnected?.Invoke(this, device);
        }

        /// <summary>Removes a bridge and raises <see cref="OnGuestDisconnected"/>.</summary>
        internal void RemoveDevice(string deviceId)
        {
            if (_devices.TryRemove(deviceId, out var device))
                OnGuestDisconnected?.Invoke(this, device);
        }
    }
}
