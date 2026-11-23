using System.Collections.Generic;
using DsDevelopment.FileZillaServer.AdminClient.Protocol;

namespace DsDevelopment.FileZillaServer.AdminClient.Models
{
    /// <summary>Information the server returns on a successful login.</summary>
    public class ServerInfo
    {
        /// <summary>Server version, e.g. <c>1.12.6</c>.</summary>
        public string ServerVersion { get; set; }

        /// <summary>Build target, e.g. <c>x86_64-w64-mingw32</c>.</summary>
        public string ServerHost { get; set; }

        /// <summary>True if the server uses Windows paths.</summary>
        public bool UsesWindowsPaths { get; set; }

        /// <summary>OS account the server runs as.</summary>
        public string ServiceAccount { get; set; }

        /// <summary>Whether the server can log users in as OS accounts.</summary>
        public bool CanImpersonate { get; set; }

        /// <summary>Linux only: whether ptrace scope is unsafe.</summary>
        public bool UnsafePtraceScope { get; set; }

        /// <summary>Network interfaces, or null if the server didn't report them.</summary>
        public List<NetworkInterfaceInfo> NetworkInterfaces { get; set; }

        /// <summary>True if any of the server's TLS certificates has expired.</summary>
        public bool SomeCertificatesHaveExpired { get; set; }

        /// <summary>Internal flag reported by the server.</summary>
        public bool AnyIsEquivalent { get; set; }

        /// <summary>Whether the FTP server has a host override for passive mode.</summary>
        public bool FtpHasHostOverride { get; set; }

        /// <summary>Active identity providers, e.g. <c>system</c>.</summary>
        public List<string> ActiveIdentityProviders { get; set; }

        /// <summary>Unique id of this server installation.</summary>
        public byte[] InstanceId { get; set; }

        /// <summary><c>fz::util::fs::path_format</c> value for Windows paths (0 is Unix).</summary>
        private const byte WindowsPathFormat = 1;

        internal static ServerInfo Read(RmpReader r) => new ServerInfo
        {
            UsesWindowsPaths = r.ReadByte() == WindowsPathFormat,
            UnsafePtraceScope = r.ReadBool(),
            CanImpersonate = r.ReadBool(),
            ServiceAccount = r.ReadString(),
            NetworkInterfaces = r.ReadBool() ? r.ReadList(NetworkInterfaceInfo.Read) : null,
            SomeCertificatesHaveExpired = r.ReadBool(),
            AnyIsEquivalent = r.ReadBool(),
            FtpHasHostOverride = r.ReadBool(),
            ActiveIdentityProviders = r.ReadStringList(),
            ServerVersion = r.ReadString(),
            ServerHost = r.ReadString(),
            InstanceId = r.ReadBytes(),
        };
    }

    /// <summary>A network interface on the server.</summary>
    public class NetworkInterfaceInfo
    {
        /// <summary>Interface name.</summary>
        public string Name { get; set; }
        /// <summary>Addresses in CIDR notation.</summary>
        public List<string> Addresses { get; set; }
        /// <summary>MAC address.</summary>
        public string Mac { get; set; }

        internal static NetworkInterfaceInfo Read(RmpReader r) => new NetworkInterfaceInfo
        {
            Name = r.ReadString(),
            Addresses = r.ReadStringList(),
            Mac = r.ReadString(),
        };
    }
}
