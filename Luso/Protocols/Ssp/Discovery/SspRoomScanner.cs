#nullable enable
using Luso.Features.Rooms.Domain.Technologies;

namespace Luso.Features.Rooms.Networking.Ssp
{
    /// <summary>
    /// Guest-side room scanner for the SSP/1.0 protocol.
    ///
    /// Wraps <see cref="UdpRoomDiscovery"/> to provide the protocol-agnostic
    /// <see cref="IRoomScanner"/> contract. Listens for ANNC broadcast datagrams
    /// (room announcements) and INVI unicast datagrams (direct invites).
    ///
    /// Guest presence (PRES) broadcast is also started here so the host can discover
    /// this device before or while scanning for rooms.
    /// </summary>
    internal sealed class SspRoomScanner : IRoomScanner
    {
        private readonly UdpRoomDiscovery _udp = new();
        private readonly string _guestId;
        private readonly string _guestName;
        private bool _started;

        public event EventHandler<IDiscoveredRoom>? OnRoomDiscovered;
        public event EventHandler<IRoomInvite>? OnInviteReceived;

        public SspRoomScanner(string guestId, string guestName)
        {
            _guestId = guestId;
            _guestName = guestName;

            _udp.OnRoomDiscovered += (_, ann) => OnRoomDiscovered?.Invoke(this, new SspDiscoveredRoom(ann));
            _udp.OnInviteReceived += (_, invite) => OnInviteReceived?.Invoke(this, new SspRoomInvite(invite));
        }

        public Task StartAsync()
        {
            if (_started) return Task.CompletedTask;
            _started = true;
            _udp.StartListening();
            _udp.StartGuestPresence(_guestId, _guestName);
            return Task.CompletedTask;
        }

        public void Stop()
        {
            _udp.StopListening();
        }

        public Task RefuseInviteAsync(IRoomInvite invite, string reason)
        {
            if (invite is not SspRoomInvite ssp)
                return Task.CompletedTask;
            return _udp.SendInviteRefusalAsync(ssp.InviteId, _guestId, reason, ssp.HostIp);
        }

        public void Dispose() => _udp.Dispose();
    }
}
