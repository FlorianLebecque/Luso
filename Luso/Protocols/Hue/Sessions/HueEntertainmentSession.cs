#nullable enable
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Org.BouncyCastle.Tls;

namespace Luso.Features.Rooms.Networking.Hue
{
    // ── Minimal REST models ───────────────────────────────────────────────────

    internal sealed class EntertainmentConfigListResponse
    {
        [JsonPropertyName("data")]
        public List<EntertainmentConfig> Data { get; set; } = new();
    }

    internal sealed class EntertainmentConfig
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("metadata")]
        public EntertainmentMetadata? Metadata { get; set; }

        [JsonPropertyName("channels")]
        public List<EntertainmentChannel> Channels { get; set; } = new();

        [JsonPropertyName("status")]
        public string Status { get; set; } = "";
    }

    internal sealed class EntertainmentMetadata
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";
    }

    internal sealed class EntertainmentChannel
    {
        [JsonPropertyName("channel_id")]
        public int ChannelId { get; set; }
    }

    // ── Entertainment session ─────────────────────────────────────────────────

    /// <summary>
    /// Manages a single Hue Entertainment configuration:
    /// starts streaming mode via REST, performs a DTLS 1.2 PSK handshake on
    /// UDP port 2100, then streams colour frames at ~50 Hz.
    ///
    /// Each channel maps to a <see cref="HueEntertainmentTarget"/>. Calling
    /// <see cref="SetChannel"/> (from a target's ExecuteAsync) updates the in-memory
    /// state; the streaming loop picks it up on the next frame.
    /// </summary>
    internal sealed class HueEntertainmentSession : IAsyncDisposable
    {
        private const int StreamPort = 2100;
        private const int FrameMs = 20; // ~50 Hz

        private readonly string _bridgeIp;
        private readonly string _apiKey;
        private readonly string _configId;
        private readonly int[] _channelIds;
        private readonly bool[] _channelOn;  // indexed parallel to _channelIds

        private Socket? _socket;
        private DtlsTransport? _dtls;
        private CancellationTokenSource? _cts;
        private Task? _streamTask;
        private byte _seq;

        private HueEntertainmentSession(string ip, string apiKey, string configId, int[] channelIds)
        {
            _bridgeIp = ip;
            _apiKey = apiKey;
            _configId = configId;
            _channelIds = channelIds;
            _channelOn = new bool[channelIds.Length];
        }

        // ── Factory ───────────────────────────────────────────────────────────

        /// <summary>
        /// Fetches all entertainment configurations from the bridge and creates one
        /// <see cref="HueEntertainmentSession"/> per configuration.
        /// Returns an empty list if the bridge has no configurations or DTLS fails.
        /// </summary>
        public static async Task<IReadOnlyList<HueEntertainmentSession>> CreateAllAsync(
            string ip, string apiKey, string clientKey, string appId)
        {
            var configs = await FetchConfigsAsync(ip, apiKey).ConfigureAwait(false);
            var sessions = new List<HueEntertainmentSession>();

            foreach (var cfg in configs)
            {
                var ids = cfg.Channels.Select(c => c.ChannelId).ToArray();
                if (ids.Length == 0) continue;

                var session = new HueEntertainmentSession(ip, apiKey, cfg.Id, ids);
                if (await session.StartAsync(clientKey, appId).ConfigureAwait(false))
                    sessions.Add(session);
            }

            return sessions;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Configuration UUID (used by targets to identify their session).</summary>
        public string ConfigId => _configId;

        /// <summary>Channel IDs managed by this session.</summary>
        public IReadOnlyList<int> ChannelIds => _channelIds;

        /// <summary>
        /// Sets the colour state for <paramref name="channelId"/> to on (white) or off (black).
        /// Thread-safe; picked up on the next stream frame.
        /// </summary>
        public void SetChannel(int channelId, bool on)
        {
            int idx = Array.IndexOf(_channelIds, channelId);
            if (idx >= 0) _channelOn[idx] = on;
        }

        // ── REST helpers ──────────────────────────────────────────────────────

        private static readonly HttpClientHandler _insecureHandler = new()
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };
        private static readonly HttpClient _http = new(_insecureHandler);

        private static async Task<List<EntertainmentConfig>> FetchConfigsAsync(string ip, string apiKey)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get,
                    $"https://{ip}/clip/v2/resource/entertainment_configuration");
                req.Headers.Add("hue-application-key", apiKey);
                var resp = await _http.SendAsync(req).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return new();
                var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                var result = JsonSerializer.Deserialize<EntertainmentConfigListResponse>(json);
                return result?.Data ?? new();
            }
            catch { return new(); }
        }

        private async Task<bool> SetStreamingAction(string action)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Put,
                    $"https://{_bridgeIp}/clip/v2/resource/entertainment_configuration/{_configId}");
                req.Headers.Add("hue-application-key", _apiKey);
                req.Content = new StringContent($"{{\"action\":\"{action}\"}}", Encoding.UTF8, "application/json");
                var resp = await _http.SendAsync(req).ConfigureAwait(false);
                return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        // ── Startup ───────────────────────────────────────────────────────────

        private async Task<bool> StartAsync(string clientKey, string appId)
        {
            try
            {
                // 1. Activate streaming mode
                if (!await SetStreamingAction("start").ConfigureAwait(false)) return false;

                // 2. Decode PSK and identity
                var psk = Convert.FromHexString(clientKey);  // 32-char hex → 16 bytes
                var identity = Encoding.UTF8.GetBytes(appId);

                // 3. DTLS handshake on UDP 2100
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                _socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                var remote = new IPEndPoint(IPAddress.Parse(_bridgeIp), StreamPort);
                var transport = new UdpDatagramTransport(_socket, remote);
                var protocol = new DtlsClientProtocol();
                _dtls = protocol.Connect(new HuePskTlsClient(identity, psk), transport);

                // 4. Start streaming loop
                _cts = new CancellationTokenSource();
                _streamTask = Task.Run(async () => await StreamLoopAsync(_cts.Token));
                return true;
            }
            catch { return false; }
        }

        // ── Streaming loop ────────────────────────────────────────────────────

        private async Task StreamLoopAsync(CancellationToken ct)
        {
            // Pre-build the static portion of the message header (bytes 0-51).
            // Layout:
            //  [0-8]  "HueStream"
            //  [9]    0x02 (ver major)  [10] 0x00 (ver minor)
            //  [11]   sequence number (updated each frame)
            //  [12-13] reserved 0x00 0x00
            //  [14]   0x00 (color space: RGB)
            //  [15]   0x00 reserved
            //  [16-51] UUID ASCII (36 bytes)
            //  [52..] channel slots: 1+2+2+2 bytes each

            int msgLen = 52 + _channelIds.Length * 7;
            var msg = new byte[msgLen];

            "HueStream"u8.CopyTo(msg.AsSpan(0, 9));
            msg[9] = 0x02;
            msg[10] = 0x00;
            // msg[11] = seq — filled per frame
            // msg[12-13] = 0x00 already
            msg[14] = 0x00; // RGB color space
            // msg[15] = 0x00 already
            Encoding.ASCII.GetBytes(_configId).CopyTo(msg, 16); // 36-byte UUID at offset 16

            while (!ct.IsCancellationRequested)
            {
                var frameStart = DateTime.UtcNow;

                msg[11] = _seq++;

                int offset = 52;
                for (int i = 0; i < _channelIds.Length; i++)
                {
                    bool on = _channelOn[i];
                    byte v = on ? (byte)0xFF : (byte)0x00;
                    msg[offset++] = (byte)_channelIds[i]; // channel ID
                    msg[offset++] = v; msg[offset++] = v; // R (16-bit)
                    msg[offset++] = v; msg[offset++] = v; // G (16-bit)
                    msg[offset++] = v; msg[offset++] = v; // B (16-bit)
                }

                try { _dtls!.Send(msg, 0, msgLen); }
                catch { break; }

                var elapsed = (DateTime.UtcNow - frameStart).TotalMilliseconds;
                var delay = FrameMs - (int)elapsed;
                if (delay > 0)
                {
                    try { await Task.Delay(delay, ct).ConfigureAwait(false); }
                    catch (OperationCanceledException) { break; }
                }
            }
        }

        // ── Dispose ───────────────────────────────────────────────────────────

        public async ValueTask DisposeAsync()
        {
            _cts?.Cancel();
            if (_streamTask is not null)
                try { await _streamTask.ConfigureAwait(false); } catch { }

            try { await SetStreamingAction("stop").ConfigureAwait(false); } catch { }

            _dtls?.Close();
            _socket?.Close();
        }
    }
}
