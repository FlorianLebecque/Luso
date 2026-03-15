namespace Luso.Features.Rooms.Domain.Targets
{
    /// <summary>
    /// Discriminated set of output kinds that a target can represent.
    /// Each kind implies a specific command vocabulary.
    /// </summary>
    internal enum TargetKind
    {
        /// <summary>Binary on/off torch/flashlight output.</summary>
        Flashlight,

        /// <summary>Full-color light output (device screen strobe, smart RGB bulb, etc.).</summary>
        Screen,

        /// <summary>Haptic / vibration motor output.</summary>
        Vibration,
    }
}
