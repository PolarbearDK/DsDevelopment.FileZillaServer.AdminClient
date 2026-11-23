using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace DsDevelopment.FileZillaServer.AdminClient
{
    /// <summary>Connection settings for <see cref="FileZillaAdminClient"/>.</summary>
    public class FileZillaAdminClientOptions
    {
        /// <summary>Default admin port of FileZilla Server 1.x.</summary>
        public const int DefaultPort = 14148;

        /// <summary>Server host name or IP address. Default <c>127.0.0.1</c>.</summary>
        public string Host { get; set; } = "127.0.0.1";

        /// <summary>Admin port. Default 14148.</summary>
        public int Port { get; set; } = DefaultPort;

        /// <summary>
        /// SHA-256 fingerprint of the admin TLS certificate, as shown in the server's administration
        /// settings (e.g. <c>9d:2e:bd:…</c>; colons, dashes and case are ignored). The admin certificate is
        /// normally self-signed, so the connection is only accepted if this matches.
        /// </summary>
        public string CertificateFingerprint { get; set; }

        /// <summary>
        /// Custom certificate validation. When set, it is used instead of <see cref="CertificateFingerprint"/>.
        /// </summary>
        public Func<X509Certificate, X509Chain, SslPolicyErrors, bool> ServerCertificateValidation { get; set; }

        /// <summary>Timeout for connecting, sending and receiving. Default 30 seconds.</summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}
