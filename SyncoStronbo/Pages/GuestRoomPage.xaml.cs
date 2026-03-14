#nullable enable
using SyncoStronbo.Services;

namespace SyncoStronbo.Pages;

public partial class GuestRoomPage : ContentPage {

    private bool _leavingVoluntarily;

    public GuestRoomPage() {
        InitializeComponent();
    }

    protected override void OnAppearing() {
        base.OnAppearing();
        _leavingVoluntarily = false;

        var room = RoomSession.Current;
        if (room is null || room.IsHost) { Shell.Current.GoToAsync("//Home"); return; }

        lblRoomName.Text = room.RoomName;
        lblHost.Text = $"Host: {room.HostIp}";

        room.OnHostDisconnected += OnHostDisconnected;
        RoomNotifications.SetGuestStatus(room.RoomName, room.HostIp);
    }

    protected override void OnDisappearing() {
        base.OnDisappearing();
        if (RoomSession.Current is { } room) room.OnHostDisconnected -= OnHostDisconnected;
    }

    private void OnHostDisconnected(object? sender, EventArgs e) {
        if (_leavingVoluntarily) return;

        MainThread.BeginInvokeOnMainThread(async () => {
            RoomNotifications.Clear();
            RoomSession.Clear();
            await DisplayAlert("Disconnected", "The host has closed the room.", "OK");
            await Shell.Current.GoToAsync("//Home");
        });
    }

    private async void OnLeaveClicked(object sender, EventArgs e) {
        _leavingVoluntarily = true;
        RoomNotifications.Clear();
        RoomSession.Clear();
        await Shell.Current.GoToAsync("//Home");
    }
}
