#nullable enable
using Luso.Features.Rooms.Domain;
using Luso.Features.Rooms.Domain.Devices;
using Luso.Features.Rooms.Domain.Technologies;
using Luso.Infrastructure;

namespace Luso.Features.Rooms
{
    /// <summary>
    /// Injectable factory that creates <see cref="Room"/> instances.
    /// Implements <see cref="IRoomFactory"/> so pages depend on the abstraction.
    ///
    /// <c>Room</c> itself has no registry knowledge; <c>RoomFactory</c> creates the
    /// correct sessions for every registered technology and injects them into the room.
    /// </summary>
    internal sealed class RoomFactory : IRoomFactory
    {
        private readonly IRoomTechnologyCatalog _catalog;

        public RoomFactory(IRoomTechnologyCatalog catalog)
        {
            _catalog = catalog;
        }

        /// <summary>
        /// Creates a new host room.
        /// One <see cref="IRoomHostSession"/> and one <see cref="IInviteSession"/> are
        /// started for every registered technology so guests from any protocol can join.
        /// </summary>
        public Room Create(string roomName)
        {
            var roomId = Guid.NewGuid().ToString("N")[..8];
            var localDevice = LocalDevice.Detect();
            var room = new Room(roomId, roomName, isHost: true, localDevice);

            foreach (var tech in _catalog.GetAll())
            {
                var hostSession = tech.CreateHostSession(room);
                if (hostSession is not null)
                    room.AddHostSession(hostSession);

                var invite = tech.CreateInviteSession(room);
                if (invite is not null)
                {
                    room.AddInviteSession(tech.TechnologyId, invite);
                }

                var announcer = tech.CreateAnnouncer(room);
                if (announcer is not null)
                {
                    room.AddAnnouncer(announcer);
                }
            }

            return room;
        }

        /// <summary>
        /// Joins an existing room described by <paramref name="discovered"/>.
        /// The correct technology is resolved from <see cref="IDiscoveredRoom.TechnologyId"/>.
        /// </summary>
        public async Task<Room> JoinAsync(IDiscoveredRoom discovered)
        {
            var tech = _catalog.Get(discovered.TechnologyId);
            var localDevice = LocalDevice.Detect();
            var room = new Room(discovered.RoomId, discovered.RoomName, isHost: false, localDevice, discovered);

            var guest = tech.CreateGuestSession(room);
            if (guest is null)
                throw new InvalidOperationException($"Technology '{tech.TechnologyId}' does not support guest joins.");

            room.SetGuestSession(guest);
            await room.StartAsync();

            return room;
        }
    }
}
