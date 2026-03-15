using Luso.Features.Rooms.Domain.Commands;

namespace Luso.Features.Rooms.Domain.Targets
{
    /// <summary>
    /// A full-color light target. For a phone this is the full-screen RGB strobe;
    /// for a Hue bridge this is an individual smart bulb.
    ///
    /// Screen strobe rendering is handled at the UI layer (HostRoomPage drives
    /// the background colour). ExecuteAsync is a no-op here; override in a
    /// platform-specific subclass when direct control is required.
    /// </summary>
    internal sealed record ScreenTarget(
        string TargetId,
        string DisplayName,
        int PixelWidth = 0,
        int PixelHeight = 0
    ) : ITarget
    {
        public TargetKind Kind => TargetKind.Screen;

        /// <summary>True when this target has a physical pixel grid (e.g. a phone screen).</summary>
        public bool HasResolution => PixelWidth > 0 && PixelHeight > 0;

        /// <summary>Convenience factory for a phone screen strobe target.</summary>
        public static ScreenTarget Screen(int width, int height)
            => new("screen", "Screen", width, height);

        /// <summary>Convenience factory for a single ambient bulb (no pixel resolution).</summary>
        public static ScreenTarget Bulb(string id, string name)
            => new(id, name);

        /// <summary>Screen strobe is rendered by the UI layer — no-op here.</summary>
        public Task ExecuteAsync(FlashCommand command) => Task.CompletedTask;
    }
}
