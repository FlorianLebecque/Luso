#nullable enable
using System.Collections.ObjectModel;
using SyncoStronbo.Features.Rooms.Domain;
using SyncoStronbo.Features.Rooms.Networking;
using SyncoStronbo.Features.Rooms.Services;
using SyncoStronbo.Shared.Session;

namespace SyncoStronbo.Features.Rooms.Pages;

public partial class HostRoomPage : ContentPage {
    private readonly ObservableCollection<GuestInfo> _guestInfos = new();
    private readonly ObservableCollection<GuestPresenceAnnouncement> _candidates = new();
    private UdpRoomDiscovery? _inviteDiscovery;

    public HostRoomPage() {
        InitializeComponent();
        guestList.ItemsSource = _guestInfos;
        candidateList.ItemsSource = _candidates;
    }

    protected override void OnAppearing() {
        base.OnAppearing();

        var room = RoomSession.Current;
        if (room is null || !room.IsHost) { Shell.Current.GoToAsync("//Home"); return; }

        lblRoomName.Text = room.RoomName;

        room.OnGuestConnected += OnGuestConnected;
        room.OnGuestDisconnected += OnGuestDisconnected;
        room.OnGuestPingUpdated += OnGuestPingUpdated;

        _inviteDiscovery = new UdpRoomDiscovery();
        _inviteDiscovery.OnGuestPresenceDiscovered += OnGuestPresenceDiscovered;
        _inviteDiscovery.OnInviteRefused += OnInviteRefused;
        _inviteDiscovery.StartListening();

        foreach (var (ip, name, rtt) in room.GetGuests()) {
            var info = GetOrAdd(ip, name);
            info.RttMs = rtt;
        }

        RefreshNoGuestsLabel();
        RefreshNoCandidatesLabel();
        lblInviteStatus.Text = string.Empty;
        RoomNotifications.SetHostStatus(room.RoomName, _guestInfos.Count);
    }

    protected override void OnDisappearing() {
        base.OnDisappearing();

        if (RoomSession.Current is { } room) {
            room.OnGuestConnected -= OnGuestConnected;
            room.OnGuestDisconnected -= OnGuestDisconnected;
            room.OnGuestPingUpdated -= OnGuestPingUpdated;
        }

        if (_inviteDiscovery is not null) {
            _inviteDiscovery.OnGuestPresenceDiscovered -= OnGuestPresenceDiscovered;
            _inviteDiscovery.OnInviteRefused -= OnInviteRefused;
            _inviteDiscovery.Dispose();
            _inviteDiscovery = null;
        }

        _guestInfos.Clear();
        _candidates.Clear();
    }

    private void OnGuestConnected(object? sender, GuestJoinedArgs args) {
        MainThread.BeginInvokeOnMainThread(() => {
            GetOrAdd(args.Ip, args.Name);
            RemoveCandidateByIp(args.Ip);
            RefreshNoGuestsLabel();
            if (RoomSession.Current is { } room) RoomNotifications.SetHostStatus(room.RoomName, _guestInfos.Count);
        });
    }

    private void OnGuestDisconnected(object? sender, string ip) {
        MainThread.BeginInvokeOnMainThread(() => {
            var g = _guestInfos.FirstOrDefault(x => x.Ip == ip);
            if (g is not null) _guestInfos.Remove(g);
            RefreshNoGuestsLabel();
            if (RoomSession.Current is { } room) RoomNotifications.SetHostStatus(room.RoomName, _guestInfos.Count);
        });
    }

    private void OnGuestPingUpdated(object? sender, GuestPingArgs args) {
        MainThread.BeginInvokeOnMainThread(() => GetOrAdd(args.Ip).RttMs = args.RttMs);
    }

    private GuestInfo GetOrAdd(string ip, string name = "") {
        var g = _guestInfos.FirstOrDefault(x => x.Ip == ip);
        if (g is not null) return g;
        var info = new GuestInfo { Ip = ip, Name = name };
        _guestInfos.Add(info);
        return info;
    }

