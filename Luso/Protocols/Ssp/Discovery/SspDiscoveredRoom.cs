using Luso.Features.Rooms.Domain.Technologies;

namespace Luso.Features.Rooms.Networking.Ssp
{
    /// <summary>
    /// Adapts a <see cref="RoomAnnouncement"/> (SSP ANNC wire type) to the
    /// protocol-agnostic <see cref="IDiscoveredRoom"/> interface.
    /// </summary>
    internal sealed class SspDiscoveredRoom : IDiscoveredRoom
    {
        private readonly RoomAnnouncement _ann;

        public string RoomId => _ann.RoomId;
        public string RoomName => _ann.RoomName;
        public string TechnologyId => SspRoomTechnology.Id;

        /// <summary>Host IP address; needed only by <see cref="SspRoomTechnology.CreateGuestSessionAsync"/>.</summary>
        internal string HostIp => _ann.HostIp;
        internal int TcpPort => _ann.TcpPort;

        public SspDiscoveredRoom(RoomAnnouncement ann) => _ann = ann;

        /// <summary>Convenience: create from individual fields (used when promoting an invite).</summary>
        public static SspDiscoveredRoom FromInvite(string roomId, string roomName, string hostIp, int tcpPort)
            => new(new RoomAnnouncement(roomId, roomName, hostIp, tcpPort));
    }
}
