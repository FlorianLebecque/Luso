namespace Luso.Features.Rooms.Networking {
    internal record FlashCommand(
        string Action,
        long AtUnixMs
    );
}
