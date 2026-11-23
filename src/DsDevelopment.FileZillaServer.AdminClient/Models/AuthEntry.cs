using System.Collections.Generic;
using DsDevelopment.FileZillaServer.AdminClient.Protocol;

namespace DsDevelopment.FileZillaServer.AdminClient.Models
{
    /// <summary>Settings shared by users and groups.</summary>
    public abstract class AuthEntry
    {
        /// <summary>Name of the user or group.</summary>
        public string Name { get; set; }

        /// <summary>Virtual file system: which server directories the FTP user sees, and where.</summary>
        public List<MountPoint> MountPoints { get; set; } = new List<MountPoint>();

        /// <summary>Speed limits.</summary>
        public RateLimits RateLimits { get; set; } = new RateLimits();

        /// <summary>Addresses allowed to connect even if they match <see cref="DisallowedIps"/>.</summary>
        public IpRangeList AllowedIps { get; set; } = new IpRangeList();

        /// <summary>Addresses not allowed to connect.</summary>
        public IpRangeList DisallowedIps { get; set; } = new IpRangeList();

        /// <summary>Per-session limits on open files and directories.</summary>
        public OpenLimits SessionOpenLimits { get; set; } = new OpenLimits();

        /// <summary>Maximum concurrent sessions, 0 = unlimited.</summary>
        public ushort SessionCountLimit { get; set; }

        /// <summary>Free-text description.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Protocol realm overrides (e.g. disallow plain <c>ftp</c>). Empty = inherit.</summary>
        public List<Realm> Realms { get; set; } = new List<Realm>();

        internal void ReadAuthEntry(RmpReader r)
        {
            MountPoints = r.ReadList(MountPoint.Read);
            RateLimits = RateLimits.Read(r);
            AllowedIps = IpRangeList.Read(r);
            DisallowedIps = IpRangeList.Read(r);
            SessionOpenLimits = OpenLimits.Read(r);
            SessionCountLimit = r.ReadUInt16();
            Description = r.ReadString();
            Realms = r.ReadList(Realm.Read);
        }

        internal void WriteAuthEntry(RmpWriter w)
        {
            w.WriteList(MountPoints, (x, m) => m.Write(x));
            (RateLimits ?? new RateLimits()).Write(w);
            (AllowedIps ?? new IpRangeList()).Write(w);
            (DisallowedIps ?? new IpRangeList()).Write(w);
            (SessionOpenLimits ?? new OpenLimits()).Write(w);
            w.WriteUInt16(SessionCountLimit);
            w.WriteString(Description);
            w.WriteList(Realms, (x, realm) => realm.Write(x));
        }

        /// <inheritdoc />
        public override string ToString() => Name;
    }

    /// <summary>A group. Users inherit settings from the groups they are members of.</summary>
    public class Group : AuthEntry
    {
        internal static Group Read(RmpReader r)
        {
            var group = new Group { Name = r.ReadString() };
            group.ReadAuthEntry(r);
            return group;
        }

        internal void Write(RmpWriter w)
        {
            w.WriteString(Name);
            WriteAuthEntry(w);
        }
    }
}
