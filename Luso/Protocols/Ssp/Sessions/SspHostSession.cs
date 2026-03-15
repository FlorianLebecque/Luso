#nullable enable
using System.Collections.Concurrent;
using Luso.Features.Rooms.Domain.Devices;
using Luso.Features.Rooms.Domain.Technologies;

namespace Luso.Features.Rooms.Networking.Ssp
{
    /// <summary>
    /// Host-side SSP session — pure lifecycle.
    ///
    /// Accepts connections, translates each into an <see cref="SspDevice"/> whose
    /// targets hold delegates back to <see cref="SocketRoomHost"/>. Command dispatch
    /// therefore flows domain → IDevice → ITarget → delegate → socket; this class
    /// never relays commands itself.
    /// </summary>
    internal sealed class SspHostSession : IRoomHostSession
    {
        private readonly string _roomId;
        private readonly string _roomName;
        private readonly ConcurrentDictionary<string, SspDevice> _devices = new();
        private SocketRoomHost? _host;

        public event EventHandler<IDevice>? OnGuestConnected;
        public event EventHandler<IDevice>? OnGuestDisconnected;
        public event EventHandler<IDevice>? OnGuestLatencyUpdated;

        public int DeviceCount => _devices.Count;

        public SspHostSession(string roomId, string roomName)
        {
            _roomId = roomId;
            _roomName = roomName;
        }

        public Task StartAsync()
        {
            if (_host is not null) return Task.CompletedTask;

            _host = new SocketRoomHost(_roomName, _roomId);

            _host.OnGuestConnected += (_, args) =>
            {
                var ip = args.Ip;
                var device = new SspDevice(
                    ip, args.Name, args.Capabilities,
                    flashGuest: cmd => _host.FlashGuestAsync(ip, cmd.Action == FlashAction.On ? "on" : "off"),
                    disconnectSelf: () => _host.KickGuestAsync(ip));
                _devices[ip] = device;
                OnGuestConnected?.Invoke(this, device);
            };

            _host.OnGuestDisconnected += (_, ip) =>
            {
                if (_devices.TryRemove(ip, out var device))
                {
                    device.SetStatus(DeviceStatus.Disconnected);
                    OnGuestDisconnected?.Invoke(this, device);
                }
            };

            _host.OnGuestPingUpdated += (_, arg) =>
            {
                if (_devices.TryGetValue(arg.Ip, out var device))
                {
                    device.UpdateLatency(arg.RttMs);
                    OnGuestLatencyUpdated?.Invoke(this, device);
                }
            };

            return Task.CompletedTask;
        }

        public Task StopAsync() => Task.CompletedTask;

        public async Task CloseAsync()
        {
            if (_host is null) return;
            foreach (var ip in _devices.Keys.ToList())
                await _host.KickGuestAsync(ip);
        }

        public IReadOnlyList<IDevice> GetDevices() => _devices.Values.ToList<IDevice>();

        public void Dispose()
        {
            _host?.Dispose();
        }
    }
}