    private void OnGuestPresenceDiscovered(object? sender, GuestPresenceAnnouncement presence) {
        MainThread.BeginInvokeOnMainThread(() => {
            if (!presence.Available) return;
            if (RoomSession.Current is not { IsHost: true }) return;
            if (_guestInfos.Any(g => g.Ip == presence.GuestIp)) {
                RemoveCandidateByIp(presence.GuestIp);
                return;
            }

            var existing = _candidates.FirstOrDefault(c => c.GuestId == presence.GuestId || c.GuestIp == presence.GuestIp);
            if (existing is not null) {
                int idx = _candidates.IndexOf(existing);
                _candidates[idx] = presence;
            } else {
                _candidates.Add(presence);
            }

            RefreshNoCandidatesLabel();
        });
    }

    private void OnInviteRefused(object? sender, InviteRefusal refusal) {
        MainThread.BeginInvokeOnMainThread(() => {
            lblInviteStatus.Text = $"Invite refused by {refusal.GuestId} ({refusal.Reason}).";
        });
    }

    private async void OnInviteClicked(object sender, EventArgs e) {
        if (sender is not Button { BindingContext: GuestPresenceAnnouncement candidate }) return;
        if (RoomSession.Current is not { IsHost: true } room) return;
        if (_inviteDiscovery is null) return;

        try {
            var invite = new RoomInvite(
                InviteId: Guid.NewGuid().ToString("N"),
                RoomId: room.RoomId,
                RoomName: room.RoomName,
                HostIp: GetHostIp(),
                TcpPort: SocketRoomHost.TcpPort,
                ProtocolVersion: SspCbor.ProtocolVersion
            );

            await _inviteDiscovery.SendInviteAsync(invite, candidate.GuestIp);
            lblInviteStatus.Text = $"Invite sent to {candidate.GuestName}.";
        } catch (Exception ex) {
            await DisplayAlert("Invite failed", ex.Message, "OK");
        }
    }

    private void RefreshNoGuestsLabel() {
        lblNoGuests.IsVisible = _guestInfos.Count == 0;
    }

    private void RefreshNoCandidatesLabel() {
        lblNoCandidates.IsVisible = _candidates.Count == 0;
    }

    private void RemoveCandidateByIp(string ip) {
        var candidate = _candidates.FirstOrDefault(c => c.GuestIp == ip);
        if (candidate is not null) {
            _candidates.Remove(candidate);
            RefreshNoCandidatesLabel();
        }
    }

    private static string GetHostIp() {
        return GuestIdentity.LocalIpv4();
    }

    private async void OnLeaveClicked(object sender, EventArgs e) {
        RoomNotifications.Clear();
        await RoomSession.ClearAsync();
        await Shell.Current.GoToAsync("//Home");
    }

    private async void OnFlashClicked(object sender, EventArgs e) {
        if (RoomSession.Current is { } room) {
            try { await room.FlashAsync("on"); }
            catch (Exception ex) { await DisplayAlert("Flash error", ex.Message, "OK"); }
        }
    }

    private async void OnKickClicked(object sender, EventArgs e) {
        if (sender is not Button { BindingContext: GuestInfo guest }) return;
        if (RoomSession.Current is not { IsHost: true } room) return;

        bool confirm = await DisplayAlert("Kick guest", $"Remove {guest.DisplayName} from the room?", "Kick", "Cancel");
        if (!confirm) return;

        try {
            bool kicked = await room.KickGuestAsync(guest.Ip);
            if (!kicked) {
                await DisplayAlert("Kick failed", "Guest is no longer connected.", "OK");
                return;
            }

            var existing = _guestInfos.FirstOrDefault(x => x.Ip == guest.Ip);
            if (existing is not null)
                _guestInfos.Remove(existing);
            RefreshNoGuestsLabel();
            if (RoomSession.Current is { } current) RoomNotifications.SetHostStatus(current.RoomName, _guestInfos.Count);
        } catch (Exception ex) {
            await DisplayAlert("Kick failed", ex.Message, "OK");
        }
    }
}
