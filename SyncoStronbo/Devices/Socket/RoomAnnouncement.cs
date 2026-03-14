namespace SyncoStronbo.Devices.Socket {

    /// <summary>
    /// Received from a UDP broadcast by a room host.
    /// </summary>
    internal record RoomAnnouncement(
        string RoomId,
        string RoomName,
        string HostIp,
        int TcpPort
    );
}
