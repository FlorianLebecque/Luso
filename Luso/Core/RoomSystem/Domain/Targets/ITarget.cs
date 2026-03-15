namespace Luso.Features.Rooms.Domain.Targets
{
    using Luso.Features.Rooms.Domain.Commands;
    /// <summary>
    /// An individual controllable output endpoint on a device.
    ///
    /// A single device may expose multiple targets (e.g. an SSP phone exposes a
    /// <see cref="FlashlightTarget"/>, a <see cref="ScreenTarget"/>, and a
    /// <see cref="VibrationTarget"/>; a Hue bridge exposes one <see cref="ScreenTarget"/>
    /// per bulb).
    ///
    /// The <see cref="TargetId"/> is unique within the owning device.
    /// Commands are dispatched directly to each target via <see cref="ExecuteAsync"/>;
    /// the caller never needs to know the underlying protocol.
    /// </summary>
    internal interface ITarget
    {
        /// <summary>Identifier unique within the owning device (e.g. "flashlight", "screen", "bulb-1").</summary>
        string TargetId { get; }

        /// <summary>The kind of controllable output this target represents.</summary>
        TargetKind Kind { get; }

        /// <summary>Human-readable label suitable for display in the UI.</summary>
        string DisplayName { get; }

        /// <summary>
        /// Executes a command on this target.
        /// </summary>
        Task ExecuteAsync(FlashCommand command);
    }
}
