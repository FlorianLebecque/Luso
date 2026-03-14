#nullable enable
using SyncoStronbo.Features.Rooms.Services;
using SyncoStronbo.Shared.Session;

namespace SyncoStronbo.Features.Rooms.Pages;

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
        room.OnKicked += OnKicked;
        RoomNotifications.SetGuestStatus(room.RoomName, room.HostIp);
    }

    protected override void OnDisappearing() {
        base.OnDisappearing();
        if (RoomSession.Current is { } room) {
            room.OnHostDisconnected -= OnHostDisconnected;
            room.OnKicked -= OnKicked;
        }
    }

    private void OnHostDisconnected(object? sender, EventArgs e) {
        if (_leavingVoluntarily) return;

        MainThread.BeginInvokeOnMainThread(async () => {
            RoomNotifications.Clear();
            await RoomSession.ClearAsync();
            await DisplayAlert("Disconnected", "The host has closed the room.", "OK");
            await Shell.Current.GoToAsync("//Home");
        });
    }

    private async void OnLeaveClicked(object sender, EventArgs e) {
        _leavingVoluntarily = true;
        RoomNotifications.Clear();
        await RoomSession.ClearAsync();
        await Shell.Current.GoToAsync("//Home");
    }

    private void OnKicked(object? sender, EventArgs e) {
        if (_leavingVoluntarily) return;

        MainThread.BeginInvokeOnMainThread(async () => {
            RoomNotifications.Clear();
            await RoomSession.ClearAsync();
            await DisplayAlert("Removed", "You were removed from the room by the host.", "OK");
            await Shell.Current.GoToAsync("//Home");
        });
    }
}
