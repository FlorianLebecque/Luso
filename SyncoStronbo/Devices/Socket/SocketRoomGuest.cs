#nullable enable
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SyncoStronbo.Devices.Socket {

    /// <summary>
    /// TCP client that connects to a room host after discovering it via UDP.
    ///
    /// Protocol (newline-delimited JSON over TCP):
    ///   host → guest:  {"Type":"PING","SentAtMs":ts}
    ///                  {"Type":"FLASH","Action":"on","AtUnixMs":ts}
    ///                  {"Type":"CLOSE"}
    ///   guest → host:  {"Type":"PONG","SentAtMs":ts}
    ///                  {"Type":"LEAVE"}
    /// </summary>
    internal sealed class SocketRoomGuest : IDisposable {

        private const int PingTimeoutMs = 6000; // host considered gone if no PING for this long

        private readonly TcpClient               _client;
        private readonly SemaphoreSlim            _writeLock  = new(1, 1);
        private readonly CancellationTokenSource  _cts        = new();
        private volatile bool                     _leavingVoluntarily;
        private long _lastPingAtMs; // set after first ping so grace period applies
        private bool _watchdogStarted;

        public string HostIp   { get; }
        public string RoomId   { get; }
        public string RoomName { get; }
        public bool   IsConnected => _client.Connected;

        /// <summary>Raised when the host sends a flash command.</summary>
        public event EventHandler<FlashCommand>? OnFlashCommand;

        /// <summary>
        /// Raised when the connection to the host is lost unexpectedly
        /// (network drop, host crash, or host closing the room).
        /// NOT raised when the guest voluntarily calls <see cref="Dispose"/>.
        /// </summary>
        public event EventHandler? OnDisconnected;

        private SocketRoomGuest(TcpClient client, RoomAnnouncement room) {
            _client  = client;
            HostIp   = room.HostIp;
            RoomId   = room.RoomId;
            RoomName = room.RoomName;

            _ = ListenLoopAsync(_cts.Token);
        }

        // ── Factory ──────────────────────────────────────────────────────────────

        public static async Task<SocketRoomGuest> ConnectAsync(RoomAnnouncement room, int timeoutMs = 5000) {
            var client = new TcpClient();
            using var cts = new CancellationTokenSource(timeoutMs);
            await client.ConnectAsync(room.HostIp, room.TcpPort, cts.Token);
            return new SocketRoomGuest(client, room);
        }

        // ── Listen loop ──────────────────────────────────────────────────────────

        private async Task ListenLoopAsync(CancellationToken token) {
            bool hostClosed = false;
            try {
                using var reader = new StreamReader(_client.GetStream(), Encoding.UTF8, leaveOpen: true);
                while (!token.IsCancellationRequested) {
                    string? line = await reader.ReadLineAsync(token);
                    if (line is null) break; // TCP EOF
                    hostClosed = await HandleLineAsync(line);
                    if (hostClosed) break;
                }
            } catch (OperationCanceledException) {
                // voluntary dispose – do not fire OnDisconnected
                return;
            } catch { /* network error */ }

            if (!_leavingVoluntarily)
                OnDisconnected?.Invoke(this, EventArgs.Empty);
        }

        // ── Watchdog loop ────────────────────────────────────────────────────────

        private async Task WatchdogLoopAsync(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                try { await Task.Delay(2000, token); } catch (OperationCanceledException) { return; }

                long elapsed = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _lastPingAtMs;
                if (elapsed > PingTimeoutMs) {
                    if (!_leavingVoluntarily)
                        OnDisconnected?.Invoke(this, EventArgs.Empty);
                    return;
                }
            }
        }

        // ── Message handling ─────────────────────────────────────────────────────

        /// <returns>true if the loop should stop (host sent CLOSE).</returns>
        private async Task<bool> HandleLineAsync(string json) {
            try {
                using var doc  = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("Type", out var typeProp)) return false;

                switch (typeProp.GetString()) {
                    case "PING": {
                        long sentAt = root.GetProperty("SentAtMs").GetInt64();
                        _lastPingAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                        // Start watchdog on first PING
                        if (!_watchdogStarted) {
                            _watchdogStarted = true;
                            _ = WatchdogLoopAsync(_cts.Token);
                        }

                        await WriteAsync(JsonSerializer.Serialize(new { Type = "PONG", SentAtMs = sentAt }) + "\n");
                        break;
                    }
                    case "FLASH":
                        OnFlashCommand?.Invoke(this, new FlashCommand(
                            Action:   root.GetProperty("Action").GetString()!,
                            AtUnixMs: root.GetProperty("AtUnixMs").GetInt64()
                        ));
                        break;
                    case "CLOSE":
                        return true; // host voluntarily closed; caller fires OnDisconnected
                }
            } catch { /* malformed message */ }
            return false;
        }

        // ── Write helper (serialised) ─────────────────────────────────────────────

        private async Task WriteAsync(string line) {
            var bytes = Encoding.UTF8.GetBytes(line);
            await _writeLock.WaitAsync();
            try {
                await _client.GetStream().WriteAsync(bytes);
            } finally {
                _writeLock.Release();
            }
        }

        // ── Dispose ──────────────────────────────────────────────────────────────

        public void Dispose() {
            _leavingVoluntarily = true;

            // Best-effort LEAVE notification so host removes us immediately.
            try {
                var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Type = "LEAVE" }) + "\n");
                _client.GetStream().Write(bytes);
            } catch { /* connection already gone */ }

            _cts.Cancel();
            _client.Dispose();
            _writeLock.Dispose();
            _cts.Dispose();
        }
    }
}
