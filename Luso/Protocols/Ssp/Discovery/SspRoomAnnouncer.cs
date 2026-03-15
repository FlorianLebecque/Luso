#nullable enable
using Luso.Features.Rooms.Domain.Technologies;

namespace Luso.Features.Rooms.Networking.Ssp
{
    /// <summary>
    /// SSP/1.0 room announcer — broadcasts ANNC datagrams on UDP.
    ///
    /// Extracted from <see cref="SspHostSession"/> so the announcer lifecycle
    /// is managed independently by <c>Room.AddAnnouncer</c> / <c>Room.Dispose</c>,
    /// matching the contract in <see cref="IRoomAnnouncer"/>.
    /// </summary>
    internal sealed class SspRoomAnnouncer : IRoomAnnouncer
    {
        private readonly UdpRoomDiscovery _udp = new();
        private readonly string _roomId;
        private readonly string _roomName;
        private readonly int _tcpPort;
        private bool _started;

        public SspRoomAnnouncer(string roomId, string roomName, int tcpPort)
        {
            _roomId = roomId;
            _roomName = roomName;
            _tcpPort = tcpPort;
        }

        public Task StartAsync()
        {
            if (_started) return Task.CompletedTask;
            _started = true;
            _udp.StartAnnouncing(_roomName, _roomId, _tcpPort);
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            _started = false;
            _udp.StopAnnouncing();
            return Task.CompletedTask;
        }

        public void Dispose() => _udp.Dispose();
    }
}
