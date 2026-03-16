#nullable enable
using Luso.Audio;
using Luso.Features.Rooms.Domain;
using Luso.Features.Rooms.Domain.Commands;
using Luso.Features.Rooms.Domain.Targets;

namespace Luso.Features.Rooms.Services
{
    /// <summary>
    /// Audio-driven task — analyses microphone input and dispatches flash commands
    /// when the low-frequency level exceeds the threshold.
    ///
    /// All output (including the local flashlight) routes through
    /// <see cref="Room.FlashAsync"/> → <c>IDevice.Targets</c> → <c>ITarget.ExecuteAsync</c>.
    /// No direct <c>Flashlight.Default</c> calls.
    /// </summary>
    internal sealed class AudioTask : ITask
    {
        private readonly double _threshold;
        private readonly IAudioAnalyser _audioAnalyser;

        public TargetKind Kind { get; }

        public AudioTask(TargetKind kind, double threshold = 0.5)
        {
            Kind = kind;
            _threshold = threshold;
            _audioAnalyser = new AudioAnalyser();
        }

        public async Task StartAsync(Room room, CancellationToken cancellationToken)
        {
            await _audioAnalyser.InitAsync();

            // If permission was denied, bail out silently rather than looping on a null recorder.
            if (!_audioAnalyser.IsReady)
                return;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var level = _audioAnalyser.GetLowLevel();
                    var action = level > _threshold ? FlashAction.On : FlashAction.Off;

                    await room.FlashAsync(action, Kind);
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
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
    }
}
