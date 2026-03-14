#nullable enable
using System.Formats.Cbor;
using SyncoStronbo.Features.Rooms.Domain;

namespace SyncoStronbo.Features.Rooms.Networking {
    /// <summary>
    /// SSP/1.0 — Message builders and map parser for the SyncoStronbo Session Protocol.
    ///
    /// Encoding: CBOR maps (RFC 7049).
    /// TCP framing: CBOR Sequences (RFC 8742) — back-to-back CBOR items, no separator.
    /// All messages start with key "t" carrying a 4-char ASCII type tag.
    /// </summary>
    internal static class SspCbor {

        // ── Message builders ──────────────────────────────────────────────────

        /// <summary>ANNC — UDP broadcast room announcement.</summary>
        public static byte[] Annc(string roomId, string roomName, string hostIp, int tcpPort) {
            var w = new CborWriter();
            w.WriteStartMap(5);
            w.WriteTextString("t");  w.WriteTextString("ANNC");
            w.WriteTextString("id"); w.WriteTextString(roomId);
            w.WriteTextString("nm"); w.WriteTextString(roomName);
            w.WriteTextString("ip"); w.WriteTextString(hostIp);
            w.WriteTextString("pt"); w.WriteUInt32((uint)tcpPort);
            w.WriteEndMap();
            return w.Encode();
        }

        /// <summary>JOIN — Guest join request with capabilities (G→H).</summary>
        public static byte[] Join(string deviceName, GuestCapabilities cap) {
            var w = new CborWriter();
            w.WriteStartMap(3);
            w.WriteTextString("t");   w.WriteTextString("JOIN");
            w.WriteTextString("nm");  w.WriteTextString(deviceName);
            w.WriteTextString("cap"); WriteCap(w, cap);
            w.WriteEndMap();
            return w.Encode();
        }

        /// <summary>JACK — Join acknowledged (H→G).</summary>
        public static byte[] Jack(string roomName, string roomId) {
            var w = new CborWriter();
            w.WriteStartMap(3);
            w.WriteTextString("t");  w.WriteTextString("JACK");
            w.WriteTextString("nm"); w.WriteTextString(roomName);
            w.WriteTextString("id"); w.WriteTextString(roomId);
            w.WriteEndMap();
            return w.Encode();
        }

        /// <summary>PING — Heartbeat probe (H→G), carries sent timestamp.</summary>
        public static byte[] Ping(long sentAtMs) {
            var w = new CborWriter();
            w.WriteStartMap(2);
            w.WriteTextString("t");  w.WriteTextString("PING");
            w.WriteTextString("ms"); w.WriteUInt64((ulong)sentAtMs);
            w.WriteEndMap();
            return w.Encode();
        }

        /// <summary>PONG — Heartbeat reply (G→H), echoes PING.ms for RTT calculation.</summary>
        public static byte[] Pong(ulong echoMs) {
            var w = new CborWriter();
            w.WriteStartMap(2);
            w.WriteTextString("t");  w.WriteTextString("PONG");
            w.WriteTextString("ms"); w.WriteUInt64(echoMs);
            w.WriteEndMap();
            return w.Encode();
        }

        /// <summary>FLSH — Scheduled effect command (H→G all guests simultaneously).</summary>
        public static byte[] Flsh(string action, long atUnixMs) {
            var w = new CborWriter();
            w.WriteStartMap(3);
            w.WriteTextString("t");  w.WriteTextString("FLSH");
            w.WriteTextString("ac"); w.WriteTextString(action);
            w.WriteTextString("at"); w.WriteUInt64((ulong)atUnixMs);
            w.WriteEndMap();
            return w.Encode();
        }

        /// <summary>CLOS — Host gracefully closes room (H→G all guests).</summary>
        public static byte[] Clos() {
            var w = new CborWriter();
            w.WriteStartMap(1);
            w.WriteTextString("t"); w.WriteTextString("CLOS");
            w.WriteEndMap();
            return w.Encode();
        }

        /// <summary>LEAV — Guest voluntarily leaves (G→H).</summary>
        public static byte[] Leav() {
            var w = new CborWriter();
            w.WriteStartMap(1);
            w.WriteTextString("t"); w.WriteTextString("LEAV");
            w.WriteEndMap();
            return w.Encode();
        }

        // ── Message parsing ───────────────────────────────────────────────────

        /// <summary>
        /// Parses a CBOR map into a flat string→object dictionary.
        /// Nested maps are returned as nested Dictionary&lt;string, object?&gt;.
        /// Unknown or unsupported CBOR types are stored as null.
        /// </summary>
        public static Dictionary<string, object?> ParseMap(ReadOnlyMemory<byte> data) {
            var r = new CborReader(data, allowMultipleRootLevelValues: true);
            return ReadMap(r);
        }

        /// <summary>Gets the type tag "t" from a parsed message dictionary.</summary>
        public static string Tag(Dictionary<string, object?> msg)
            => msg.TryGetValue("t", out var v) && v is string s ? s : string.Empty;

