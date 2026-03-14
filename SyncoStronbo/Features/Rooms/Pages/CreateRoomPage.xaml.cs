using SyncoStronbo.Features.Rooms.Domain;
using SyncoStronbo.Shared.Session;

namespace SyncoStronbo.Features.Rooms.Pages;

public partial class CreateRoomPage : ContentPage
{
    public CreateRoomPage()
    {
        InitializeComponent();
    }

    private async void OnCreateClicked(object sender, EventArgs e)
    {
        string name = entryName.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(name))
        {
            lblError.Text = "Please enter a room name.";
            lblError.IsVisible = true;
            return;
        }

        lblError.IsVisible = false;
        btnCreate.IsEnabled = false;
        spinner.IsVisible = true;
        spinner.IsRunning = true;

        try
        {
            var room = Room.Create(name);
            RoomSession.Set(room);
            await Shell.Current.GoToAsync("HostRoomPage");
        }
        catch (Exception ex)
        {
            lblError.Text = ex.Message;
            lblError.IsVisible = true;
            btnCreate.IsEnabled = true;
        }
        finally
        {
            spinner.IsRunning = false;
            spinner.IsVisible = false;
        }
    }
}
