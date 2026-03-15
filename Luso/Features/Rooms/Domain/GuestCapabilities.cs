namespace Luso.Features.Rooms.Domain {
    /// <summary>
    /// SSP/1.0 §4.4 — Capabilities advertised by a Guest in the JOIN message.
    /// </summary>
    internal sealed record GuestCapabilities(
        bool HasFlashlight,
        bool HasVibration,
        bool HasScreen,
        int ScreenWidth,
        int ScreenHeight
    ) {
        public static readonly GuestCapabilities Unknown = new(false, false, false, 0, 0);
    }
}
