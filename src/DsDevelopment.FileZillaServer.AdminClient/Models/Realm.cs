using DsDevelopment.FileZillaServer.AdminClient.Protocol;

namespace DsDevelopment.FileZillaServer.AdminClient.Models
{
    /// <summary>Whether a protocol realm is allowed.</summary>
    public enum RealmStatus : byte
    {
        /// <summary>Inherit from group / server default.</summary>
        Deferred = 0,
        /// <summary>Allowed.</summary>
        Enabled = 1,
        /// <summary>Not allowed.</summary>
        Disabled = 2,
    }

    /// <summary>
    /// A protocol realm such as <c>ftp</c> (plain FTP) or <c>ftps</c> (FTP over TLS),
    /// with whether it is allowed.
    /// </summary>
    public class Realm
    {
        /// <summary>Realm name, e.g. <c>ftp</c> or <c>ftps</c>.</summary>
        public string Name { get; set; }

        /// <summary>Status.</summary>
        public RealmStatus Status { get; set; }

        /// <summary>Creates an empty realm.</summary>
        public Realm() { }

        /// <summary>Creates a realm.</summary>
        public Realm(string name, RealmStatus status)
        {
            Name = name;
            Status = status;
        }

        internal static Realm Read(RmpReader r) => new Realm(r.ReadString(), (RealmStatus)r.ReadByte());

        internal void Write(RmpWriter w)
        {
            w.WriteString(Name);
            w.WriteByte((byte)Status);
        }
    }
}
