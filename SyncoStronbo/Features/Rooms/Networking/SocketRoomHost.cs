#nullable enable
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SyncoStronbo.Features.Rooms.Networking {
    internal sealed class SocketRoomHost : IDisposable {
        public const int TcpPort = 13000;
        private const int LeadMs = 500;
        private const int PingIntervalMs = 2000;
        private const int PingTimeoutMs = 6000;

        private sealed class GuestState {
            public TcpClient Client { get; init; } = null!;
            public SemaphoreSlim WriteLock { get; } = new(1, 1);
            public long LastPongAtMs { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            public int LatestRttMs { get; set; } = -1;
        }

        private readonly TcpListener _listener;
        private readonly ConcurrentDictionary<string, GuestState> _guests = new();
        private readonly CancellationTokenSource _cts = new();

        public string RoomId { get; }
        public string RoomName { get; }

        public event EventHandler<string>? OnGuestConnected;
        public event EventHandler<string>? OnGuestDisconnected;
        public event EventHandler<GuestPingArgs>? OnGuestPingUpdated;
        public event EventHandler<FlashCommand>? OnFlashScheduled;

        public SocketRoomHost(string roomName, string roomId) {
            RoomName = roomName;
            RoomId = roomId;

            _listener = new TcpListener(IPAddress.Any, TcpPort);
            _listener.Start();

            _ = AcceptLoopAsync(_cts.Token);
            _ = HeartbeatLoopAsync(_cts.Token);
        }

        public async Task FlashAsync(string action = "on", int leadMs = LeadMs) {
            long atUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + leadMs;
            var cmd = new FlashCommand(action, atUnixMs);
            var bytes = BuildMessage(new { Type = "FLASH", cmd.Action, cmd.AtUnixMs });

            OnFlashScheduled?.Invoke(this, cmd);

            var tasks = new List<Task>();
            foreach (var (key, state) in _guests)
                tasks.Add(SendAsync(key, state, bytes));
            await Task.WhenAll(tasks);
        }

        public int GuestCount => _guests.Count;

        public IReadOnlyList<(string Ip, int RttMs)> GetGuests() {
            var list = new List<(string, int)>(_guests.Count);
            foreach (var (ip, state) in _guests)
                list.Add((ip, state.LatestRttMs));
            return list;
        }

        private async Task HeartbeatLoopAsync(CancellationToken token) {
            while (!token.IsCancellationRequested) {
                try { await Task.Delay(PingIntervalMs, token); } catch (OperationCanceledException) { break; }

                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                foreach (var (ip, state) in _guests) {
                    if (now - state.LastPongAtMs > PingTimeoutMs) {
                        RemoveGuest(ip, state);
                        continue;
                    }
                    await SendAsync(ip, state, BuildMessage(new { Type = "PING", SentAtMs = now }));
                }
            }
        }

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
                } catch {
                }
            }
        }

        private async Task MonitorGuestAsync(string ip, GuestState state, CancellationToken token) {
            try {
                using var reader = new StreamReader(state.Client.GetStream(), Encoding.UTF8, leaveOpen: true);
                while (!token.IsCancellationRequested) {
                    string? line = await reader.ReadLineAsync(token);
                    if (line is null) break;
                    await HandleGuestMessageAsync(ip, state, line);
                }
            } catch {
            }

            RemoveGuest(ip, state);
        }

        private async Task HandleGuestMessageAsync(string ip, GuestState state, string json) {
            try {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (!root.TryGetProperty("Type", out var typeProp)) return;

                switch (typeProp.GetString()) {
                    case "PONG":
                        long sentAt = root.GetProperty("SentAtMs").GetInt64();
                        int rtt = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - sentAt);
                        state.LastPongAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        state.LatestRttMs = rtt;
                        OnGuestPingUpdated?.Invoke(this, new GuestPingArgs(ip, rtt));
                        break;
                    case "LEAVE":
                        RemoveGuest(ip, state);
                        break;
                }
            } catch {
            }
            await Task.CompletedTask;
        }

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

        public void Dispose() {
            var closeBytes = BuildMessage(new { Type = "CLOSE" });
            foreach (var (ip, state) in _guests)
                _ = SendAsync(ip, state, closeBytes);

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
