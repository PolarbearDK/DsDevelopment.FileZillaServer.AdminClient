using DsDevelopment.FileZillaServer.AdminClient.Protocol;

namespace DsDevelopment.FileZillaServer.AdminClient.Models
{
    /// <summary>Access granted through a mount point.</summary>
    public enum MountAccess : byte
    {
        /// <summary>Read only.</summary>
        ReadOnly = 0,
        /// <summary>Read and write.</summary>
        ReadWrite = 1,
        /// <summary>Mount point is disabled (hides the path).</summary>
        Disabled = 2,
        /// <summary>Write only.</summary>
        WriteOnly = 3,
    }

    /// <summary>Whether permissions apply to subdirectories.</summary>
    public enum MountRecursion : byte
    {
        /// <summary>Permissions apply to the mount point only.</summary>
        NotRecursive = 0,
        /// <summary>Permissions apply to subdirectories too.</summary>
        Recursive = 1,
        /// <summary>Recursive, and subdirectories may be created, renamed and deleted.</summary>
        RecursiveWithStructureModification = 2,
    }

    /// <summary>Mount point flags.</summary>
    [System.Flags]
    public enum MountFlags : byte
    {
        /// <summary>No flags.</summary>
        None = 0,
        /// <summary>Create the native directory if it does not exist.</summary>
        AutoCreate = 1,
    }

    /// <summary>Maps a virtual path the FTP user sees to a directory on the server.</summary>
    public class MountPoint
    {
        /// <summary>Virtual path seen by the FTP user, e.g. <c>/</c> or <c>/data</c>.</summary>
        public string VirtualPath { get; set; } = "/";

        /// <summary>
        /// Directory on the server, e.g. <c>C:\Data</c>. May contain placeholders such as
        /// <c>%&lt;home&gt;</c> (the user's home directory) or <c>%u</c> (the user name).
        /// </summary>
        public string NativePath { get; set; } = string.Empty;

        /// <summary>Access granted.</summary>
        public MountAccess Access { get; set; } = MountAccess.ReadWrite;

        /// <summary>Whether permissions apply to subdirectories.</summary>
        public MountRecursion Recursion { get; set; } = MountRecursion.RecursiveWithStructureModification;

        /// <summary>Flags.</summary>
        public MountFlags Flags { get; set; }

        /// <summary>
        /// The same path using old-style placeholders (e.g. <c>:h</c>). The server keeps it for
        /// rollback compatibility; leave it empty for new mount points.
        /// </summary>
        public string LegacyNativePath { get; set; } = string.Empty;

        internal static MountPoint Read(RmpReader r) => new MountPoint
        {
            VirtualPath = r.ReadString(),
            Access = (MountAccess)r.ReadByte(),
            LegacyNativePath = r.ReadString(),
            NativePath = r.ReadString(),
            Recursion = (MountRecursion)r.ReadByte(),
            Flags = (MountFlags)r.ReadByte(),
        };

        internal void Write(RmpWriter w)
        {
            w.WriteString(VirtualPath);
            w.WriteByte((byte)Access);
            w.WriteString(LegacyNativePath);
            w.WriteString(NativePath);
            w.WriteByte((byte)Recursion);
            w.WriteByte((byte)Flags);
        }
    }
}
