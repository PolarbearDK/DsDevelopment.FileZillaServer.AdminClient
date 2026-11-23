using System.Collections.Generic;
using System.Linq;
using DsDevelopment.FileZillaServer.AdminClient.Protocol;

namespace DsDevelopment.FileZillaServer.AdminClient.Models
{
    /// <summary>
    /// All groups and users on the server, plus server-wide user settings.
    /// Get it with <see cref="FileZillaAdminClient.GetGroupsAndUsers"/>, modify it, and send it back with
    /// <see cref="FileZillaAdminClient.SetGroupsAndUsers"/>, which replaces everything on the server.
    /// </summary>
    public class GroupsAndUsers
    {
        /// <summary>Groups.</summary>
        public List<Group> Groups { get; set; } = new List<Group>();

        /// <summary>Users. Includes the built-in <c>&lt;system user&gt;</c> entry.</summary>
        public List<User> Users { get; set; } = new List<User>();

        /// <summary>OS account used to access files when a user doesn't log in as an OS account.</summary>
        public Impersonator DefaultImpersonator { get; set; } = new WindowsImpersonator();

        /// <summary>Default realm policy when users and groups defer.</summary>
        public List<Realm> Realms { get; set; } = new List<Realm>();

        /// <summary>Credentials for network shares used in mount points (Windows UNC paths).</summary>
        public List<MountCredential> MountCredentials { get; set; } = new List<MountCredential>();

        /// <summary>Finds a user by name (case-sensitive), or null.</summary>
        public User FindUser(string name) => Users.FirstOrDefault(u => u.Name == name);

        /// <summary>Finds a group by name (case-sensitive), or null.</summary>
        public Group FindGroup(string name) => Groups.FirstOrDefault(g => g.Name == name);

        internal static GroupsAndUsers Read(RmpReader r)
        {
            var result = new GroupsAndUsers { Groups = r.ReadList(Group.Read) };

            // fz::authentication::file_based_authenticator::users
            result.DefaultImpersonator = Impersonator.Read(r);
            result.Realms = r.ReadList(Realm.Read);
            result.MountCredentials = r.ReadList(MountCredential.Read);
            result.Users = r.ReadList(User.Read);
            return result;
        }

        internal void Write(RmpWriter w)
        {
            w.WriteList(Groups, (x, g) => g.Write(x));
            (DefaultImpersonator ?? new WindowsImpersonator()).Write(w);
            w.WriteList(Realms, (x, realm) => realm.Write(x));
            w.WriteList(MountCredentials, (x, c) => c.Write(x));
            w.WriteList(Users, (x, u) => u.Write(x));
        }
    }

    /// <summary>OS account the server uses for file access.</summary>
    public abstract class Impersonator
    {
        /// <summary>Whether this impersonator is used.</summary>
        public bool Enabled { get; set; }

        /// <summary>OS account name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Alternative order of <c>file_based_authenticator::impersonator::any</c>.</summary>
        private const int WindowsIndex = 0, UnixIndex = 1, AlternativeCount = 2;

        internal static Impersonator Read(RmpReader r)
        {
            if (r.ReadVariantIndex(AlternativeCount) == WindowsIndex)
                return new WindowsImpersonator { Enabled = r.ReadBool(), Name = r.ReadString(), Password = r.ReadString() };
            return new UnixImpersonator { Enabled = r.ReadBool(), Name = r.ReadString(), Group = r.ReadString() };
        }

        internal void Write(RmpWriter w)
        {
            w.WriteVariantIndex(this is WindowsImpersonator ? WindowsIndex : UnixIndex);
            w.WriteBool(Enabled);
            w.WriteString(Name);
            w.WriteString(this is WindowsImpersonator win ? win.Password : ((UnixImpersonator)this).Group);
        }
    }

    /// <summary>Windows account used for file access.</summary>
    public class WindowsImpersonator : Impersonator
    {
        /// <summary>Account password (sent to the server in clear text, over TLS).</summary>
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>Unix account used for file access.</summary>
    public class UnixImpersonator : Impersonator
    {
        /// <summary>Unix group.</summary>
        public string Group { get; set; } = string.Empty;
    }

    /// <summary>Credentials for a network path used in mount points.</summary>
    public class MountCredential
    {
        /// <summary>Native path, e.g. <c>\\server\share</c>.</summary>
        public string NativePath { get; set; } = string.Empty;
        /// <summary>User name.</summary>
        public string Username { get; set; } = string.Empty;
        /// <summary>Password.</summary>
        public string Password { get; set; } = string.Empty;

        internal static MountCredential Read(RmpReader r) => new MountCredential
        {
            NativePath = r.ReadString(),
            Username = r.ReadString(),
            Password = r.ReadString(),
        };

        internal void Write(RmpWriter w)
        {
            w.WriteString(NativePath);
            w.WriteString(Username);
            w.WriteString(Password);
        }
    }
}
