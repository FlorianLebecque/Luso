#nullable enable
using Zeroconf;

namespace Luso.Features.Rooms.Networking.Ssp
{
    /// <summary>
    /// SSP discovery using mDNS instead of UDP broadcast.
    /// Host registers services, guests browse for them.
    /// Solves hotspot/isolated-network issues with broadcast.
    /// </summary>
    internal sealed class MdnsRoomDiscovery : IDisposable
    {
        private const string RoomServiceType = "_luso-room._tcp.local.";
        private const string GuestServiceType = "_luso-guest._tcp.local.";

        private IZeroconfHost? _registeredRoom;
        private IZeroconfHost? _registeredGuest;
        private CancellationTokenSource? _browseCts;

        public event EventHandler<RoomAnnouncement>? OnRoomDiscovered;
        public event EventHandler<GuestPresenceAnnouncement>? OnGuestPresenceDiscovered;
        public event EventHandler<RoomInvite>? OnInviteReceived;
        public event EventHandler<InviteRefusal>? OnInviteRefused;

        /// <summary>Host registers the room as an mDNS service.</summary>
        public async Task StartAnnouncingAsync(string roomId, string roomName, int tcpPort)
        {
            StopAnnouncing();

            var properties = new Dictionary<string, string>
            {
                ["id"] = roomId,
                ["nm"] = roomName,
            };

            try
            {
                _registeredRoom = await ZeroconfRegistrar.RegisterServiceAsync(
                    new ZeroconfRegistration
                    {
                        DisplayName = roomName,
                        Port = tcpPort,
                        ServiceType = RoomServiceType,
                        Name = $"{roomName}-{Guid.NewGuid():N}",
                        Properties = properties
                    });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MdnsRoomDiscovery] Register room error: {ex.Message}");
            }
        }

        public void StopAnnouncing()
        {
            if (_registeredRoom is not null)
            {
                try { _registeredRoom.Dispose(); }
                catch { }
                _registeredRoom = null;
            }
        }

        /// <summary>Guest registers presence as an mDNS service.</summary>
        public async Task StartGuestPresenceAsync(string guestId, string guestName)
        {
            StopGuestPresence();

            var properties = new Dictionary<string, string>
            {
                ["gid"] = guestId,
                ["nm"] = guestName,
            };

            try
            {
                _registeredGuest = await ZeroconfRegistrar.RegisterServiceAsync(
                    new ZeroconfRegistration
                    {
                        DisplayName = guestName,
                        Port = 0,
                        ServiceType = GuestServiceType,
                        Name = $"{guestName}-{Guid.NewGuid():N}",
                        Properties = properties
                    });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MdnsRoomDiscovery] Register guest error: {ex.Message}");
            }
        }

        public void StopGuestPresence()
        {
            if (_registeredGuest is not null)
            {
                try { _registeredGuest.Dispose(); }
                catch { }
                _registeredGuest = null;
            }
        }

        /// <summary>Guest browses for room services.</summary>
        public async Task StartListeningAsync(int browseTimeoutMs = 5000)
        {
            StopListening();

            _browseCts = new CancellationTokenSource();
            var token = _browseCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    var results = await ZeroconfResolver.ResolveAsync(
                        RoomServiceType,
                        TimeSpan.FromMilliseconds(browseTimeoutMs),
                        null,
                        token);

                    foreach (var service in results)
                    {
                        try
                        {
                            if (service.Properties.TryGetValue("id", out var idList) && idList.Count > 0 &&
                                service.Properties.TryGetValue("nm", out var nmList) && nmList.Count > 0)
                            {
                                string roomId = idList[0];
                                string roomName = nmList[0];
                                string hostIp = service.IPAddresses?.FirstOrDefault() ?? string.Empty;
                                int tcpPort = service.Port;

                                if (!string.IsNullOrEmpty(hostIp))
                                {
                                    var announcement = new RoomAnnouncement(roomId, roomName, hostIp, tcpPort);
                                    OnRoomDiscovered?.Invoke(this, announcement);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[MdnsRoomDiscovery] Parse service error: {ex.Message}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MdnsRoomDiscovery] Browse error: {ex.Message}");
                }
            }, token);
        }

        public void StopListening()
        {
            if (_browseCts is not null)
            {
                try { _browseCts.Cancel(); }
                catch { }
                _browseCts = null;
            }
        }

        /// <summary>Host sends invite (for now, fire-and-forget; full implementation uses presence service).</summary>
        public async Task SendInviteAsync(RoomInvite invite, string guestIp)
        {
            // Future: use mDNS TXT records or TCP to deliver invite more robustly.
            // For now, just log.
            System.Diagnostics.Debug.WriteLine($"[MdnsRoomDiscovery] SendInvite to {guestIp}: {invite.RoomId}");
            await Task.CompletedTask;
        }

        public async Task SendInviteRefusalAsync(string inviteId, string guestId, string reason, string hostIp)
        {
            System.Diagnostics.Debug.WriteLine($"[MdnsRoomDiscovery] SendInviteRefusal: {inviteId}");
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            StopAnnouncing();
            StopGuestPresence();
            StopListening();
        }
    }
}
