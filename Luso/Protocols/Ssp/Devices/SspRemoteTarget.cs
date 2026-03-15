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
    internal sealed class SspRemoteTarget : ITarget
    {
        private readonly Func<FlashCommand, Task> _execute;

        public string TargetId { get; }
        public TargetKind Kind { get; }
        public string DisplayName { get; }

        public SspRemoteTarget(
            string targetId,
            TargetKind kind,
            string displayName,
            Func<FlashCommand, Task> execute)
        {
            TargetId = targetId;
            Kind = kind;
            DisplayName = displayName;
            _execute = execute;
        }

        public Task ExecuteAsync(FlashCommand command)
            => _execute(command);
    }
}
