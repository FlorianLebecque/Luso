#nullable enable
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using SyncoStronbo.Features.Rooms.Domain;

namespace SyncoStronbo.Features.Rooms.Networking {
    /// <summary>
    /// SSP/1.0 §4 — Host-side TCP session server.
    ///
    /// Lifecycle per guest:
    ///   TCP connect → Guest sends JOIN → Host sends JACK → session active
    ///   Heartbeat: Host sends PING every 2 s; expects PONG within 6 s.
    ///   Graceful close: Host broadcasts CLOS on dispose; Guest sends LEAV to leave.
    /// Framing: CBOR Sequences (RFC 8742).
    /// </summary>
    internal sealed class SocketRoomHost : IDisposable {
        public const int TcpPort = 13000;

        private const int PingIntervalMs  = 2_000;
        private const int PingTimeoutMs   = 6_000;
        private const int HandshakeTimeMs = 5_000;
        private const int LeadMs          = 500;

        // ── Per-guest state ───────────────────────────────────────────────────

        private sealed class GuestState {
            public TcpClient          Client       { get; init; } = null!;
            public SemaphoreSlim      WriteLock    { get; }       = new(1, 1);
            public long               LastPongAtMs { get; set; }  = Now();
            public int                LatestRttMs  { get; set; }  = -1;
            public string             Name         { get; init; } = string.Empty;
            public GuestCapabilities  Capabilities { get; init; } = GuestCapabilities.Unknown;
        }

        // ── Fields ────────────────────────────────────────────────────────────

        private readonly TcpListener _listener;
        private readonly ConcurrentDictionary<string, GuestState> _guests = new();
        private readonly CancellationTokenSource _cts = new();

        public string RoomId   { get; }
        public string RoomName { get; }

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Raised after the JOIN/JACK handshake completes successfully.</summary>
        public event EventHandler<GuestJoinedArgs>? OnGuestConnected;
        public event EventHandler<string>?          OnGuestDisconnected;
        public event EventHandler<GuestPingArgs>?   OnGuestPingUpdated;
        public event EventHandler<FlashCommand>?    OnFlashScheduled;

        // ── Constructor ───────────────────────────────────────────────────────

        public SocketRoomHost(string roomName, string roomId) {
            RoomName = roomName;
            RoomId   = roomId;

            _listener = new TcpListener(IPAddress.Any, TcpPort);
            _listener.Start();

            _ = AcceptLoopAsync(_cts.Token);
            _ = HeartbeatLoopAsync(_cts.Token);
        }

        // ── Public API ────────────────────────────────────────────────────────

        public int GuestCount => _guests.Count;

        public IReadOnlyList<(string Ip, string Name, int RttMs)> GetGuests() {
            var list = new List<(string, string, int)>(_guests.Count);
            foreach (var (ip, state) in _guests)
                list.Add((ip, state.Name, state.LatestRttMs));
            return list;
        }

        public async Task<bool> KickGuestAsync(string ip, string reason = "removed_by_host") {
            if (!_guests.TryGetValue(ip, out var state))
                return false;

            await SendAsync(ip, state, SspCbor.Kick(reason));
            RemoveGuest(ip, state);
            return true;
        }

        /// <summary>Broadcasts a FLSH command to all connected guests.</summary>
        public async Task FlashAsync(string action = "on", int leadMs = LeadMs) {
            long atUnixMs = Now() + leadMs;
            byte[] bytes  = SspCbor.Flsh(action, atUnixMs);

            OnFlashScheduled?.Invoke(this, new FlashCommand(action, atUnixMs));

            var tasks = new List<Task>(_guests.Count);
            foreach (var (key, state) in _guests)
                tasks.Add(SendAsync(key, state, bytes));
            await Task.WhenAll(tasks);
        }

        // ── Accept loop ───────────────────────────────────────────────────────

        private async Task AcceptLoopAsync(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                TcpClient client;
                try {
                    client = await _listener.AcceptTcpClientAsync(token);
                } catch (OperationCanceledException) {
                    break;
                } catch {
                    continue;
                }

                string ip = ((IPEndPoint)client.Client.RemoteEndPoint!).Address.ToString();
                _ = HandshakeAndMonitorAsync(ip, client, token);
            }
        }

        // ── JOIN / JACK handshake ─────────────────────────────────────────────

