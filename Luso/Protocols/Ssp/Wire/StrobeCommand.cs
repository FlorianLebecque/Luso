namespace Luso.Features.Rooms.Networking.Ssp
{
    internal sealed record SspStrobeCommand(
        long AtUnixMs,
        int OnMs,
        int OffMs,
        double FrequencyHz
    );
}
