#nullable enable
using System.Net;
using System.Net.Sockets;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tls;
using Org.BouncyCastle.Tls.Crypto;
using Org.BouncyCastle.Tls.Crypto.Impl.BC;

namespace Luso.Features.Rooms.Networking.Hue
{
    /// <summary>
    /// DTLS 1.2 PSK client configured for Hue Entertainment API.
    /// Cipher: TLS_PSK_WITH_AES_128_GCM_SHA256 (0x00A8).
    /// PSK identity = hue-application-id; PSK = 16-byte binary from clientkey hex.
    /// </summary>
    internal sealed class HuePskTlsClient : PskTlsClient
    {
        public HuePskTlsClient(byte[] identity, byte[] psk)
            : base(new BcTlsCrypto(new SecureRandom()), new BasicTlsPskIdentity(identity, psk)) { }

        public override TlsSession? GetSessionToResume() => null;

        protected override ProtocolVersion[] GetSupportedVersions()
            => new[] { ProtocolVersion.DTLSv12 };

        public override int[] GetCipherSuites()
            => new[] { CipherSuite.TLS_PSK_WITH_AES_128_GCM_SHA256 };
    }

    /// <summary>
    /// Wraps a bound <see cref="Socket"/> as a BouncyCastle <see cref="DatagramTransport"/>
    /// for the DTLS handshake and send path.
    /// </summary>
    internal sealed class UdpDatagramTransport : DatagramTransport
    {
        private readonly Socket _socket;
        private readonly EndPoint _remote;

        internal UdpDatagramTransport(Socket socket, EndPoint remote)
        {
            _socket = socket;
            _remote = remote;
        }

        public int GetReceiveLimit() => 1500;
        public int GetSendLimit() => 1452;

        // Legacy byte-array overload (used during handshake by some BouncyCastle paths)
        public int Receive(byte[] buf, int off, int len, int waitMillis)
        {
            try
            {
                if (!_socket.Poll(Math.Max(1, waitMillis) * 1000, SelectMode.SelectRead))
                    return -1;
                EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                return _socket.ReceiveFrom(buf, off, len, SocketFlags.None, ref ep);
            }
            catch { return -1; }
        }

        // Span overload required by BouncyCastle 2.x DatagramReceiver
        public int Receive(Span<byte> buf, int waitMillis)
        {
            try
            {
                if (!_socket.Poll(Math.Max(1, waitMillis) * 1000, SelectMode.SelectRead))
                    return -1;
                var tmp = new byte[buf.Length];
                EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                int n = _socket.ReceiveFrom(tmp, 0, tmp.Length, SocketFlags.None, ref ep);
                tmp.AsSpan(0, n).CopyTo(buf);
                return n;
            }
            catch { return -1; }
        }

        // Legacy byte-array overload
        public void Send(byte[] buf, int off, int len)
            => _socket.SendTo(buf, off, len, SocketFlags.None, _remote);

        // Span overload required by BouncyCastle 2.x DatagramSender
        public void Send(ReadOnlySpan<byte> buf)
        {
            var tmp = buf.ToArray();
            _socket.SendTo(tmp, 0, tmp.Length, SocketFlags.None, _remote);
        }

        public void Close() { /* socket lifetime owned by HueEntertainmentSession */ }
    }
}
