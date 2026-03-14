#nullable enable
using System.Collections.ObjectModel;
using SyncoStronbo.Features.Rooms.Domain;
using SyncoStronbo.Features.Rooms.Networking;
using SyncoStronbo.Shared.Session;

namespace SyncoStronbo.Features.Rooms.Pages;

public partial class BrowseRoomsPage : ContentPage
{
    private readonly ObservableCollection<RoomAnnouncement> _rooms = new();
    private readonly HashSet<string> _seenIds = new();
    private UdpRoomDiscovery? _discovery;
    private readonly string _guestId = GuestIdentity.GetOrCreateGuestId();
    private bool _isJoining;

    public BrowseRoomsPage()
    {
        InitializeComponent();
        roomsCollection.ItemsSource = _rooms;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _rooms.Clear();
        _seenIds.Clear();

        _discovery = new UdpRoomDiscovery();
        _discovery.OnRoomDiscovered += OnRoomDiscovered;
        _discovery.OnInviteReceived += OnInviteReceived;
        _discovery.StartListening();
        _discovery.StartGuestPresence(_guestId, GuestIdentity.DeviceName());

        lblStatus.Text = "Scanning for rooms on this network…";
        spinner.IsRunning = true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_discovery is not null)
            _discovery.OnInviteReceived -= OnInviteReceived;
        _discovery?.Dispose();
        _discovery = null;
        _isJoining = false;
    }

    private void OnRoomDiscovered(object? sender, RoomAnnouncement ann)
    {
        MainThread.BeginInvokeOnMainThread(() => {
            if (_seenIds.Add(ann.RoomId))
                _rooms.Add(ann);
        });
    }

    private async void OnRoomSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not RoomAnnouncement ann) return;
        if (_isJoining) return;

        roomsCollection.SelectedItem = null;
        spinner.IsRunning = true;
        lblStatus.Text = $"Joining '{ann.RoomName}'…";

        try
        {
            _isJoining = true;
            var room = await Room.JoinAsync(ann);
            RoomSession.Set(room);
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

    private void OnInviteReceived(object? sender, RoomInvite invite)
    {
        MainThread.BeginInvokeOnMainThread(async () => {
            if (_discovery is null || _isJoining) return;

            if (!string.Equals(invite.ProtocolVersion, SspCbor.ProtocolVersion, StringComparison.Ordinal)) {
                await _discovery.SendInviteRefusalAsync(invite.InviteId, _guestId, "version", invite.HostIp);
                return;
            }

            bool accept = await DisplayAlert(
                "Room Invite",
                $"{invite.RoomName} from {invite.HostIp}. Join now?",
                "Join",
                "Refuse");

            if (!accept) {
                await _discovery.SendInviteRefusalAsync(invite.InviteId, _guestId, "user_refused", invite.HostIp);
                return;
            }

            try {
                _isJoining = true;
                spinner.IsRunning = true;
                lblStatus.Text = $"Joining '{invite.RoomName}'…";

                var ann = new RoomAnnouncement(invite.RoomId, invite.RoomName, invite.HostIp, invite.TcpPort);
                var room = await Room.JoinAsync(ann);
                RoomSession.Set(room);
                await Shell.Current.GoToAsync("GuestRoomPage");
            } catch (Exception ex) {
                await DisplayAlert("Invite join failed", ex.Message, "OK");
                lblStatus.Text = "Scanning for rooms on this network…";
            } finally {
                _isJoining = false;
            }
        });
    }


    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Home");
    }
}
