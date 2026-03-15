#nullable enable
using Luso.Features.Rooms.Services;
using Luso.Shared.Session;

namespace Luso.Features.Rooms.Pages;

public partial class GuestRoomPage : ContentPage
{
    private bool _leavingVoluntarily;
    private readonly IRoomSessionStore _session;

    public GuestRoomPage()
    {
        _session = IPlatformApplication.Current!.Services.GetRequiredService<IRoomSessionStore>();
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _leavingVoluntarily = false;

        var room = _session.Current;
        if (room is null || room.IsHost) { Shell.Current.GoToAsync("//Home"); return; }

        lblRoomName.Text = room.RoomName;

        room.OnHostDisconnected += OnHostDisconnected;
        room.OnKicked += OnKicked;
        RoomNotifications.SetGuestStatus(room.RoomName, string.Empty);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_session.Current is { } room)
        {
            room.OnHostDisconnected -= OnHostDisconnected;
            room.OnKicked -= OnKicked;
        }
    }

    // ── Host / kick events ────────────────────────────────────────────────────

    private void OnHostDisconnected(object? sender, EventArgs e)
    {
        if (_leavingVoluntarily) return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            RoomNotifications.Clear();
            await _session.ClearAsync();
            await DisplayAlert("Disconnected", "The host has closed the room.", "OK");
            await Shell.Current.GoToAsync("//Home");
        });
    }

    private async void OnLeaveClicked(object sender, EventArgs e)
    {
        _leavingVoluntarily = true;
        RoomNotifications.Clear();
        await _session.ClearAsync();
        await Shell.Current.GoToAsync("//Home");
    }

    private void OnKicked(object? sender, EventArgs e)
    {
        if (_leavingVoluntarily) return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            RoomNotifications.Clear();
            await _session.ClearAsync();
            await DisplayAlert("Removed", "You were removed from the room by the host.", "OK");
            await Shell.Current.GoToAsync("//Home");
        });
    }
}
