#nullable enable
using Luso.Features.Rooms.Domain;
using Luso.Features.Rooms;
using Luso.Features.Rooms.Domain.Technologies;
using Luso.Features.Rooms.Services;
using Luso.Shared.Session;

namespace Luso.Features.Home.Pages;

public partial class HomePage : ContentPage
{
    // Room discovery
    private readonly RoomDiscoveryCoordinator _discovery;
    private readonly IRoomSessionStore _session;
    private readonly IRoomFactory _factory;
    private readonly System.Collections.ObjectModel.ObservableCollection<IDiscoveredRoom> _rooms = new();
    private readonly HashSet<string> _seenIds = new();
    private bool _isJoining;

    // Invite handling (direct invite from host)
    private bool _isJoiningInvite;
    private bool _isNavigating;

    public HomePage()
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
        _isNavigating = false;
        _isJoining = false;
        SetNavigationButtonsEnabled(true);

        _rooms.Clear();
        _seenIds.Clear();

        _discovery.RoomDiscovered += OnRoomDiscovered;
        _discovery.InviteReceived += OnInviteReceived;
        _discovery.StartAsync();

        spinner.IsRunning = true;
        lblStatus.Text = "Scanning for rooms…";
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _discovery.RoomDiscovered -= OnRoomDiscovered;
        _discovery.InviteReceived -= OnInviteReceived;
        _discovery.Stop();
    }

    private void OnRoomDiscovered(object? sender, IDiscoveredRoom ann)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_seenIds.Add(ann.RoomId))
            {
                _rooms.Add(ann);
                spinner.IsRunning = false;
                lblStatus.Text = $"{_rooms.Count} room{(_rooms.Count == 1 ? "" : "s")} found";
            }
        });
    }

    private async void OnRoomSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not IDiscoveredRoom discovered) return;
        if (_isJoining) return;

        roomsCollection.SelectedItem = null;
        _isJoining = true;
        spinner.IsRunning = true;
        lblStatus.Text = $"Joining ‘{discovered.RoomName}’…";

        try
        {
            SetNavigationButtonsEnabled(false);
            var room = await _factory.JoinAsync(discovered);
            _session.Set(room);
            await Shell.Current.GoToAsync("GuestRoomPage");
        }
        catch (Exception ex)
        {
            spinner.IsRunning = false;
            lblStatus.Text = "Scanning for rooms…";
            await DisplayAlert("Could not join", ex.Message, "OK");
        }
        finally
        {
            _isJoining = false;
            SetNavigationButtonsEnabled(true);
        }
    }

    private async void btnCreateClicked(object sender, EventArgs e)
    {
        if (_isNavigating) return;
        _isNavigating = true;
        SetNavigationButtonsEnabled(false);
        try
        {
            await Shell.Current.GoToAsync("CreateRoom");
        }
        finally
        {
            if (Shell.Current.CurrentState?.Location?.OriginalString?.Contains("Home") == true)
            {
                _isNavigating = false;
                SetNavigationButtonsEnabled(true);
            }
        }
    }

    private void OnInviteReceived(object? sender, IRoomInvite invite)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (_isJoiningInvite) return;

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
                _isJoiningInvite = true;
                var room = await _factory.JoinAsync(invite);
                _session.Set(room);
                await Shell.Current.GoToAsync("GuestRoomPage");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Invite join failed", ex.Message, "OK");
            }
            finally
            {
                _isJoiningInvite = false;
            }
        });
    }

    private void SetNavigationButtonsEnabled(bool enabled)
    {
        btnCreate.IsEnabled = enabled;
    }
}
