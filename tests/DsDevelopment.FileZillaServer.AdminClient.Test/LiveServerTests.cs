using System;
using System.Linq;
using DsDevelopment.FileZillaServer.AdminClient.Models;
using NUnit.Framework;

namespace DsDevelopment.FileZillaServer.AdminClient.Test
{
    /// <summary>
    /// Tests against a running FileZilla Server. Skipped unless these environment variables are set:
    /// FZ_ADMIN_PASSWORD, FZ_ADMIN_FINGERPRINT (SHA-256, as shown in the server's admin settings),
    /// and optionally FZ_ADMIN_HOST / FZ_ADMIN_PORT.
    /// Changes are made with save=false (running server only) and are reverted.
    /// </summary>
    [TestFixture, Category("LiveServer")]
    public class LiveServerTests
    {
        private static FileZillaAdminClient ConnectOrIgnore()
        {
            var password = Environment.GetEnvironmentVariable("FZ_ADMIN_PASSWORD");
            var fingerprint = Environment.GetEnvironmentVariable("FZ_ADMIN_FINGERPRINT");
            if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(fingerprint))
                Assert.Ignore("Set FZ_ADMIN_PASSWORD and FZ_ADMIN_FINGERPRINT to run live server tests.");

            var options = new FileZillaAdminClientOptions { CertificateFingerprint = fingerprint };
            var host = Environment.GetEnvironmentVariable("FZ_ADMIN_HOST");
            if (!string.IsNullOrEmpty(host))
                options.Host = host;
            if (int.TryParse(Environment.GetEnvironmentVariable("FZ_ADMIN_PORT"), out var port))
                options.Port = port;

            var client = new FileZillaAdminClient(options);
            client.Connect(password);
            return client;
        }

        [Test]
        public void ConnectReturnsServerInfo()
        {
            using (var client = ConnectOrIgnore())
            {
                TestContext.Out.WriteLine($"FileZilla Server {client.ServerInfo.ServerVersion} ({client.ServerInfo.ServerHost}), running as {client.ServerInfo.ServiceAccount}");
                Assert.That(client.ServerInfo.ServerVersion, Does.StartWith("1."));
                Assert.That(client.ServerInfo.InstanceId, Is.Not.Empty);
            }
        }

        [Test]
        public void WrongFingerprintIsRejected()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FZ_ADMIN_FINGERPRINT")))
                Assert.Ignore("Set FZ_ADMIN_FINGERPRINT to run live server tests.");

            var options = new FileZillaAdminClientOptions { CertificateFingerprint = new string('0', 64) };
            var host = Environment.GetEnvironmentVariable("FZ_ADMIN_HOST");
            if (!string.IsNullOrEmpty(host))
                options.Host = host;

            using (var client = new FileZillaAdminClient(options))
                Assert.Throws<AdminClientException>(() => client.Connect("irrelevant"));
        }

        [Test]
        public void GetThenSetUnchangedIsLossless()
        {
            using (var client = ConnectOrIgnore())
            {
                var before = client.GetGroupsAndUsers();
                client.SetGroupsAndUsers(before, save: false);
                var after = client.GetGroupsAndUsers();
                Assert.That(Serialize(after), Is.EqualTo(Serialize(before)));
            }
        }

        [Test]
        public void AddedUserIsAcceptedByServer()
        {
            using (var client = ConnectOrIgnore())
            {
                var original = client.GetGroupsAndUsers();
                var name = "adminclient-test-" + Guid.NewGuid().ToString("N").Substring(0, 8);
                try
                {
                    var modified = client.GetGroupsAndUsers();
                    var user = new User
                    {
                        Name = name,
                        Description = "Temporary user created by DsDevelopment.FileZillaServer.AdminClient tests",
                        MountPoints = { new MountPoint { VirtualPath = "/", NativePath = @"C:\Temp\" + name, Access = MountAccess.ReadOnly } },
                    };
                    user.SetPassword("Pa55word!");
                    modified.Users.Add(user);
                    client.SetGroupsAndUsers(modified, save: false);

                    var readBack = client.GetGroupsAndUsers().FindUser(name);
                    Assert.That(readBack, Is.Not.Null, "server did not keep the new user");
                    Assert.That(readBack.Password.Verify("Pa55word!"), Is.True);
                    Assert.That(readBack.MountPoints.Single().NativePath, Is.EqualTo(@"C:\Temp\" + name));
                }
                finally
                {
                    client.SetGroupsAndUsers(original, save: false);
                }

                Assert.That(client.GetGroupsAndUsers().FindUser(name), Is.Null, "test user was not removed");
            }
        }

        private static string Serialize(GroupsAndUsers value)
        {
            var w = new Protocol.RmpWriter();
            value.Write(w);
            return Convert.ToHexString(w.ToArray());
        }
    }
}
