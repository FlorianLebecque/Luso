namespace SyncoStronbo
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute("CreateRoom", typeof(Pages.CreateRoomPage));
            Routing.RegisterRoute("BrowseRooms", typeof(Pages.BrowseRoomsPage));
            Routing.RegisterRoute("HostRoomPage", typeof(Pages.HostRoomPage));
            Routing.RegisterRoute("GuestRoomPage", typeof(Pages.GuestRoomPage));
        }
    }
}