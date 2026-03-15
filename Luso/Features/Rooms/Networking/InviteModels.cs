namespace Luso.Features.Rooms.Networking {
    internal record GuestPresenceAnnouncement(
        string GuestId,
        string GuestName,
        string GuestIp,
        string ProtocolVersion,
        bool Available
    );

    internal record RoomInvite(
        string InviteId,
        string RoomId,
        string RoomName,
        string HostIp,
        int TcpPort,
        string ProtocolVersion
    );

    internal record InviteRefusal(
        string InviteId,
        string GuestId,
        string Reason,
        string FromIp
    );
}
