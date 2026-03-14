#nullable enable
using System.Collections.ObjectModel;
using SyncoStronbo.Devices.Socket;

namespace SyncoStronbo.Pages;

public partial class RoomPage : ContentPage {

    private readonly ObservableCollection<GuestInfo> _guestInfos = new();
    private bool _leavingVoluntarily;

    public RoomPage() {
        InitializeComponent();
        guestList.ItemsSource = _guestInfos;
    }

    // ── Page lifecycle ──────────────────────────────

    protected override void OnAppearing() {
        base.OnAppearing();
        _leavingVoluntarily = false;

        var room = RoomSession.Current;
        if (room is null) { Shell.Current.GoToAsync("//Home"); return; }

        lblRoomName.Text         = room.RoomName;
        lblRole.Text             = room.IsHost ? "You are the Host" : "You are a Guest";
        btnFlash.IsVisible       = room.IsHost;
        hostPanel.IsVisible      = room.IsHost;
        lblGuestStatus.IsVisible = !room.IsHost;

        room.OnHostDisconnected  += OnHostDisconnected;
        room.OnGuestConnected    += OnGuestConnected;
        room.OnGuestDisconnected += OnGuestDisconnected;
        room.OnGuestPingUpdated  += OnGuestPingUpdated;

        if (room.IsHost) {
            foreach (var (ip, rtt) in room.GetGuests())
                GetOrAdd(ip).RttMs = rtt;
            RefreshNoGuestsLabel();
        }
    }

    protected override void OnDisappearing() {
        base.OnDisappearing();

        if (RoomSession.Current is { } room) {
            room.OnHostDisconnected  -= OnHostDisconnected;
            room.OnGuestConnected    -= OnGuestConnected;
            room.OnGuestDisconnected -= OnGuestDisconnected;
            room.OnGuestPingUpdated  -= OnGuestPingUpdated;
        }
        _guestInfos.Clear();
    }

    // ── Room event handlers ───────────────────────────

    private void OnHostDisconnected(object? sender, EventArgs e) {
        if (_leavingVoluntarily) return;
        MainThread.BeginInvokeOnMainThread(async () => {
            RoomSession.Clear();
            await DisplayAlert("Disconnected", "The host has closed the room.", "OK");
            await Shell.Current.GoToAsync("//Home");
        });
    }

    private void OnGuestConnected(object? sender, string ip) {
        MainThread.BeginInvokeOnMainThread(() => { GetOrAdd(ip); RefreshNoGuestsLabel(); });
    }

    private void OnGuestDisconnected(object? sender, string ip) {
        MainThread.BeginInvokeOnMainThread(() => {
            var g = _guestInfos.FirstOrDefault(x => x.Ip == ip);
            if (g is not null) _guestInfos.Remove(g);
            RefreshNoGuestsLabel();
        });
    }

    private void OnGuestPingUpdated(object? sender, GuestPingArgs args) {
        MainThread.BeginInvokeOnMainThread(() => GetOrAdd(args.Ip).RttMs = args.RttMs);
    }

    // ── Helpers ─────────────────────────────────────

    private GuestInfo GetOrAdd(string ip) {
        var g = _guestInfos.FirstOrDefault(x => x.Ip == ip);
        if (g is not null) return g;
        var info = new GuestInfo { Ip = ip };
        _guestInfos.Add(info);
        return info;
    }

    private void RefreshNoGuestsLabel() {
        lblNoGuests.IsVisible = _guestInfos.Count == 0;
    }

    // ── Button handlers ───────────────────────────────

    private async void OnLeaveClicked(object sender, EventArgs e) {
        _leavingVoluntarily = true;
        RoomSession.Clear();
        await Shell.Current.GoToAsync("//Home");
    }

    private async void OnFlashClicked(object sender, EventArgs e) {
        if (RoomSession.Current is { } room) {
            try { await room.FlashAsync("on"); }
            catch (Exception ex) { await DisplayAlert("Flash error", ex.Message, "OK"); }
        }
    }
}
