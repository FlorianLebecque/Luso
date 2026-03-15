#nullable enable
using System.Net.Sockets;
using Luso.Features.Rooms.Domain.Commands;

namespace Luso.Features.Rooms.Networking.Ssp
{
    /// <summary>
    /// SSP/1.0 §4 — Guest-side TCP session client.
    ///
    /// Lifecycle:
    ///   ConnectAsync → sends JOIN → waits for JACK → session active
    ///   Responds to PING with PONG; fires OnFlashCommand on FLSH.
    ///   Watchdog: fires OnDisconnected if no PING arrives within 6 s.
    ///   Dispose: sends LEAV gracefully before closing.
    /// Framing: CBOR Sequences (RFC 8742).
    /// </summary>
    internal sealed class SocketRoomGuest : IDisposable
    {
        private const int PingTimeoutMs = 6_000;
        private const int HandshakeTimeMs = 5_000;

        private readonly TcpClient _client;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly CancellationTokenSource _cts = new();
        private volatile bool _leavingVoluntarily;
        private volatile bool _kicked;   // set on KICK to suppress redundant OnDisconnected
        private long _lastPingAtMs;
        private bool _watchdogStarted;

        public string HostIp { get; }
        public string RoomId { get; }
        public string RoomName { get; }
        public bool IsConnected => _client.Connected;

        public event EventHandler<FlashCommand>? OnFlashCommand;
        public event EventHandler? OnDisconnected;
        public event EventHandler? OnKicked;

        private SocketRoomGuest(TcpClient client, RoomAnnouncement room)
        {
            _client = client;
            HostIp = room.HostIp;
            RoomId = room.RoomId;
            RoomName = room.RoomName;
            _lastPingAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        // ── Factory ───────────────────────────────────────────────────────────

        /// <summary>
        /// Opens a TCP connection, performs the JOIN/JACK handshake, then returns an
        /// active guest session. Throws if the handshake times out or is rejected.
        /// </summary>
        public static async Task<SocketRoomGuest> ConnectAsync(
            RoomAnnouncement room,
            string deviceName,
            GuestCapabilities capabilities,
            int timeoutMs = 5_000)
        {

            var client = new TcpClient();
            using var connectCts = new CancellationTokenSource(timeoutMs);
            await client.ConnectAsync(room.HostIp, room.TcpPort, connectCts.Token);

            var guest = new SocketRoomGuest(client, room);
            var stream = client.GetStream();
            var reader = new CborStreamReader(stream);

            // Send JOIN immediately after connecting.
            await stream.WriteAsync(SspCbor.Join(deviceName, capabilities), connectCts.Token);

            // Wait for JACK within the handshake window.
            using var handshakeCts = new CancellationTokenSource(HandshakeTimeMs);
            var raw = await reader.ReadNextAsync(handshakeCts.Token)
                      ?? throw new InvalidOperationException("Host closed connection during handshake.");

            var msg = SspCbor.ParseMap(raw);
            string tag = SspCbor.Tag(msg);
            if (tag == "JNAK")
            {
                string ec = msg.TryGetValue("ec", out var ecObj) && ecObj is string ecs ? ecs : "UNKNOWN";
                string txt = msg.TryGetValue("msg", out var mObj) && mObj is string ms ? ms : "Join refused by host.";
                string pv = msg.TryGetValue("pv", out var pvObj) && pvObj is string pvs ? pvs : "?";
                throw new InvalidOperationException($"Join refused ({ec}). {txt} Host protocol: {pv}.");
            }
            if (tag != "JACK")
                throw new InvalidOperationException($"Expected JACK, received '{tag}'.");

            string hostPv = msg.TryGetValue("pv", out var hpvObj) && hpvObj is string hpv ? hpv : string.Empty;
            if (!string.Equals(hostPv, SspCbor.ProtocolVersion, StringComparison.Ordinal))
                throw new InvalidOperationException($"Protocol mismatch. Guest uses {SspCbor.ProtocolVersion}, host uses {hostPv}.");

            // Start listening loop in the background.
            _ = guest.ListenLoopAsync(reader, guest._cts.Token);

            return guest;
        }

        // ── Listen loop ───────────────────────────────────────────────────────

        private async Task ListenLoopAsync(CborStreamReader reader, CancellationToken token)
        {
            bool hostClosed = false;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var raw = await reader.ReadNextAsync(token);
                    if (raw is null) break;

                    hostClosed = await HandleMessageAsync(SspCbor.ParseMap(raw.Value));
                    if (hostClosed) break;
                }
            }
            catch (OperationCanceledException)
            {
                return; // voluntary dispose
            }
            catch
            {
                // network error — fall through
            }

            if (!_leavingVoluntarily && !_kicked)
                OnDisconnected?.Invoke(this, EventArgs.Empty);
        }

        private async Task<bool> HandleMessageAsync(Dictionary<string, object?> msg)
        {
            switch (SspCbor.Tag(msg))
            {
                case "PING":
                    if (msg.TryGetValue("ms", out var msObj) && msObj is ulong sentAt)
                    {
                        _lastPingAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        if (!_watchdogStarted)
                        {
                            _watchdogStarted = true;
                            _ = WatchdogLoopAsync(_cts.Token);
                        }
                        await WriteAsync(SspCbor.Pong(sentAt));
                    }
                    break;

                case "FLSH":
                    if (msg.TryGetValue("ac", out var ac) && ac is string action &&
                        msg.TryGetValue("at", out var at) && at is ulong atMs)
                    {
                        var fa = action == "on" ? FlashAction.On : FlashAction.Off;
                        OnFlashCommand?.Invoke(this, new FlashCommand(fa, (long)atMs));
                    }
                    break;

                case "CLOS":
                    return true; // signal host-initiated close

                case "KICK":
                    _kicked = true;  // prevents ListenLoopAsync from also raising OnDisconnected
                    OnKicked?.Invoke(this, EventArgs.Empty);
                    return true;
            }
            return false;
        }

        // ── Watchdog ──────────────────────────────────────────────────────────

        private async Task WatchdogLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try { await Task.Delay(2_000, token); }
                catch (OperationCanceledException) { return; }

                long elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _lastPingAtMs;
                if (elapsed > PingTimeoutMs)
                {
                    if (!_leavingVoluntarily && !_kicked)
                        OnDisconnected?.Invoke(this, EventArgs.Empty);
                    return;
                }
            }
        }

        // ── Write helper ──────────────────────────────────────────────────────

        private async Task WriteAsync(byte[] bytes)
        {
            await _writeLock.WaitAsync();
            try
            {
                await _client.GetStream().WriteAsync(bytes);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        // ── Dispose ───────────────────────────────────────────────────────────

        public void Dispose()
        {
            _leavingVoluntarily = true;
            try
            {
                _client.GetStream().Write(SspCbor.Leav());
            }
            catch
            {
                // Best-effort; ignore if stream is already closed.
            }

            _cts.Cancel();
            _client.Dispose();
            _writeLock.Dispose();
            _cts.Dispose();
        }
    }
}
