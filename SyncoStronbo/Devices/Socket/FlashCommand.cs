namespace SyncoStronbo.Devices.Socket {

    /// <summary>
    /// Sent over TCP from host to all guests.
    /// <see cref="AtUnixMs"/> is an absolute UTC timestamp (milliseconds since epoch);
    /// every device schedules its flash to fire at that exact moment.
    /// </summary>
    internal record FlashCommand(
        string Action,   // "on" | "off"
        long   AtUnixMs  // DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + leadMs
    );
}
