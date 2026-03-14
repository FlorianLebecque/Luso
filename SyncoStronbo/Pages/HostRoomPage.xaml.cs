#nullable enable
using System.Collections.ObjectModel;
using SyncoStronbo.Devices.Socket;
using SyncoStronbo.Services;

namespace SyncoStronbo.Pages;

public partial class HostRoomPage : ContentPage {

    private readonly ObservableCollection<GuestInfo> _guestInfos = new();

    public HostRoomPage() {
        InitializeComponent();
        guestList.ItemsSource = _guestInfos;
    }

    protected override void OnAppearing() {
        base.OnAppearing();

        var room = RoomSession.Current;
        if (room is null || !room.IsHost) { Shell.Current.GoToAsync("//Home"); return; }

        lblRoomName.Text = room.RoomName;

        room.OnGuestConnected += OnGuestConnected;
        room.OnGuestDisconnected += OnGuestDisconnected;
        room.OnGuestPingUpdated += OnGuestPingUpdated;

        foreach (var (ip, rtt) in room.GetGuests())
            GetOrAdd(ip).RttMs = rtt;

        RefreshNoGuestsLabel();
        RoomNotifications.SetHostStatus(room.RoomName, _guestInfos.Count);
    }

    protected override void OnDisappearing() {
        base.OnDisappearing();

        if (RoomSession.Current is { } room) {
            room.OnGuestConnected -= OnGuestConnected;
            room.OnGuestDisconnected -= OnGuestDisconnected;
            room.OnGuestPingUpdated -= OnGuestPingUpdated;
        }
        _guestInfos.Clear();
    }

    private void OnGuestConnected(object? sender, string ip) {
        MainThread.BeginInvokeOnMainThread(() => {
            GetOrAdd(ip);
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

    private async void OnLeaveClicked(object sender, EventArgs e) {
        RoomNotifications.Clear();
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