        /// <summary>Parses the nested "cap" map from a JOIN message into GuestCapabilities.</summary>
        public static GuestCapabilities ParseCap(object? capObj) {
            if (capObj is not Dictionary<string, object?> cap)
                return GuestCapabilities.Unknown;

            return new GuestCapabilities(
                HasFlashlight: cap.TryGetValue("fl", out var fl) && fl is bool bfl && bfl,
                HasVibration:  cap.TryGetValue("vb", out var vb) && vb is bool bvb && bvb,
                HasScreen:     cap.TryGetValue("sc", out var sc) && sc is bool bsc && bsc,
                ScreenWidth:   cap.TryGetValue("sw", out var sw) ? ToInt(sw) : 0,
                ScreenHeight:  cap.TryGetValue("sh", out var sh) ? ToInt(sh) : 0
            );
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static void WriteCap(CborWriter w, GuestCapabilities cap) {
            w.WriteStartMap(5);
            w.WriteTextString("fl"); w.WriteBoolean(cap.HasFlashlight);
            w.WriteTextString("vb"); w.WriteBoolean(cap.HasVibration);
            w.WriteTextString("sc"); w.WriteBoolean(cap.HasScreen);
            w.WriteTextString("sw"); w.WriteUInt32((uint)cap.ScreenWidth);
            w.WriteTextString("sh"); w.WriteUInt32((uint)cap.ScreenHeight);
            w.WriteEndMap();
        }

        private static Dictionary<string, object?> ReadMap(CborReader r) {
            var dict = new Dictionary<string, object?>(8);
            int? count = r.ReadStartMap();
            int read = 0;
            while (count == null ? r.PeekState() != CborReaderState.EndMap : read < count) {
                string key = r.ReadTextString();
                dict[key] = ReadValue(r);
                read++;
            }
            r.ReadEndMap();
            return dict;
        }

        private static object? ReadValue(CborReader r) {
            return r.PeekState() switch {
                CborReaderState.TextString     => r.ReadTextString(),
                CborReaderState.UnsignedInteger => r.ReadUInt64(),
                CborReaderState.NegativeInteger => r.ReadInt64(),
                CborReaderState.Boolean         => r.ReadBoolean(),
                CborReaderState.StartMap        => ReadMap(r),
                _                               => SkipAndReturn(r)
            };
        }

        private static object? SkipAndReturn(CborReader r) { r.SkipValue(); return null; }

        private static int ToInt(object? v) => v switch {
            ulong u => (int)u,
            long  l => (int)l,
            _       => 0
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CBOR Sequence stream reader
    // Reads one complete CBOR item at a time from a NetworkStream.
    // ─────────────────────────────────────────────────────────────────────────

    internal sealed class CborStreamReader {
        private readonly Stream _stream;
        private byte[] _buf;
        private int _start;
        private int _end;

        public CborStreamReader(Stream stream, int initialCapacity = 4096) {
            _stream = stream;
            _buf    = new byte[initialCapacity];
        }

        /// <summary>
        /// Returns the raw bytes of the next complete CBOR item from the stream.
        /// Returns null when the connection is cleanly closed (0-byte read).
        /// Throws on cancellation or unrecoverable stream errors.
        /// </summary>
        public async Task<ReadOnlyMemory<byte>?> ReadNextAsync(CancellationToken ct = default) {
            while (true) {
                // Try to locate a complete CBOR item in the buffer.
                if (_end > _start) {
                    var window = new ReadOnlyMemory<byte>(_buf, _start, _end - _start);
                    if (TryConsume(window, out int consumed, out var item)) {
                        _start += consumed;
                        return item;
                    }
                }

                // Compact the buffer to make room for more incoming bytes.
                if (_start > 0) {
                    int remaining = _end - _start;
                    if (remaining > 0)
                        Array.Copy(_buf, _start, _buf, 0, remaining);
                    _end   = remaining;
                    _start = 0;
                }

                // Grow if the buffer is still full after compaction.
                if (_end == _buf.Length) {
                    var grown = new byte[_buf.Length * 2];
                    Array.Copy(_buf, grown, _end);
                    _buf = grown;
                }

                int n = await _stream.ReadAsync(_buf.AsMemory(_end, _buf.Length - _end), ct);
                if (n == 0) return null; // connection closed
                _end += n;
            }
        }

        /// <summary>Tests whether the buffer starts with a complete CBOR item.</summary>
        private static bool TryConsume(
            ReadOnlyMemory<byte> buf,
            out int consumed,
            out ReadOnlyMemory<byte> item) {
            consumed = 0;
            item     = default;
            try {
                var r = new CborReader(buf, allowMultipleRootLevelValues: true);
                r.SkipValue();
                consumed = buf.Length - r.BytesRemaining;
                item     = buf[..consumed];
                return true;
            } catch {
                // Incomplete or malformed — need more bytes.
                return false;
            }
        }
    }
}
