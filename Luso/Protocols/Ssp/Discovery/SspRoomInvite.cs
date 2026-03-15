using Luso.Features.Rooms.Domain.Technologies;

namespace Luso.Features.Rooms.Networking.Ssp
{
    /// <summary>
    /// Adapts a <see cref="RoomInvite"/> (SSP INVI wire type) to the
    /// protocol-agnostic <see cref="IRoomInvite"/> interface.
    ///
    /// Because <see cref="IRoomInvite"/> extends <see cref="IDiscoveredRoom"/>,
    /// it can be passed directly to <c>Room.JoinAsync(IDiscoveredRoom)</c>.
    /// </summary>
    internal sealed class SspRoomInvite : IRoomInvite
    {
        private readonly RoomInvite _invite;

        public string InviteId => _invite.InviteId;
        public string RoomId => _invite.RoomId;
        public string RoomName => _invite.RoomName;
        public string TechnologyId => SspRoomTechnology.Id;

        /// <summary>True when the host's protocol version matches ours.</summary>
        public bool IsCompatible
            => string.Equals(_invite.ProtocolVersion, SspCbor.ProtocolVersion, StringComparison.Ordinal);

        /// <summary>Exposed internally so <see cref="SspRoomTechnology.CreateGuestSessionAsync"/> can connect.</summary>
        internal RoomAnnouncement AsAnnouncement()
            => new(_invite.RoomId, _invite.RoomName, _invite.HostIp, _invite.TcpPort);

        /// <summary>Host IP needed to send a refusal UDP datagram.</summary>
        internal string HostIp => _invite.HostIp;

        public SspRoomInvite(RoomInvite invite) => _invite = invite;
    }
}
