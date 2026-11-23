using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace DsDevelopment.FileZillaServer.AdminClient.Protocol
{
    /// <summary>
    /// TLS connection to the admin port, with message framing.
    /// Frame: u8 little-endian flag, u32 payload length, payload. Payload: u16 message index, message body.
    /// </summary>
    internal sealed class AdminConnection : IDisposable
    {
        private const int MaxFrameSize = 64 * 1024 * 1024;

        /// <summary>Values of the first byte of a frame, which gives the byte order of the length and payload.</summary>
        private const byte BigEndianFlag = 0, LittleEndianFlag = 1;

        /// <summary>Byte-order flag followed by the u32 payload length.</summary>
        private const int FrameHeaderSize = sizeof(byte) + sizeof(uint);

        private const int MessageIndexSize = sizeof(ushort);

        /// <summary>Lets the operating system pick the TLS version (1.2 or 1.3).</summary>
        private const SslProtocols OperatingSystemDefaultTls = SslProtocols.None;

        private readonly FileZillaAdminClientOptions _options;
        private TcpClient _tcp;
        private SslStream _ssl;

        public AdminConnection(FileZillaAdminClientOptions options)
        {
            _options = options;
        }

        public bool IsConnected => _ssl != null;

        public void Open()
        {
            if (_ssl != null)
                throw new InvalidOperationException("Already connected.");

            var timeout = (int)_options.Timeout.TotalMilliseconds;
            var tcp = new TcpClient { ReceiveTimeout = timeout, SendTimeout = timeout };
            try
            {
                if (!tcp.ConnectAsync(_options.Host, _options.Port).Wait(timeout))
                    throw new AdminClientException($"Timed out connecting to {_options.Host}:{_options.Port}.");

                var ssl = new SslStream(tcp.GetStream(), false, ValidateCertificate);
#if NET6_0_OR_GREATER
                var protocols = OperatingSystemDefaultTls;
#else
                var protocols = SslProtocols.Tls12;
#endif
                try
                {
                    ssl.AuthenticateAsClient(_options.Host, null, protocols, false);
                }
                catch (AuthenticationException ex)
                {
                    ssl.Dispose();
                    throw new AdminClientException(
                        "TLS handshake with the FileZilla Server admin interface failed. " +
                        "If the certificate was rejected, check CertificateFingerprint against the one shown in the server's administration settings.", ex);
                }

                _tcp = tcp;
                _ssl = ssl;
            }
            catch
            {
                tcp.Dispose();
                throw;
            }
        }

        private bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            if (_options.ServerCertificateValidation != null)
                return _options.ServerCertificateValidation(certificate, chain, errors);

            if (certificate == null || string.IsNullOrWhiteSpace(_options.CertificateFingerprint))
                return false;

            byte[] actual;
            using (var sha = SHA256.Create())
                actual = sha.ComputeHash(certificate.GetRawCertData());

            return string.Equals(ToHex(actual), NormalizeFingerprint(_options.CertificateFingerprint), StringComparison.Ordinal);
        }

        internal static string NormalizeFingerprint(string fingerprint)
            => fingerprint.Replace(":", "").Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();

        internal static string ToHex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "");

        public void Send(ushort messageIndex, byte[] body)
        {
            var stream = Stream;
            var w = new RmpWriter();
            w.WriteByte(LittleEndianFlag);
            w.WriteUInt32((uint)(MessageIndexSize + body.Length));
            w.WriteUInt16(messageIndex);
            w.WriteRaw(body);
            var frame = w.ToArray();

            stream.Write(frame, 0, frame.Length);
            stream.Flush();
        }

        /// <summary>
        /// Reads frames until one with <paramref name="expectedIndex"/> arrives. Unsolicited messages
        /// (log lines, session events, update info, ...) are skipped.
        /// </summary>
        /// <returns>A reader positioned at the start of the message body.</returns>
        public RmpReader Receive(ushort expectedIndex)
        {
            while (true)
            {
                var header = ReadExact(FrameHeaderSize);
                var bigEndian = header[0] == BigEndianFlag;
                var headerReader = new RmpReader(header, bigEndian);
                headerReader.ReadByte(); // the byte-order flag, already inspected
                var length = headerReader.ReadUInt32();
                if (length < MessageIndexSize || length > MaxFrameSize)
                    throw new ProtocolException($"Invalid frame length {length}.");

                var reader = new RmpReader(ReadExact((int)length), bigEndian);
                var index = reader.ReadUInt16();

                if (index == expectedIndex)
                    return reader;

                if (index == MessageIndex.Exception)
                    throw new ProtocolException(
                        $"The server rejected the request (exception message, {reader.Remaining} byte(s): {ToHex(reader.ReadFixed(Math.Min(reader.Remaining, 64)))}). " +
                        "This usually means the server speaks a different protocol version than this library.");
            }
        }

        private Stream Stream => _ssl ?? throw new InvalidOperationException("Not connected.");

        private byte[] ReadExact(int count)
        {
            var stream = Stream;
            var buffer = new byte[count];
            var offset = 0;
            while (offset < count)
            {
                int read;
                try
                {
                    read = stream.Read(buffer, offset, count - offset);
                }
                catch (IOException ex)
                {
                    throw new AdminClientException("Lost connection to the FileZilla Server admin interface.", ex);
                }
                if (read <= 0)
                    throw new AdminClientException("The FileZilla Server admin interface closed the connection.");
                offset += read;
            }
            return buffer;
        }

        public void Dispose()
        {
            _ssl?.Dispose();
            _tcp?.Dispose();
            _ssl = null;
            _tcp = null;
        }
    }
}
