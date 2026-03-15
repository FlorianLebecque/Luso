#nullable enable
using Luso.Features.Rooms.Domain;

namespace Luso.Shared.Session
{
    /// <summary>
    /// Injectable singleton that holds the currently active <see cref="Room"/>.
    /// Replaces the old static <c>RoomSession</c> class so pages can declare an
    /// explicit dependency via <see cref="IRoomSessionStore"/>.
    /// </summary>
    internal sealed class RoomSessionStore : IRoomSessionStore
    {
        private readonly object _sync = new();
        public Room? Current { get; private set; }

        public void Set(Room room)
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

        public void Clear()
        {
            Room? previous;
            lock (_sync)
            {
                previous = Current;
                Current = null;
            }

            previous?.Dispose();
        }

        public Task ClearAsync()
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
