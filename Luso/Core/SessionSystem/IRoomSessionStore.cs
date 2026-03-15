#nullable enable
using Luso.Features.Rooms.Domain;

namespace Luso.Shared.Session
{
    /// <summary>
    /// Abstracts the single active <see cref="Room"/> slot so pages can have it injected
    /// rather than reaching into a static class.
    /// </summary>
    internal interface IRoomSessionStore
    {
        Room? Current { get; }

        void Set(Room room);
        void Clear();
        Task ClearAsync();
    }
}
