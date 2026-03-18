#nullable enable
using System.Diagnostics;
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
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var targets = room.GetDevices()
                .SelectMany(d => d.Targets)
                .Where(t => t.Kind == Kind)
                .ToList();

            if (room.LocalDevice is not null)
                targets.AddRange(room.LocalDevice.Targets.Where(t => t.Kind == Kind));

            var native = targets.OfType<IStrobeCapableTarget>().ToList();
            var fallback = targets.Except(native.Cast<ITarget>()).ToList();

            foreach (var t in native)
                await t.StartStrobeAsync(now, (int)halfPeriodMs, (int)halfPeriodMs, _frequencyHz);

            try
            {
                if (fallback.Count > 0)
                {
                    var stopwatch = Stopwatch.StartNew();
                    long nextTickMs = 0;
                    bool state = false;

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        state = !state;

                        var cmd = new FlashCommand(state ? FlashAction.On : FlashAction.Off,
                            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                        await Task.WhenAll(fallback.Select(t => t.ExecuteAsync(cmd)));

                        nextTickMs += halfPeriodMs;
                        long delayMs = nextTickMs - stopwatch.ElapsedMilliseconds;
                        if (delayMs > 0)
                            await Task.Delay((int)delayMs, cancellationToken).ConfigureAwait(false);
                        else if (delayMs < -halfPeriodMs)
                            nextTickMs = stopwatch.ElapsedMilliseconds;
                    }
                }
                else
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // expected on task stop
            }
            finally
            {
                foreach (var t in native)
                    await t.StopStrobeAsync();

                var offCmd = new FlashCommand(FlashAction.Off, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                await Task.WhenAll(targets.Select(t => t.ExecuteAsync(offCmd)));
            }
        }

        public void Stop() { }

        private static long HalfPeriod(double frequencyHz) => (long)(1000.0 / (frequencyHz * 2));
    }
}
