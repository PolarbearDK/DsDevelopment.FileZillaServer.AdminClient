using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using DsDevelopment.FileZillaServer.AdminClient.Protocol;

namespace DsDevelopment.FileZillaServer.AdminClient.Models
{
    /// <summary>Speed limits in bytes per second.</summary>
    public class RateLimits
    {
        /// <summary>Value meaning "no limit".</summary>
        public const ulong Unlimited = ulong.MaxValue;

        /// <summary>Total upload speed for all sessions of this user/group.</summary>
        public ulong Inbound { get; set; } = Unlimited;
        /// <summary>Total download speed for all sessions of this user/group.</summary>
        public ulong Outbound { get; set; } = Unlimited;
        /// <summary>Upload speed per session.</summary>
        public ulong SessionInbound { get; set; } = Unlimited;
        /// <summary>Download speed per session.</summary>
        public ulong SessionOutbound { get; set; } = Unlimited;

        internal static RateLimits Read(RmpReader r) => new RateLimits
        {
            Inbound = r.ReadUInt64(),
            Outbound = r.ReadUInt64(),
            SessionInbound = r.ReadUInt64(),
            SessionOutbound = r.ReadUInt64(),
        };

        internal void Write(RmpWriter w)
        {
            w.WriteUInt64(Inbound);
            w.WriteUInt64(Outbound);
            w.WriteUInt64(SessionInbound);
            w.WriteUInt64(SessionOutbound);
        }
    }

    /// <summary>Limits on files and directories open at the same time per session. 0 means unlimited.</summary>
    public class OpenLimits
    {
        /// <summary>Maximum open files, 0 = unlimited.</summary>
        public ushort Files { get; set; }
        /// <summary>Maximum open directories, 0 = unlimited.</summary>
        public ushort Directories { get; set; }

        internal static OpenLimits Read(RmpReader r) => new OpenLimits { Files = r.ReadUInt16(), Directories = r.ReadUInt16() };

        internal void Write(RmpWriter w)
        {
            w.WriteUInt16(Files);
            w.WriteUInt16(Directories);
        }
    }

    /// <summary>An inclusive range of IP addresses of one family.</summary>
    public class IpRange
    {
        /// <summary>First address.</summary>
        public IPAddress From { get; set; }
        /// <summary>Last address (equal to <see cref="From"/> for a single address).</summary>
        public IPAddress To { get; set; }

        /// <summary>Creates an empty range.</summary>
        public IpRange() { }

        /// <summary>Creates a range from two addresses of the same family.</summary>
        public IpRange(IPAddress from, IPAddress to)
        {
            From = from;
            To = to;
        }

        /// <summary>Creates a single-address range.</summary>
        public IpRange(IPAddress address) : this(address, address) { }

        /// <inheritdoc />
        public override string ToString() => Equals(From, To) ? $"{From}" : $"{From}-{To}";
    }

    /// <summary>
    /// List of IP ranges. On the wire IPv4 and IPv6 ranges are stored separately;
    /// reading yields IPv4 ranges first, then IPv6.
    /// </summary>
    public class IpRangeList : List<IpRange>
    {
        private const int IPv4Size = 4, IPv6Size = 16;

        internal static IpRangeList Read(RmpReader r)
        {
            var list = new IpRangeList();
            list.AddRange(r.ReadList(x => ReadRange(x, IPv4Size)));
            list.AddRange(r.ReadList(x => ReadRange(x, IPv6Size)));
            return list;
        }

        private static IpRange ReadRange(RmpReader r, int size)
            => new IpRange(new IPAddress(r.ReadFixed(size)), new IPAddress(r.ReadFixed(size)));

        internal void Write(RmpWriter w)
        {
            var v4 = FindAll(x => x.From.AddressFamily == AddressFamily.InterNetwork);
            var v6 = FindAll(x => x.From.AddressFamily == AddressFamily.InterNetworkV6);
            if (v4.Count + v6.Count != Count)
                throw new AdminClientException("IP ranges must be IPv4 or IPv6.");

            w.WriteList(v4, (x, range) => WriteRange(x, range, AddressFamily.InterNetwork, IPv4Size));
            w.WriteList(v6, (x, range) => WriteRange(x, range, AddressFamily.InterNetworkV6, IPv6Size));
        }

        private static void WriteRange(RmpWriter w, IpRange range, AddressFamily family, int size)
        {
            if (range.To == null || range.To.AddressFamily != family)
                throw new AdminClientException($"IP range {range} mixes address families.");
            w.WriteFixed(range.From.GetAddressBytes(), size);
            w.WriteFixed(range.To.GetAddressBytes(), size);
        }
    }
}
