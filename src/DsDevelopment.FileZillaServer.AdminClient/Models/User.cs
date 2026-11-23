using System;
using System.Collections.Generic;
using DsDevelopment.FileZillaServer.AdminClient.Protocol;

namespace DsDevelopment.FileZillaServer.AdminClient.Models
{
    /// <summary>A way of logging in. A user can log in with any one of its method sets.</summary>
    [Flags]
    public enum AuthenticationMethods : ulong
    {
        /// <summary>No credentials required.</summary>
        None = 0,
        /// <summary>Password.</summary>
        Password = 1,
        /// <summary>Token (used by the WebUI).</summary>
        Token = 2,
    }

    /// <summary>A user that can log in to the FTP server.</summary>
    public class User : AuthEntry
    {
        /// <summary>Whether the user may log in.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Names of the groups the user is a member of. Order matters: earlier groups take precedence.</summary>
        public List<string> Groups { get; set; } = new List<string>();

        /// <summary>
        /// When not null, the user logs in as the operating-system account with the same name
        /// (password checked by the OS) instead of using <see cref="Password"/>.
        /// </summary>
        public SystemImpersonation SystemImpersonation { get; set; }

        /// <summary>The stored password. Ignored when <see cref="SystemImpersonation"/> is set.</summary>
        public Password Password { get; set; } = new NoPassword();

        /// <summary>
        /// Accepted login methods. Each entry is one alternative; e.g. a single
        /// <see cref="AuthenticationMethods.Password"/> entry means "password required".
        /// </summary>
        public List<AuthenticationMethods> Methods { get; set; } = new List<AuthenticationMethods> { AuthenticationMethods.Password };

        /// <summary>External identity provider binding, or null.</summary>
        public IdentityProfile Identity { get; set; }

        /// <summary>Sets <see cref="Password"/> from clear text (PBKDF2-HMAC-SHA256, random salt).</summary>
        public void SetPassword(string clearText)
        {
            SystemImpersonation = null;
            Password = Password.Create(clearText);
        }

        internal static User Read(RmpReader r)
        {
            var user = new User { Name = r.ReadString(), Enabled = r.ReadBool() };
            user.ReadAuthEntry(r);
            user.Groups = r.ReadStringList();

            // fz::authentication::password::with_impersonation
            user.SystemImpersonation = r.ReadOptional(x => new SystemImpersonation { LoginOnly = x.ReadBool() });
            user.Password = user.SystemImpersonation == null ? Password.Read(r) : new NoPassword();

            user.Methods = r.ReadList(x => (AuthenticationMethods)x.ReadUInt64());
            user.Identity = r.ReadOptional(IdentityProfile.Read);
            return user;
        }

        internal void Write(RmpWriter w)
        {
            w.WriteString(Name);
            w.WriteBool(Enabled);
            WriteAuthEntry(w);
            w.WriteStringList(Groups);

            w.WriteOptional(SystemImpersonation, (x, s) => x.WriteBool(s.LoginOnly));
            if (SystemImpersonation == null)
                (Password ?? new NoPassword()).Write(w);

            w.WriteList(Methods, (x, m) => x.WriteUInt64((ulong)m));
            w.WriteOptional(Identity, (x, i) => i.Write(x));
        }
    }

    /// <summary>User logs in with the credentials of the operating-system account of the same name.</summary>
    public class SystemImpersonation
    {
        /// <summary>
        /// If true, the OS account is only used to check the password; file access still uses
        /// the server's own account.
        /// </summary>
        public bool LoginOnly { get; set; }
    }

    /// <summary>Binds a user to an identity provider (e.g. <c>system</c>).</summary>
    public class IdentityProfile
    {
        /// <summary>Identity provider name.</summary>
        public string Idp { get; set; } = string.Empty;

        /// <summary>User id at the provider; empty = same as the user name.</summary>
        public string Uid { get; set; } = string.Empty;

        /// <summary>Provider-specific parameters.</summary>
        public List<IdentityParameter> Parameters { get; set; } = new List<IdentityParameter>();

        internal static IdentityProfile Read(RmpReader r) => new IdentityProfile
        {
            Idp = r.ReadString(),
            Uid = r.ReadString(),
            Parameters = r.ReadList(x => new IdentityParameter { Type = x.ReadString(), Value = x.ReadString() }),
        };

        internal void Write(RmpWriter w)
        {
            w.WriteString(Idp);
            w.WriteString(Uid);
            w.WriteList(Parameters, (x, p) =>
            {
                x.WriteString(p.Type);
                x.WriteString(p.Value);
            });
        }
    }

    /// <summary>An identity provider parameter.</summary>
    public class IdentityParameter
    {
        /// <summary>Parameter type.</summary>
        public string Type { get; set; } = string.Empty;
        /// <summary>Parameter value.</summary>
        public string Value { get; set; } = string.Empty;
    }
}
