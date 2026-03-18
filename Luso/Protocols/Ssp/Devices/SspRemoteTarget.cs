#nullable enable
using Luso.Features.Rooms.Domain.Commands;
using Luso.Features.Rooms.Domain.Targets;

namespace Luso.Features.Rooms.Networking.Ssp
{
    /// <summary>
    /// A target that sends commands to a remote SSP device via a delegate.
    ///
    /// Created by <see cref="SspDevice"/> for each capability declared in the
    /// JOIN handshake. The delegate is bound to
    /// <c>SocketRoomHost.FlashGuestAsync(ip, action)</c> so command dispatch
    /// goes from domain → target → SSP socket, with no protocol knowledge
    /// leaking above this layer.
    /// </summary>
    internal sealed class SspRemoteTarget : ITarget, IStrobeCapableTarget
    {
        private readonly Func<TargetKind, FlashCommand, Task> _execute;
        private readonly Func<TargetKind, long, int, int, double, Task> _startStrobe;
        private readonly Func<TargetKind, Task> _stopStrobe;

        public string TargetId { get; }
        public TargetKind Kind { get; }
        public string DisplayName { get; }

        public SspRemoteTarget(
            string targetId,
            TargetKind kind,
            string displayName,
            Func<TargetKind, FlashCommand, Task> execute,
            Func<TargetKind, long, int, int, double, Task> startStrobe,
            Func<TargetKind, Task> stopStrobe)
        {
            TargetId = targetId;
            Kind = kind;
            DisplayName = displayName;
            _execute = execute;
            _startStrobe = startStrobe;
            _stopStrobe = stopStrobe;
        }

        public Task ExecuteAsync(FlashCommand command)
            => _execute(Kind, command);

        public Task StartStrobeAsync(long atUnixMs, int onMs, int offMs, double frequencyHz)
            => _startStrobe(Kind, atUnixMs, onMs, offMs, frequencyHz);

        public Task StopStrobeAsync()
            => _stopStrobe(Kind);
    }
}
