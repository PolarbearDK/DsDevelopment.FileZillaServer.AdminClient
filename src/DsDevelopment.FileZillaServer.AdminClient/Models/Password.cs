using System;
using System.Security.Cryptography;
using System.Text;
using DsDevelopment.FileZillaServer.AdminClient.Protocol;

namespace DsDevelopment.FileZillaServer.AdminClient.Models
{
    /// <summary>A stored password. Use <see cref="Password.Create"/> to make one from clear text.</summary>
    public abstract class Password
    {
        /// <summary>Alternative order of <c>fz::authentication::any_password</c>.</summary>
        private const int NoneIndex = 0, Pbkdf2Index = 1, Md5Index = 2, Sha512Index = 3, AlternativeCount = 4;

        /// <summary>Hashes a clear-text password the way FileZilla Server does by default (PBKDF2-HMAC-SHA256).</summary>
        public static Password Create(string clearText) => Pbkdf2Password.Create(clearText);

        /// <summary>Checks a clear-text password against this stored password.</summary>
        public abstract bool Verify(string clearText);

        internal static Password Read(RmpReader r)
        {
            switch (r.ReadVariantIndex(AlternativeCount))
            {
                case NoneIndex: return new NoPassword();
                case Pbkdf2Index: return new Pbkdf2Password(r.ReadFixed(Pbkdf2Password.HashSize), r.ReadFixed(Pbkdf2Password.SaltSize), r.ReadUInt64());
                case Md5Index: return new Md5Password(r.ReadFixed(Md5Password.HashSize));
                default: return new Sha512Password(r.ReadFixed(Sha512Password.HashSize), r.ReadFixed(Sha512Password.SaltSize));
            }
        }

        internal void Write(RmpWriter w)
        {
            switch (this)
            {
                case NoPassword _:
                    w.WriteVariantIndex(NoneIndex);
                    break;
                case Pbkdf2Password p:
                    w.WriteVariantIndex(Pbkdf2Index);
                    w.WriteFixed(p.Hash, Pbkdf2Password.HashSize);
                    w.WriteFixed(p.Salt, Pbkdf2Password.SaltSize);
                    w.WriteUInt64(p.Iterations);
                    break;
                case Md5Password m:
                    w.WriteVariantIndex(Md5Index);
                    w.WriteFixed(m.Hash, Md5Password.HashSize);
                    break;
                case Sha512Password s:
                    w.WriteVariantIndex(Sha512Index);
                    w.WriteFixed(s.Hash, Sha512Password.HashSize);
                    w.WriteFixed(s.Salt, Sha512Password.SaltSize);
                    break;
                default:
                    throw new AdminClientException($"Unsupported password type {GetType().Name}.");
            }
        }

        internal static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }

    /// <summary>No password: login with a password is not possible.</summary>
    public sealed class NoPassword : Password
    {
        /// <inheritdoc />
        public override bool Verify(string clearText) => false;
    }

    /// <summary>PBKDF2-HMAC-SHA256 password, the FileZilla Server 1.x default.</summary>
    public sealed class Pbkdf2Password : Password
    {
        internal const int HashSize = 32, SaltSize = 32;

        /// <summary>Minimum iterations the server accepts when verifying.</summary>
        public const ulong MinIterations = 100000;

        /// <summary>Creates a password from stored values.</summary>
        public Pbkdf2Password(byte[] hash, byte[] salt, ulong iterations)
        {
            Hash = hash;
            Salt = salt;
            Iterations = iterations;
        }

        /// <summary>32-byte derived key.</summary>
        public byte[] Hash { get; }
        /// <summary>32-byte salt.</summary>
        public byte[] Salt { get; }
        /// <summary>Iteration count.</summary>
        public ulong Iterations { get; }

        /// <summary>Hashes a clear-text password with a random salt.</summary>
        public static new Pbkdf2Password Create(string clearText)
        {
            var salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(salt);
            return new Pbkdf2Password(Pbkdf2.DeriveSha256(Encoding.UTF8.GetBytes(clearText), salt, (int)MinIterations, HashSize), salt, MinIterations);
        }

