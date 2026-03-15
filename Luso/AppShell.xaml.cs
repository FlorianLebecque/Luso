namespace Luso
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute("CreateRoom", typeof(Features.Rooms.Pages.CreateRoomPage));
            Routing.RegisterRoute("BrowseRooms", typeof(Features.Rooms.Pages.BrowseRoomsPage));
            Routing.RegisterRoute("HostRoomPage", typeof(Features.Rooms.Pages.HostRoomPage));
            Routing.RegisterRoute("GuestRoomPage", typeof(Features.Rooms.Pages.GuestRoomPage));
        }
    }
}