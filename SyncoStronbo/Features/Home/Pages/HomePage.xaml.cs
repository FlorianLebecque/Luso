#nullable enable
using SyncoStronbo.Features.Rooms.Domain;
using SyncoStronbo.Features.Rooms.Networking;
using SyncoStronbo.Shared.Session;

namespace SyncoStronbo.Features.Home.Pages;

public partial class HomePage : ContentPage
{
    private readonly string _guestId = GuestIdentity.GetOrCreateGuestId();
    private UdpRoomDiscovery? _inviteDiscovery;
    private bool _isJoiningInvite;
    private bool _isNavigating;

    public HomePage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _isNavigating = false;
        SetNavigationButtonsEnabled(true);

        _inviteDiscovery = new UdpRoomDiscovery();
        _inviteDiscovery.OnInviteReceived += OnInviteReceived;
        _inviteDiscovery.StartListening();
        _inviteDiscovery.StartGuestPresence(_guestId, GuestIdentity.DeviceName());
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_inviteDiscovery is not null)
        {
            _inviteDiscovery.OnInviteReceived -= OnInviteReceived;
            _inviteDiscovery.Dispose();
            _inviteDiscovery = null;
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

    private async void btnEnterClicked(object sender, EventArgs e)
    {
        if (_isNavigating) return;
        _isNavigating = true;
        SetNavigationButtonsEnabled(false);
        try
        {
            await Shell.Current.GoToAsync("BrowseRooms");
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
            if (_inviteDiscovery is null || _isJoiningInvite) return;

            if (!string.Equals(invite.ProtocolVersion, SspCbor.ProtocolVersion, StringComparison.Ordinal))
            {
                await _inviteDiscovery.SendInviteRefusalAsync(invite.InviteId, _guestId, "version", invite.HostIp);
                return;
            }

            bool accept = await DisplayAlert(
                "Room Invite",
                $"{invite.RoomName} from {invite.HostIp}. Join now?",
                "Join",
                "Refuse");

            if (!accept)
            {
                await _inviteDiscovery.SendInviteRefusalAsync(invite.InviteId, _guestId, "user_refused", invite.HostIp);
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
        btnEnter.IsEnabled = enabled;
    }
}
