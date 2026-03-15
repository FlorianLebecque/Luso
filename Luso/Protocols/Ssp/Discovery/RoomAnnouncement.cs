namespace Luso.Features.Rooms.Networking.Ssp {
    internal record RoomAnnouncement(
        string RoomId,
        string RoomName,
        string HostIp,
        int TcpPort
    );
}
