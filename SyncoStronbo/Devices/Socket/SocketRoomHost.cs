#nullable enable
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SyncoStronbo.Devices.Socket {

    /// <summary>
    /// TCP server that represents the room owner (host).
    ///
    /// Protocol (newline-delimited JSON over TCP):
    ///   host → guest:  {"Type":"PING","SentAtMs":ts}
    ///                  {"Type":"FLASH","Action":"on","AtUnixMs":ts}
    ///                  {"Type":"CLOSE"}   (sent before the host shuts down)
    ///   guest → host:  {"Type":"PONG","SentAtMs":ts}   (echo of the ping ts)
    ///                  {"Type":"LEAVE"}                 (graceful guest exit)
    /// </summary>
    internal sealed class SocketRoomHost : IDisposable {

        public const int TcpPort      = 13000;
        private const int LeadMs      = 500;   // flash scheduling lead time
        private const int PingIntervalMs = 2000;
        private const int PingTimeoutMs  = 6000; // 3 missed pings → remove guest

        // ── Per-guest state ──────────────────────────────────────────────────────
        private sealed class GuestState {
            public TcpClient        Client    { get; init; } = null!;
            public SemaphoreSlim    WriteLock { get; }       = new(1, 1);
            public long             LastPongAtMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            public int              LatestRttMs  { get; set; } = -1;
        }

        private readonly TcpListener                               _listener;
        private readonly ConcurrentDictionary<string, GuestState> _guests = new();
        private readonly CancellationTokenSource                   _cts    = new();

        public string RoomId   { get; }
        public string RoomName { get; }

        /// <summary>Raised when a guest connects. Arg = guest IP.</summary>
        public event EventHandler<string>?       OnGuestConnected;
        /// <summary>Raised when a guest disconnects. Arg = guest IP.</summary>
        public event EventHandler<string>?       OnGuestDisconnected;
        /// <summary>Raised after each PING round-trip. Args = (guestIp, rttMs).</summary>
        public event EventHandler<GuestPingArgs>? OnGuestPingUpdated;
        /// <summary>Raised on the host itself when a flash is scheduled.</summary>
        public event EventHandler<FlashCommand>? OnFlashScheduled;

        public SocketRoomHost(string roomName, string roomId) {
            RoomName = roomName;
            RoomId   = roomId;

            _listener = new TcpListener(IPAddress.Any, TcpPort);
            _listener.Start();

            _ = AcceptLoopAsync(_cts.Token);
            _ = HeartbeatLoopAsync(_cts.Token);
        }

        // ── Public API ───────────────────────────────────────────────────────────

        public async Task FlashAsync(string action = "on", int leadMs = LeadMs) {
            long atUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + leadMs;
            var  cmd      = new FlashCommand(action, atUnixMs);
            var  bytes    = BuildMessage(new { Type = "FLASH", cmd.Action, cmd.AtUnixMs });

            OnFlashScheduled?.Invoke(this, cmd);

            var tasks = new List<Task>();
            foreach (var (key, state) in _guests)
                tasks.Add(SendAsync(key, state, bytes));
            await Task.WhenAll(tasks);
        }

        public int GuestCount => _guests.Count;

        /// <summary>Snapshot of all connected guests with their latest RTT.</summary>
        public IReadOnlyList<(string Ip, int RttMs)> GetGuests() {
            var list = new List<(string, int)>(_guests.Count);
            foreach (var (ip, state) in _guests)
                list.Add((ip, state.LatestRttMs));
            return list;
        }

        // ── Heartbeat loop ───────────────────────────────────────────────────────

        private async Task HeartbeatLoopAsync(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                try { await Task.Delay(PingIntervalMs, token); } catch (OperationCanceledException) { break; }

                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                foreach (var (ip, state) in _guests) {
                    // Evict timed-out guests first
                    if (now - state.LastPongAtMs > PingTimeoutMs) {
                        RemoveGuest(ip, state);
                        continue;
                    }
                    await SendAsync(ip, state, BuildMessage(new { Type = "PING", SentAtMs = now }));
                }
            }
        }

        // ── Accept loop ──────────────────────────────────────────────────────────

        private async Task AcceptLoopAsync(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                try {
                    TcpClient client = await _listener.AcceptTcpClientAsync(token);
                    string ip = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();

                    var state = new GuestState { Client = client };
                    _guests[ip] = state;
                    OnGuestConnected?.Invoke(this, ip);

                    _ = MonitorGuestAsync(ip, state, token);
                } catch (OperationCanceledException) {
                    break;
                } catch { /* transient accept error */ }
            }
        }

        // ── Per-guest read loop ──────────────────────────────────────────────────

        private async Task MonitorGuestAsync(string ip, GuestState state, CancellationToken token) {
            try {
                using var reader = new StreamReader(state.Client.GetStream(), Encoding.UTF8, leaveOpen: true);
                while (!token.IsCancellationRequested) {
                    string? line = await reader.ReadLineAsync(token);
                    if (line is null) break;
                    await HandleGuestMessageAsync(ip, state, line);
                }
            } catch { /* disconnected or cancelled */ }

            RemoveGuest(ip, state);
        }

        private async Task HandleGuestMessageAsync(string ip, GuestState state, string json) {
            try {
                using var doc  = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("Type", out var typeProp)) return;

                switch (typeProp.GetString()) {
                    case "PONG": {
                        long sentAt = root.GetProperty("SentAtMs").GetInt64();
                        int  rtt    = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - sentAt);
                        state.LastPongAtMs  = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        state.LatestRttMs   = rtt;
                        OnGuestPingUpdated?.Invoke(this, new GuestPingArgs(ip, rtt));
                        break;
                    }
                    case "LEAVE":
                        RemoveGuest(ip, state);
                        break;
                }
            } catch { /* malformed message */ }
            await Task.CompletedTask;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private async Task SendAsync(string ip, GuestState state, byte[] bytes) {
            try {
                await state.WriteLock.WaitAsync();
                try {
                    await state.Client.GetStream().WriteAsync(bytes);
                } finally {
                    state.WriteLock.Release();
                }
            } catch {
                RemoveGuest(ip, state);
            }
        }

        private void RemoveGuest(string ip, GuestState state) {
            if (_guests.TryRemove(ip, out _)) {
                state.Client.Dispose();
                OnGuestDisconnected?.Invoke(this, ip);
            }
        }

        private static byte[] BuildMessage(object payload)
            => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload) + "\n");

        // ── Dispose ──────────────────────────────────────────────────────────────

        public void Dispose() {
            // Tell all guests the room is closing before cutting the connection.
            var closeBytes = BuildMessage(new { Type = "CLOSE" });
            foreach (var (ip, state) in _guests)
                _ = SendAsync(ip, state, closeBytes);

            // Small grace window so CLOSE messages are actually sent.
            Thread.Sleep(200);

            _cts.Cancel();
            _listener.Stop();
            foreach (var (_, state) in _guests) state.Client.Dispose();
            _guests.Clear();
            _cts.Dispose();
        }
    }

    internal readonly record struct GuestPingArgs(string Ip, int RttMs);
}
