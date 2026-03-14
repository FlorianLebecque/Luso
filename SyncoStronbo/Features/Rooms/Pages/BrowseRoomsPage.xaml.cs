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
        _discovery.StartListening();

        lblStatus.Text = "Scanning for rooms on this network…";
        spinner.IsRunning = true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _discovery?.Dispose();
        _discovery = null;
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

        roomsCollection.SelectedItem = null;
        spinner.IsRunning = true;
        lblStatus.Text = $"Joining '{ann.RoomName}'…";

        try
        {
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
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Home");
    }
}
