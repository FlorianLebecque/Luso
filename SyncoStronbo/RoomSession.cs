#nullable enable
namespace SyncoStronbo
{

    /// <summary>
    /// Lightweight static holder for the currently active <see cref="Room"/>.
    /// Shared across pages to avoid serialising objects through Shell query parameters.
    /// </summary>
    internal static class RoomSession
    {

        public static Room? Current { get; private set; }

        /// <summary>Set (and optionally dispose the previous) room.</summary>
        public static void Set(Room room)
        {
            Current?.Dispose();
            Current = room;
        }

        /// <summary>Leave the current room and dispose it.</summary>
        public static void Clear()
        {
            Current?.Dispose();
            Current = null;
        }
    }
}
