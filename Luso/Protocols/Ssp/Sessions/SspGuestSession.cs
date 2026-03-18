#nullable enable
using System.Diagnostics;
using Luso.Features.Rooms.Domain.Commands;
using Luso.Features.Rooms.Domain.Devices;
using Luso.Features.Rooms.Domain.Targets;
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
        private CancellationTokenSource? _flashlightStrobeCts;

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
                    if (cmd.Action == FlashAction.Off)
                        StopLocalFlashlightStrobe();

                    var tasks = _localDevice.Targets
                        .Where(t => t.Kind == TargetKind.Flashlight)
                        .Select(t => t.ExecuteAsync(cmd));
                    await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SspGuestSession] Flash execute error: {ex.Message}");
                }
            };

            _guest.OnScreenCommand += async (_, cmd) =>
            {
                try
                {
                    var tasks = _localDevice.Targets
                        .Where(t => t.Kind == TargetKind.Screen)
                        .Select(t => t.ExecuteAsync(cmd));
                    await Task.WhenAll(tasks);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SspGuestSession] Screen execute error: {ex.Message}");
                }
            };

            _guest.OnStrobeCommand += (_, cmd) =>
            {
                StartLocalFlashlightStrobe(cmd);
            };

            _guest.OnDisconnected += (_, e) => OnHostDisconnected?.Invoke(this, e);
            _guest.OnKicked += (_, e) => OnKicked?.Invoke(this, e);
        }

        public Task StopAsync() => Task.CompletedTask;

        public Task LeaveAsync()
        {
            StopLocalFlashlightStrobe();
            _guest?.Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            StopLocalFlashlightStrobe();
            _guest?.Dispose();
        }

        private void StartLocalFlashlightStrobe(SspStrobeCommand cmd)
        {
            StopLocalFlashlightStrobe();

            var cts = new CancellationTokenSource();
            _flashlightStrobeCts = cts;
            var token = cts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    long initialDelay = cmd.AtUnixMs - now;
                    if (initialDelay > 0)
                        await Task.Delay((int)Math.Min(initialDelay, int.MaxValue), token);

                    int onMs = Math.Max(1, cmd.OnMs);
                    int offMs = Math.Max(1, cmd.OffMs);

                    while (!token.IsCancellationRequested)
                    {
                        long tNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        var on = new FlashCommand(FlashAction.On, tNow);
                        await Task.WhenAll(_localDevice.Targets
                            .Where(t => t.Kind == TargetKind.Flashlight)
                            .Select(t => t.ExecuteAsync(on)));

                        await Task.Delay(onMs, token);

                        tNow = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        var off = new FlashCommand(FlashAction.Off, tNow);
                        await Task.WhenAll(_localDevice.Targets
                            .Where(t => t.Kind == TargetKind.Flashlight)
                            .Select(t => t.ExecuteAsync(off)));

                        await Task.Delay(offMs, token);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SspGuestSession] STRB execute error: {ex.Message}");
                }
            }, token);
        }

        private void StopLocalFlashlightStrobe()
        {
            if (_flashlightStrobeCts is null) return;
            try { _flashlightStrobeCts.Cancel(); }
            catch { }
            _flashlightStrobeCts.Dispose();
            _flashlightStrobeCts = null;
        }
    }
}
