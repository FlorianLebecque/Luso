#nullable enable
using System.Collections.ObjectModel;
using Luso.Features.Rooms.Domain;
using Luso.Features.Rooms;
using Luso.Features.Rooms.Domain.Technologies;
using Luso.Features.Rooms.Services;
using Luso.Shared.Session;

namespace Luso.Features.Rooms.Pages;

public partial class BrowseRoomsPage : ContentPage
{
    private readonly ObservableCollection<IDiscoveredRoom> _rooms = new();
    private readonly HashSet<string> _seenIds = new();
    private readonly RoomDiscoveryCoordinator _discovery;
    private readonly IRoomSessionStore _session;
    private readonly IRoomFactory _factory;
    private bool _isJoining;

    public BrowseRoomsPage()
    {
        var sp = IPlatformApplication.Current!.Services;
        _session = sp.GetRequiredService<IRoomSessionStore>();
        _factory = sp.GetRequiredService<IRoomFactory>();
        _discovery = sp.GetRequiredService<RoomDiscoveryCoordinator>();
        InitializeComponent();
        roomsCollection.ItemsSource = _rooms;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _rooms.Clear();
        _seenIds.Clear();

        _discovery.RoomDiscovered += OnRoomDiscovered;
        _discovery.InviteReceived += OnInviteReceived;
        _discovery.StartAsync();

        lblStatus.Text = "Scanning for rooms on this network…";
        spinner.IsRunning = true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _discovery.RoomDiscovered -= OnRoomDiscovered;
        _discovery.InviteReceived -= OnInviteReceived;
        _discovery.Stop();
        _isJoining = false;
    }

    private void OnRoomDiscovered(object? sender, IDiscoveredRoom ann)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_seenIds.Add(ann.RoomId))
                _rooms.Add(ann);
        });
    }

    private async void OnRoomSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not IDiscoveredRoom discovered) return;
        if (_isJoining) return;

        roomsCollection.SelectedItem = null;
        spinner.IsRunning = true;
        lblStatus.Text = $"Joining '{discovered.RoomName}'…";

        try
        {
            _isJoining = true;
            var room = await _factory.JoinAsync(discovered);
            _session.Set(room);
            await Shell.Current.GoToAsync("GuestRoomPage");
        }
        catch (Exception ex)
        {
            lblStatus.Text = "Scanning for rooms on this network…";
            spinner.IsRunning = true;
            await DisplayAlert("Could not join", ex.Message, "OK");
        }
        finally
        {
            _isJoining = false;
        }
    }

    private void OnInviteReceived(object? sender, IRoomInvite invite)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (_isJoining) return;

            if (!invite.IsCompatible)
            {
                await _discovery.RefuseInviteAsync(invite, InviteRefuseReason.IncompatibleVersion);
                return;
            }

            bool accept = await DisplayAlert(
                "Room Invite",
                $"{invite.RoomName}. Join now?",
                "Join",
                "Refuse");

            if (!accept)
            {
                await _discovery.RefuseInviteAsync(invite, InviteRefuseReason.UserRefused);
                return;
            }

            try
            {
                _isJoining = true;
                spinner.IsRunning = true;
                lblStatus.Text = $"Joining '{invite.RoomName}'…";

                var room = await _factory.JoinAsync(invite);
                _session.Set(room);
                await Shell.Current.GoToAsync("GuestRoomPage");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Invite join failed", ex.Message, "OK");
                lblStatus.Text = "Scanning for rooms on this network…";
            }
            finally
            {
                _isJoining = false;
            }
        });
    }


    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Home");
    }
}