        /// <inheritdoc />
        public override bool Verify(string clearText)
            => Iterations >= MinIterations && Iterations <= int.MaxValue
               && FixedTimeEquals(Hash, Pbkdf2.DeriveSha256(Encoding.UTF8.GetBytes(clearText), Salt, (int)Iterations, HashSize));
    }

    /// <summary>Legacy MD5 password (FileZilla Server 0.9.x). Accepted, but not recommended.</summary>
    public sealed class Md5Password : Password
    {
        internal const int HashSize = 16;

        /// <summary>Creates a password from a stored hash.</summary>
        public Md5Password(byte[] hash) => Hash = hash;

        /// <summary>16-byte MD5 hash.</summary>
        public byte[] Hash { get; }

        /// <inheritdoc />
        public override bool Verify(string clearText)
        {
            using (var md5 = MD5.Create())
                return FixedTimeEquals(Hash, md5.ComputeHash(Encoding.UTF8.GetBytes(clearText)));
        }
    }

    /// <summary>Legacy salted SHA-512 password (FileZilla Server 0.9.54+). Accepted, but not recommended.</summary>
    public sealed class Sha512Password : Password
    {
        internal const int HashSize = 64, SaltSize = 64;

        /// <summary>Creates a password from stored values.</summary>
        public Sha512Password(byte[] hash, byte[] salt)
        {
            Hash = hash;
            Salt = salt;
        }

        /// <summary>64-byte hash.</summary>
        public byte[] Hash { get; }
        /// <summary>64-byte salt.</summary>
        public byte[] Salt { get; }

        /// <inheritdoc />
        public override bool Verify(string clearText)
        {
            var pw = Encoding.UTF8.GetBytes(clearText);
            var data = new byte[pw.Length + Salt.Length];
            Buffer.BlockCopy(pw, 0, data, 0, pw.Length);
            Buffer.BlockCopy(Salt, 0, data, pw.Length, Salt.Length);
            using (var sha = SHA512.Create())
                return FixedTimeEquals(Hash, sha.ComputeHash(data));
        }
    }

    /// <summary>PBKDF2-HMAC-SHA256 (RFC 8018).</summary>
    internal static class Pbkdf2
    {
        public static byte[] DeriveSha256(byte[] password, byte[] salt, int iterations, int length)
        {
#if NET6_0_OR_GREATER
            return Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, length);
#else
            return DeriveSha256Managed(password, salt, iterations, length);
#endif
        }

        /// <summary>Plain implementation for netstandard2.0, which lacks PBKDF2 with SHA-256.</summary>
        public static byte[] DeriveSha256Managed(byte[] password, byte[] salt, int iterations, int length)
        {
            if (iterations < 1)
                throw new ArgumentOutOfRangeException(nameof(iterations));

            var result = new byte[length];
            using (var hmac = new HMACSHA256(password))
            {
                var block = new byte[salt.Length + 4];
                Buffer.BlockCopy(salt, 0, block, 0, salt.Length);
                var offset = 0;
                for (uint blockIndex = 1; offset < length; blockIndex++)
                {
                    block[salt.Length] = (byte)(blockIndex >> 24);
                    block[salt.Length + 1] = (byte)(blockIndex >> 16);
                    block[salt.Length + 2] = (byte)(blockIndex >> 8);
                    block[salt.Length + 3] = (byte)blockIndex;

                    var u = hmac.ComputeHash(block);
                    var t = (byte[])u.Clone();
                    for (var i = 1; i < iterations; i++)
                    {
                        u = hmac.ComputeHash(u);
                        for (var j = 0; j < t.Length; j++)
                            t[j] ^= u[j];
                    }

                    var count = Math.Min(t.Length, length - offset);
                    Buffer.BlockCopy(t, 0, result, offset, count);
                    offset += count;
                }
            }
            return result;
        }
    }
}
