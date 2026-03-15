#nullable enable
using System.Diagnostics;
using Luso.Features.Rooms.Domain.Commands;
using Luso.Features.Rooms.Domain.Devices;
using Luso.Features.Rooms.Domain.Technologies;

namespace Luso.Features.Rooms.Networking.Ssp
{
    /// <summary>
    /// Guest-side SSP session — pure lifecycle.
    ///
    /// When the host sends a FLSH command the session dispatches it directly to
    /// the local device's targets via <see cref="ITarget.ExecuteAsync"/> and raises
    /// <see cref="OnFlashCommand"/>.
    /// </summary>
    internal sealed class SspGuestSession : IRoomGuestSession
    {
        private readonly RoomAnnouncement _announcement;
        private readonly LocalDevice _localDevice;
        private SocketRoomGuest? _guest;

        public event EventHandler? OnHostDisconnected;
        public event EventHandler? OnKicked;
        public event EventHandler<FlashCommand>? OnFlashCommand;

        public SspGuestSession(RoomAnnouncement announcement, LocalDevice localDevice)
        {
            _announcement = announcement;
            _localDevice = localDevice;
        }

        public async Task StartAsync()
        {
            if (_guest is not null) return;

            var rawGuest = await SocketRoomGuest.ConnectAsync(
                _announcement,
                _localDevice.DeviceName,
                SspRoomTechnology.BuildCapabilities(_localDevice));

            _guest = rawGuest;

            _guest.OnFlashCommand += async (_, cmd) =>
            {
                OnFlashCommand?.Invoke(this, cmd);
                try
                {
                    foreach (var target in _localDevice.Targets)
                        await target.ExecuteAsync(cmd);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SspGuestSession] Flash execute error: {ex.Message}");
                }
            };

            _guest.OnDisconnected += (_, e) => OnHostDisconnected?.Invoke(this, e);
            _guest.OnKicked += (_, e) => OnKicked?.Invoke(this, e);
        }

        public Task StopAsync() => Task.CompletedTask;

        public Task LeaveAsync()
        {
            _guest?.Dispose();
            return Task.CompletedTask;
        }

        public void Dispose() => _guest?.Dispose();
    }
}
