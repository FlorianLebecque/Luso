#nullable enable
using SyncoStronbo.Features.Rooms.Domain;
using SyncoStronbo.Features.Rooms.Networking;
using SyncoStronbo.Shared.Session;

namespace SyncoStronbo.Features.Home.Pages;

public partial class HomePage : ContentPage
{
    private readonly string _guestId = GuestIdentity.GetOrCreateGuestId();

    // Room discovery
    private UdpRoomDiscovery? _discovery;
    private readonly System.Collections.ObjectModel.ObservableCollection<RoomAnnouncement> _rooms = new();
    private readonly HashSet<string> _seenIds = new();
    private bool _isJoining;

    // Invite handling (direct invite from host)
    private bool _isJoiningInvite;
    private bool _isNavigating;

    public HomePage()
    {
        InitializeComponent();
        roomsCollection.ItemsSource = _rooms;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _isNavigating = false;
        _isJoining    = false;
        SetNavigationButtonsEnabled(true);

        _rooms.Clear();
        _seenIds.Clear();

        _discovery = new UdpRoomDiscovery();
        _discovery.OnRoomDiscovered  += OnRoomDiscovered;
        _discovery.OnInviteReceived  += OnInviteReceived;
        _discovery.StartListening();
        _discovery.StartGuestPresence(_guestId, GuestIdentity.DeviceName());

        spinner.IsRunning = true;
        lblStatus.Text    = "Scanning for rooms…";
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_discovery is not null)
        {
            _discovery.OnRoomDiscovered -= OnRoomDiscovered;
            _discovery.OnInviteReceived -= OnInviteReceived;
            _discovery.Dispose();
            _discovery = null;
        }
    }

    private void OnRoomDiscovered(object? sender, RoomAnnouncement ann)
    {
        MainThread.BeginInvokeOnMainThread(() => {
            if (_seenIds.Add(ann.RoomId))
            {
                _rooms.Add(ann);
                spinner.IsRunning = false;
                lblStatus.Text    = $"{_rooms.Count} room{(_rooms.Count == 1 ? "" : "s")} found";
            }
        });
    }

    private async void OnRoomSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not RoomAnnouncement ann) return;
        if (_isJoining) return;

        roomsCollection.SelectedItem = null;
        _isJoining = true;
        spinner.IsRunning = true;
        lblStatus.Text    = $"Joining \u2018{ann.RoomName}\u2019…";

        try
        {
            SetNavigationButtonsEnabled(false);
            var room = await Room.JoinAsync(ann);
            RoomSession.Set(room);
            await Shell.Current.GoToAsync("GuestRoomPage");
        }
        catch (Exception ex)
        {
            spinner.IsRunning = false;
            lblStatus.Text    = "Scanning for rooms…";
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

    private void OnInviteReceived(object? sender, RoomInvite invite)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (_discovery is null || _isJoiningInvite) return;

            if (!string.Equals(invite.ProtocolVersion, SspCbor.ProtocolVersion, StringComparison.Ordinal))
            {
                await _discovery.SendInviteRefusalAsync(invite.InviteId, _guestId, "version", invite.HostIp);
                return;
            }

            bool accept = await DisplayAlert(
                "Room Invite",
                $"{invite.RoomName} from {invite.HostIp}. Join now?",
                "Join",
                "Refuse");

            if (!accept)
            {
                await _discovery.SendInviteRefusalAsync(invite.InviteId, _guestId, "user_refused", invite.HostIp);
                return;
            }

            try
            {
                _isJoiningInvite = true;
                var ann = new RoomAnnouncement(invite.RoomId, invite.RoomName, invite.HostIp, invite.TcpPort);
                var room = await Room.JoinAsync(ann);
                RoomSession.Set(room);
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
