#nullable enable
using Luso.Features.Rooms.Domain;
using Luso.Features.Rooms.Domain.Technologies;

namespace Luso.Features.Rooms
{
    /// <summary>
    /// Creates <see cref="Room"/> instances for hosting or joining.
    /// Backed by <see cref="RoomFactory"/>.
    /// </summary>
    internal interface IRoomFactory
    {
        Room Create(string roomName);
        Task<Room> JoinAsync(IDiscoveredRoom discovered);
    }
}
