using System;
using DsDevelopment.FileZillaServer.AdminClient.Models;
using DsDevelopment.FileZillaServer.AdminClient.Protocol;

namespace DsDevelopment.FileZillaServer.AdminClient
{
    /// <summary>
    /// Client for the administration interface of FileZilla Server 1.x.
    /// </summary>
    /// <remarks>
    /// FileZilla Server only accepts admin clients speaking exactly its own protocol version.
    /// This version of the library speaks protocol <see cref="SupportedProtocolVersion"/> (FileZilla Server 1.12.x).
    /// Not thread-safe: use one instance per thread, or synchronize.
    /// </remarks>
    /// <example>
    /// <code>
    /// using (var client = new FileZillaAdminClient(new FileZillaAdminClientOptions { CertificateFingerprint = "9d:2e:..." }))
    /// {
    ///     client.Connect("admin password");
    ///     var data = client.GetGroupsAndUsers();
    ///     var user = new User { Name = "alice", Groups = { "Customers" } };
    ///     user.SetPassword("secret");
    ///     user.MountPoints.Add(new MountPoint { VirtualPath = "/", NativePath = @"C:\Ftp\alice" });
    ///     data.Users.Add(user);
    ///     client.SetGroupsAndUsers(data);
    /// }
    /// </code>
    /// </example>
    public class FileZillaAdminClient : IDisposable
    {
        /// <summary>Admin protocol version this library speaks.</summary>
        public const uint SupportedProtocolVersion = MessageIndex.ProtocolVersion;

        /// <summary>Product flavour sent at login (1 = standard edition).</summary>
        private const byte StandardFlavour = 1;

        private readonly AdminConnection _connection;

        /// <summary>Creates a client. Call <see cref="Connect"/> before anything else.</summary>
        public FileZillaAdminClient(FileZillaAdminClientOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            _connection = new AdminConnection(options);
        }

        /// <summary>Connection settings.</summary>
        public FileZillaAdminClientOptions Options { get; }

        /// <summary>Information returned by the server at login, or null before <see cref="Connect"/>.</summary>
        public ServerInfo ServerInfo { get; private set; }

        /// <summary>Opens the TLS connection and logs in.</summary>
        /// <exception cref="LoginFailedException">Wrong password, or the server uses a different protocol version.</exception>
        public ServerInfo Connect(string password)
        {
            _connection.Open();
            try
            {
                var w = new RmpWriter();
                // admin_login is a versioned message: flavour + protocol version, then the password.
                w.WriteByte(StandardFlavour);
                w.WriteUInt32(MessageIndex.ProtocolVersion);
                w.WriteString(password ?? string.Empty);
                _connection.Send(MessageIndex.AdminLogin, w.ToArray());

                RmpReader r;
                try
                {
                    r = _connection.Receive(MessageIndex.AdminLoginResponse);
                }
                catch (ProtocolException ex)
                {
                    throw new LoginFailedException(
                        $"The server rejected the login message. It probably runs a FileZilla Server version that uses a different admin protocol than version {SupportedProtocolVersion} ({ex.Message})");
                }
                catch (AdminClientException ex) when (!(ex is LoginFailedException))
                {
                    throw new LoginFailedException(
                        $"The server closed the connection during login. It probably runs a FileZilla Server version that uses a different admin protocol than version {SupportedProtocolVersion}.");
                }

                var version = r.ReadUInt32();
                if (version != MessageIndex.ProtocolVersion)
                    throw new LoginFailedException($"Server answered with protocol version {version}; this library supports {SupportedProtocolVersion}.");

                if (!ReadSuccess(r))
                    throw new LoginFailedException("Login failed: wrong admin password.");

                ServerInfo = ServerInfo.Read(r);
                return ServerInfo;
            }
            catch
            {
                _connection.Dispose();
                throw;
            }
        }

        /// <summary>Gets all groups and users.</summary>
        public GroupsAndUsers GetGroupsAndUsers()
        {
            var r = Execute(MessageIndex.GetGroupsAndUsers, MessageIndex.GetGroupsAndUsersResponse, null);
            return GroupsAndUsers.Read(r);
        }

        /// <summary>
        /// Replaces <b>all</b> groups and users on the server with <paramref name="groupsAndUsers"/>.
        /// Start from <see cref="GetGroupsAndUsers"/> and modify it.
        /// </summary>
        /// <param name="groupsAndUsers">The complete set of groups and users.</param>
        /// <param name="save">True to save to the server's configuration file; false to change only the running server.</param>
        public void SetGroupsAndUsers(GroupsAndUsers groupsAndUsers, bool save = true)
        {
            if (groupsAndUsers == null)
                throw new ArgumentNullException(nameof(groupsAndUsers));

            Execute(MessageIndex.SetGroupsAndUsers, MessageIndex.SetGroupsAndUsersResponse, w =>
            {
                groupsAndUsers.Write(w);
                w.WriteBool(save);
            });
        }

        private RmpReader Execute(ushort command, ushort response, Action<RmpWriter> writeBody)
        {
            if (ServerInfo == null)
                throw new InvalidOperationException("Call Connect first.");

            var w = new RmpWriter();
            writeBody?.Invoke(w);
            _connection.Send(command, w.ToArray());

            var r = _connection.Receive(response);
            if (!ReadSuccess(r))
                throw new CommandFailedException($"The server reported that command {command} failed.");
            return r;
        }

        /// <summary>Command responses are an expected&lt;success, failure&gt; encoded as a 16-bit variant index.</summary>
        private static bool ReadSuccess(RmpReader r) => r.ReadVariantIndex(ExpectedAlternativeCount) == ExpectedSuccessIndex;

        private const int ExpectedSuccessIndex = 0, ExpectedAlternativeCount = 2;

        /// <summary>Closes the connection.</summary>
        public void Dispose()
        {
            _connection.Dispose();
            ServerInfo = null;
        }
    }
}
