#nullable enable
using Luso.Features.Rooms.Domain.Technologies;

namespace Luso.Features.Rooms.Networking.Ssp
{
    /// <summary>
    /// Host-side invite session for the SSP/1.0 protocol.
    ///
    /// Listens for PRES (guest presence) and INVR (invite refusal) datagrams
    /// and can send INVI (invite) unicast datagrams to discovered devices.
    /// </summary>
    internal sealed class SspInviteSession : IInviteSession
    {
        private readonly UdpRoomDiscovery _udp = new();
        private readonly string _roomId;
        private readonly string _roomName;
        private bool _started;

        public event EventHandler<IDiscoveredDevice>? OnDevicePresenceDiscovered;
        public event EventHandler<string>? OnInviteRefused;   // inviteId

        public SspInviteSession(string roomId, string roomName)
        {
            _roomId = roomId;
            _roomName = roomName;

            _udp.OnGuestPresenceDiscovered += (_, pres) =>
            {
                if (pres.Available)
                    OnDevicePresenceDiscovered?.Invoke(this, new SspDiscoveredDevice(pres));
            };
            _udp.OnInviteRefused += (_, refusal) =>
                OnInviteRefused?.Invoke(this, refusal.InviteId);
        }

        public void Start()
        {
            if (_started) return;
            _started = true;
            _udp.StartListening();
        }

        public void Stop() => _udp.StopListening();

        public Task SendInviteAsync(IDiscoveredDevice device, string roomId, string roomName)
        {
            if (device is not SspDiscoveredDevice ssp)
                return Task.CompletedTask;

            var invite = new RoomInvite(
                InviteId: Guid.NewGuid().ToString("N"),
                RoomId: roomId,
                RoomName: roomName,
                HostIp: GuestIdentity.LocalIpv4(),
                TcpPort: SocketRoomHost.TcpPort,
                ProtocolVersion: SspCbor.ProtocolVersion
            );
            return _udp.SendInviteAsync(invite, ssp.GuestIp);
        }

        public void Dispose() => _udp.Dispose();
    }
}
