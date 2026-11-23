using System;
using System.Security.Cryptography;
using System.Text;
using DsDevelopment.FileZillaServer.AdminClient.Models;
using NUnit.Framework;

namespace DsDevelopment.FileZillaServer.AdminClient.Test
{
    [TestFixture]
    public class PasswordTests
    {
        /// <summary>The netstandard2.0 build uses a hand-written PBKDF2; check it against the framework's.</summary>
        [TestCase("password", 1, 32)]
        [TestCase("Test1234", 1000, 32)]
        [TestCase("æøå", 7, 64)]
        [TestCase("", 3, 20)]
        public void ManagedPbkdf2MatchesFramework(string password, int iterations, int length)
        {
            var pw = Encoding.UTF8.GetBytes(password);
            var salt = RandomNumberGenerator.GetBytes(32);
            var expected = Rfc2898DeriveBytes.Pbkdf2(pw, salt, iterations, HashAlgorithmName.SHA256, length);
            Assert.That(Convert.ToHexString(Pbkdf2.DeriveSha256Managed(pw, salt, iterations, length)), Is.EqualTo(Convert.ToHexString(expected)));
        }

        [Test]
        public void CreatedPasswordVerifies()
        {
            var password = Pbkdf2Password.Create("secret");
            Assert.That(password.Iterations, Is.EqualTo(Pbkdf2Password.MinIterations));
            Assert.That(password.Salt.Length, Is.EqualTo(32));
            Assert.That(password.Verify("secret"), Is.True);
            Assert.That(password.Verify("Secret"), Is.False);
        }

        [Test]
        public void SettingPasswordClearsSystemImpersonation()
        {
            var user = new User { SystemImpersonation = new SystemImpersonation() };
            user.SetPassword("x");
            Assert.That(user.SystemImpersonation, Is.Null);
            Assert.That(user.Password, Is.TypeOf<Pbkdf2Password>());
        }
    }
}
