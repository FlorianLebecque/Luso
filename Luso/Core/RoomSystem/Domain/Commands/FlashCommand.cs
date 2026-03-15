namespace Luso.Features.Rooms.Domain.Commands
{
    /// <summary>
    /// A synchronized output command instructing devices to toggle a flash output.
    ///
    /// <see cref="AtUnixMs"/> carries the absolute UTC timestamp at which all devices
    /// should execute the command simultaneously, compensated for the lead time built
    /// into the host dispatch logic.
    /// </summary>
    internal record FlashCommand(
        /// <summary>The flash action to perform.</summary>
        FlashAction Action,
        /// <summary>Absolute Unix epoch in milliseconds at which to execute.</summary>
        long AtUnixMs
    );
}
