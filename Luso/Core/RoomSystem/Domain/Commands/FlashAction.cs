#nullable enable

namespace Luso.Features.Rooms.Domain.Commands
{
    /// <summary>
    /// Typed flash action, replacing the "on" / "off" magic strings in the domain layer.
    /// Mapping to/from wire strings happens only at the SSP protocol boundary
    /// (<c>SocketRoomGuest</c> on receive, <c>SspHostSession</c> delegate on send).
    /// </summary>
    internal enum FlashAction
    {
        On = 0,
        Off = 1,
    }
}
