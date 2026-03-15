#nullable enable
using Luso.Features.Rooms.Domain.Commands;
using Luso.Features.Rooms.Domain.Targets;

namespace Luso.Features.Rooms.Networking.Hue
{
    /// <summary>
    /// <see cref="ITarget"/> backed by a single channel in a
    /// <see cref="HueEntertainmentSession"/> UDP stream.
    ///
    /// ExecuteAsync is synchronous (fire-and-forget into the 50 Hz loop) and
    /// never allocates — the streaming loop holds the frame buffer.
    /// </summary>
    internal sealed class HueEntertainmentTarget : ITarget
    {
        private readonly HueEntertainmentSession _session;
        private readonly int _channelId;

        public string TargetId { get; }
        public string DisplayName { get; }
        public TargetKind Kind => TargetKind.Flashlight;

        internal HueEntertainmentTarget(HueEntertainmentSession session, int channelId, string displayName)
        {
            _session = session;
            _channelId = channelId;
            TargetId = $"hue-ent-{session.ConfigId}-ch{channelId}";
            DisplayName = displayName;
        }

        public Task ExecuteAsync(FlashCommand command)
        {
            _session.SetChannel(_channelId, command.Action == FlashAction.On);
            return Task.CompletedTask;
        }
    }
}
