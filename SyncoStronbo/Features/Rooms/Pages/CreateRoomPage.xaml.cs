using SyncoStronbo.Features.Rooms.Domain;
using SyncoStronbo.Shared.Session;

namespace SyncoStronbo.Features.Rooms.Pages;

public partial class CreateRoomPage : ContentPage
{
    private bool _isCreating;

    public CreateRoomPage()
    {
        InitializeComponent();
    }

    private async void OnBackClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync("..");

    private async void OnCreateClicked(object sender, EventArgs e)
    {
        if (_isCreating) return;

        string name = entryName.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(name))
        {
            lblError.Text = "Please enter a room name.";
            lblError.IsVisible = true;
            return;
        }

        lblError.IsVisible = false;
        _isCreating = true;
        btnCreate.IsEnabled = false;
        spinner.IsVisible = true;
        spinner.IsRunning = true;

        try
        {
            var room = await Task.Run(() => Room.Create(name));
            RoomSession.Set(room);
            await Shell.Current.GoToAsync("HostRoomPage");
        }
        catch (Exception ex)
        {
            lblError.Text = ex.Message;
            lblError.IsVisible = true;
        }
        finally
        {
            _isCreating = false;
            btnCreate.IsEnabled = true;
            spinner.IsRunning = false;
            spinner.IsVisible = false;
        }
    }
}
