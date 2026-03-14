namespace SyncoStronbo.Features.Home.Pages;

public partial class HomePage : ContentPage
{
    public HomePage()
    {
        InitializeComponent();
    }

    private async void btnCreateClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("CreateRoom");
    }

    private async void btnEnterClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("BrowseRooms");
    }
}
