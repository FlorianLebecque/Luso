#nullable enable
using Luso.Features.Rooms.Domain;
using Luso.Features.Rooms.Domain.Commands;
using Luso.Features.Rooms.Domain.Targets;

namespace Luso.Features.Rooms.Services
{
    /// <summary>
    /// Manual trigger task — stays alive until stopped and exposes <see cref="Fire"/>
    /// for on-demand flash commands driven by the host pad UI.
    ///
    /// Unlike <see cref="StrobeTask"/> and <see cref="AudioTask"/> this task does not
    /// run an autonomous loop; it simply holds the <c>TargetKind</c> slot in the
    /// <see cref="ITaskOrchestrator"/> so that switching to another mode stops it
    /// cleanly, and provides a typed entry point for manual pad triggers.
    /// </summary>
    internal sealed class ManualTask : ITask
    {
        private Room? _room;

        public TargetKind Kind { get; }

        public ManualTask(TargetKind kind) => Kind = kind;

        public async Task StartAsync(Room room, CancellationToken cancellationToken)
        {
            _room = room;
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // expected on task stop
            }
            finally
            {
                // Ensure outputs are off when the task is stopped.
                try { await room.FlashAsync(FlashAction.Off, Kind); }
                catch { /* best-effort cleanup */ }
                _room = null;
            }
        }

        /// <summary>
        /// Fires a single flash command. Called from the pad press/release handler.
        /// </summary>
        /// <param name="action">On or Off.</param>
        /// <param name="deviceId">
        /// When non-null, targets a single device via <see cref="Room.FlashDeviceAsync"/>;
        /// when null, broadcasts to all devices via <see cref="Room.FlashAsync"/>.
        /// </param>
        public void Fire(FlashAction action, string? deviceId = null)
        {
            if (_room is not { } room) return;
            if (deviceId is not null)
                _ = room.FlashDeviceAsync(deviceId, action, Kind);
            else
                _ = room.FlashAsync(action, Kind);
        }

        public void Stop() { _room = null; }
    }
}
