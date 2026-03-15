#nullable enable
using Luso.Features.Rooms.Domain;
using Luso.Features.Rooms.Domain.Commands;
using Luso.Features.Rooms.Domain.Targets;

namespace Luso.Features.Rooms.Services
{
    /// <summary>
    /// Strobe task — toggles output at a fixed frequency.
    ///
    /// All output (including the local flashlight) routes through
    /// <see cref="Room.FlashAsync"/> → <c>IDevice.Targets</c> → <c>ITarget.ExecuteAsync</c>.
    /// No direct <c>LightController</c> or <c>Flashlight.Default</c> calls.
    /// </summary>
    internal sealed class StrobeTask : ITask
    {
        private readonly double _frequencyHz;

        public TargetKind Kind { get; }

        public StrobeTask(TargetKind kind, double frequencyHz)
        {
            Kind = kind;
            _frequencyHz = frequencyHz;
        }

        public async Task StartAsync(Room room, CancellationToken cancellationToken)
        {
            var halfPeriodMs = HalfPeriod(_frequencyHz);

            try
            {
                bool state = false;
                while (!cancellationToken.IsCancellationRequested)
                {
                    state = !state;
                    await room.FlashAsync(state ? FlashAction.On : FlashAction.Off, Kind);
                    await Task.Delay((int)halfPeriodMs, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // expected on task stop
            }
            finally
            {
                await room.FlashAsync(FlashAction.Off, Kind);
            }
        }

        public void Stop() { }

        private static long HalfPeriod(double frequencyHz) => (long)(1000.0 / (frequencyHz * 2));
    }
}
