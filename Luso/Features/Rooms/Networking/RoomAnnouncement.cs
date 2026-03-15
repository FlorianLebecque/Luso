namespace Luso.Features.Rooms.Networking {
    internal record RoomAnnouncement(
        string RoomId,
        string RoomName,
        string HostIp,
        int TcpPort
    );
}
