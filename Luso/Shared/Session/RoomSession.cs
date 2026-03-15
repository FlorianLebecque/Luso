#nullable enable
using Luso.Features.Rooms.Domain;

namespace Luso.Shared.Session
{
    /// <summary>
    /// Lightweight static holder for the currently active <see cref="Room"/>.
    /// Shared across pages to avoid serialising objects through Shell query parameters.
    /// </summary>
    internal static class RoomSession
    {
        private static readonly object _sync = new();
        public static Room? Current { get; private set; }

        public static void Set(Room room)
        {
            Room? previous;
            lock (_sync)
            {
                previous = Current;
                Current = room;
            }

            if (previous is not null)
                _ = Task.Run(previous.Dispose);
        }

        public static void Clear()
        {
            Room? previous;
            lock (_sync)
            {
                previous = Current;
                Current = null;
            }

            previous?.Dispose();
        }

        public static Task ClearAsync()
        {
            Room? previous;
            lock (_sync)
            {
                previous = Current;
                Current = null;
            }

            return previous is null ? Task.CompletedTask : Task.Run(previous.Dispose);
        }
    }
}
