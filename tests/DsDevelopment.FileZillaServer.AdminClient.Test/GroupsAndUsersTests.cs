using System;
using System.IO;
using System.Linq;
using System.Net;
using DsDevelopment.FileZillaServer.AdminClient.Models;
using DsDevelopment.FileZillaServer.AdminClient.Protocol;
using NUnit.Framework;

namespace DsDevelopment.FileZillaServer.AdminClient.Test
{
    /// <summary>
    /// Decodes get_groups_and_users responses captured from a real FileZilla Server 1.12.6,
    /// and checks that re-encoding produces the exact same bytes.
    /// </summary>
    [TestFixture]
    public class GroupsAndUsersTests
    {
        private static byte[] LoadFixture(string name)
            => File.ReadAllBytes(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", name));

        /// <summary>Strips the 2-byte "success" index from a captured response body.</summary>
        private static GroupsAndUsers Decode(byte[] responseBody, out byte[] payload)
        {
            var reader = new RmpReader(responseBody);
            Assert.That(reader.ReadVariantIndex(2), Is.EqualTo(0), "response should be success");
            payload = responseBody.Skip(2).ToArray();
            var result = GroupsAndUsers.Read(reader);
            Assert.That(reader.AtEnd, Is.True, $"{reader.Remaining} byte(s) not consumed");
            return result;
        }

        private static byte[] Encode(GroupsAndUsers value)
        {
            var w = new RmpWriter();
            value.Write(w);
            return w.ToArray();
        }

        [TestCase("default-groups-and-users.bin")]
        [TestCase("test-user-groups-and-users.bin")]
        public void ReencodingIsByteIdentical(string fixture)
        {
            var decoded = Decode(LoadFixture(fixture), out var payload);
            Assert.That(Convert.ToHexString(Encode(decoded)), Is.EqualTo(Convert.ToHexString(payload)));
        }

        [Test]
        public void DefaultConfiguration()
        {
            var data = Decode(LoadFixture("default-groups-and-users.bin"), out _);

            Assert.That(data.Groups, Is.Empty);
            Assert.That(data.DefaultImpersonator, Is.TypeOf<WindowsImpersonator>());
            Assert.That(data.DefaultImpersonator.Enabled, Is.False);
            Assert.That(data.Realms.Select(r => $"{r.Name}={r.Status}"), Is.EqualTo(new[] { "ftp=Enabled", "ftps=Enabled" }));
            Assert.That(data.MountCredentials, Is.Empty);

            var system = data.Users.Single();
            Assert.That(system.Name, Is.EqualTo("<system user>"));
            Assert.That(system.Enabled, Is.False);
            Assert.That(system.SystemImpersonation, Is.Not.Null);
            Assert.That(system.SystemImpersonation.LoginOnly, Is.False);
            Assert.That(system.Identity.Idp, Is.EqualTo("system"));
            Assert.That(system.Description, Does.StartWith("This user can impersonate"));

            var mount = system.MountPoints.Single();
            Assert.That(mount.VirtualPath, Is.EqualTo("/"));
            Assert.That(mount.NativePath, Is.EqualTo("%<home>"));
            Assert.That(mount.LegacyNativePath, Is.EqualTo(":h"));
            Assert.That(mount.Access, Is.EqualTo(MountAccess.ReadWrite));
            Assert.That(mount.Recursion, Is.EqualTo(MountRecursion.RecursiveWithStructureModification));
            Assert.That(system.RateLimits.Inbound, Is.EqualTo(RateLimits.Unlimited));
        }

        [Test]
        public void TestUserAndGroup()
        {
            var data = Decode(LoadFixture("test-user-groups-and-users.bin"), out _);

            var group = data.FindGroup("TestGroup");
            Assert.That(group, Is.Not.Null);
            var groupMount = group.MountPoints.Single();
            Assert.That(groupMount.VirtualPath, Is.EqualTo("/data"));
            Assert.That(groupMount.NativePath, Is.EqualTo(@"C:\Temp"));
            Assert.That(groupMount.Access, Is.EqualTo(MountAccess.ReadOnly));

            var user = data.FindUser("TestUser");
            Assert.That(user, Is.Not.Null);
            Assert.That(user.Enabled, Is.True);
            Assert.That(user.Groups, Is.EqualTo(new[] { "TestGroup" }));
            Assert.That(user.SystemImpersonation, Is.Null);
            Assert.That(user.Methods, Is.EqualTo(new[] { AuthenticationMethods.Password }));
            Assert.That(user.Identity, Is.Null);

            var mount = user.MountPoints.Single();
            Assert.That(mount.VirtualPath, Is.EqualTo("/"));
            Assert.That(mount.NativePath, Is.EqualTo(@"C:\Temp\TestUser"));
            Assert.That(mount.Access, Is.EqualTo(MountAccess.ReadWrite));
            Assert.That(mount.Flags, Is.EqualTo(MountFlags.AutoCreate));

            var password = user.Password as Pbkdf2Password;
            Assert.That(password, Is.Not.Null);
            Assert.That(password.Iterations, Is.EqualTo(100_000));
            Assert.That(password.Verify("Test1234"), Is.True);
            Assert.That(password.Verify("wrong"), Is.False);
        }

        [Test]
        public void NewUserRoundTrips()
        {
            var user = new User
            {
                Name = "alice",
                Groups = { "Customers" },
                Description = "Created by test",
                SessionCountLimit = 3,
                SessionOpenLimits = { Files = 10, Directories = 5 },
                RateLimits = { Inbound = 1024 * 1024 },
                Realms = { new Realm("ftp", RealmStatus.Disabled) },
                AllowedIps = { new IpRange(IPAddress.Parse("10.0.0.1"), IPAddress.Parse("10.0.0.255")), new IpRange(IPAddress.Parse("::1")) },
                MountPoints = { new MountPoint { VirtualPath = "/", NativePath = @"C:\Ftp\alice", Flags = MountFlags.AutoCreate } },
            };
            user.SetPassword("secret");

            var data = new GroupsAndUsers
            {
                Groups = { new Group { Name = "Customers", MountPoints = { new MountPoint { VirtualPath = "/shared", NativePath = @"C:\Ftp\shared", Access = MountAccess.ReadOnly } } } },
                Users = { user },
                MountCredentials = { new MountCredential { NativePath = @"\\nas\ftp", Username = "svc", Password = "pw" } },
            };

            var bytes = Encode(data);
            var reader = new RmpReader(bytes);
            var decoded = GroupsAndUsers.Read(reader);
            Assert.That(reader.AtEnd, Is.True);
            Assert.That(Convert.ToHexString(Encode(decoded)), Is.EqualTo(Convert.ToHexString(bytes)));

            var alice = decoded.FindUser("alice");
            Assert.That(alice.Password.Verify("secret"), Is.True);
            Assert.That(alice.AllowedIps.Select(r => r.ToString()), Is.EqualTo(new[] { "10.0.0.1-10.0.0.255", "::1" }));
            Assert.That(alice.SessionOpenLimits.Files, Is.EqualTo(10));
            Assert.That(decoded.MountCredentials.Single().Username, Is.EqualTo("svc"));
        }
    }
}