        private async Task HandshakeAndMonitorAsync(
            string ip, TcpClient client, CancellationToken token) {
            try {
                using var timeout = new CancellationTokenSource(HandshakeTimeMs);
                using var linked  = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token);

                var stream = client.GetStream();
                var reader = new CborStreamReader(stream);

                // Wait for JOIN ────────────────────────────────────────────────
                var raw = await reader.ReadNextAsync(linked.Token);
                if (raw is null) { client.Dispose(); return; }

                var msg  = SspCbor.ParseMap(raw.Value);
                if (SspCbor.Tag(msg) != "JOIN") { client.Dispose(); return; }

                string guestPv = msg.TryGetValue("pv", out var pvObj) && pvObj is string pv ? pv : string.Empty;
                if (!string.Equals(guestPv, SspCbor.ProtocolVersion, StringComparison.Ordinal)) {
                    byte[] jnak = SspCbor.Jnak("PROTOCOL_MISMATCH", $"Host uses {SspCbor.ProtocolVersion}, guest uses {guestPv}");
                    await stream.WriteAsync(jnak, token);
                    client.Dispose();
                    return;
                }

                string            guestName = msg.TryGetValue("nm", out var nm) && nm is string s ? s : ip;
                GuestCapabilities cap       = SspCbor.ParseCap(msg.TryGetValue("cap", out var c) ? c : null);

                // Send JACK ────────────────────────────────────────────────────
                byte[] jack = SspCbor.Jack(RoomName, RoomId);
                await stream.WriteAsync(jack, token);

                // Register guest and start session ────────────────────────────
                var state = new GuestState {
                    Client       = client,
                    LastPongAtMs = Now(),
                    Name         = guestName,
                    Capabilities = cap
                };
                _guests[ip] = state;
                OnGuestConnected?.Invoke(this, new GuestJoinedArgs(ip, guestName, cap));

                await MonitorGuestAsync(ip, state, reader, token);

            } catch {
                client.Dispose();
            }
        }

        // ── Guest monitor ─────────────────────────────────────────────────────

        private async Task MonitorGuestAsync(
            string ip, GuestState state, CborStreamReader reader, CancellationToken token) {
            try {
                while (!token.IsCancellationRequested) {
                    var raw = await reader.ReadNextAsync(token);
                    if (raw is null) break;

                    var msg = SspCbor.ParseMap(raw.Value);
                    await HandleGuestMessageAsync(ip, state, msg);
                }
            } catch (OperationCanceledException) {
                // Normal shutdown.
            } catch {
                // Network error — fall through to cleanup.
            }

            RemoveGuest(ip, state);
        }

        private async Task HandleGuestMessageAsync(
            string ip, GuestState state, Dictionary<string, object?> msg) {
            switch (SspCbor.Tag(msg)) {
                case "PONG":
                    if (msg.TryGetValue("ms", out var msObj) && msObj is ulong sentAt) {
                        int rtt = (int)(Now() - (long)sentAt);
                        state.LastPongAtMs = Now();
                        state.LatestRttMs  = rtt;
                        OnGuestPingUpdated?.Invoke(this, new GuestPingArgs(ip, rtt));
                    }
                    break;

                case "LEAV":
                    RemoveGuest(ip, state);
                    break;
            }
            await Task.CompletedTask;
        }

        // ── Heartbeat loop ────────────────────────────────────────────────────

        private async Task HeartbeatLoopAsync(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                try { await Task.Delay(PingIntervalMs, token); }
                catch (OperationCanceledException) { break; }

                long now = Now();
                foreach (var (ip, state) in _guests) {
                    if (now - state.LastPongAtMs > PingTimeoutMs) {
                        RemoveGuest(ip, state);
                        continue;
                    }
                    await SendAsync(ip, state, SspCbor.Ping(now));
                }
            }
        }

        // ── Send helper ───────────────────────────────────────────────────────

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

        // ── Dispose ───────────────────────────────────────────────────────────

        public void Dispose() {
            // Broadcast CLOS to all guests before shutting down.
            byte[] clos = SspCbor.Clos();
            foreach (var (ip, state) in _guests)
                _ = SendAsync(ip, state, clos);

            Thread.Sleep(200); // brief flush window

            _cts.Cancel();
            _listener.Stop();
            foreach (var (_, state) in _guests) state.Client.Dispose();
            _guests.Clear();
            _cts.Dispose();
        }

        // ── Utility ───────────────────────────────────────────────────────────

        private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    internal readonly record struct GuestJoinedArgs(
        string            Ip,
        string            Name,
        GuestCapabilities Capabilities);

    internal readonly record struct GuestPingArgs(string Ip, int RttMs);
}

