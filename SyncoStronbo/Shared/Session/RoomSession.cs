#nullable enable
using SyncoStronbo.Features.Rooms.Domain;

namespace SyncoStronbo.Shared.Session
{
    /// <summary>
    /// Lightweight static holder for the currently active <see cref="Room"/>.
    /// Shared across pages to avoid serialising objects through Shell query parameters.
    /// </summary>
    internal static class RoomSession
    {
        public static Room? Current { get; private set; }

        public static void Set(Room room)
        {
            Current?.Dispose();
            Current = room;
        }

        public static void Clear()
        {
            Current?.Dispose();
            Current = null;
        }
    }
}
