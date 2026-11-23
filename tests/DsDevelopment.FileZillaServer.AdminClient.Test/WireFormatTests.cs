using System;
using System.Linq;
using DsDevelopment.FileZillaServer.AdminClient.Protocol;
using NUnit.Framework;

namespace DsDevelopment.FileZillaServer.AdminClient.Test
{
    [TestFixture]
    public class WireFormatTests
    {
        [TestCase(0, "00")]
        [TestCase(250, "FA")]
        [TestCase(251, "FB")]
        [TestCase(252, "FC01")]
        [TestCase(251 + 255, "FCFF")]
        [TestCase(251 + 256, "FD0001")]
        [TestCase(251 + 0x123456, "FE563412")]
        public void SizeTagEncoding(int size, string expectedHex)
        {
            var w = new RmpWriter();
            w.WriteSize(size);
            Assert.That(Convert.ToHexString(w.ToArray()), Is.EqualTo(expectedHex));
        }

        [Test]
        public void SizeTagRoundTrip()
        {
            foreach (var size in new[] { 0, 1, 250, 251, 252, 506, 507, 65535, 65787, 1_000_000 })
            {
                var w = new RmpWriter();
                w.WriteSize(size);
                var bytes = w.ToArray().Concat(new byte[size]).ToArray(); // reader checks size fits in the message
                Assert.That(new RmpReader(bytes).ReadSize(), Is.EqualTo(size), $"size {size}");
            }
        }

        [Test]
        public void StringsAreUtf8WithSizePrefix()
        {
            var w = new RmpWriter();
            w.WriteString("æøå");
            Assert.That(Convert.ToHexString(w.ToArray()), Is.EqualTo("06C3A6C3B8C3A5"));
            Assert.That(new RmpReader(w.ToArray()).ReadString(), Is.EqualTo("æøå"));
        }

        [Test]
        public void IntegersAreLittleEndianAndBigEndianFramesAreReadable()
        {
            var w = new RmpWriter();
            w.WriteUInt32(70);
            Assert.That(Convert.ToHexString(w.ToArray()), Is.EqualTo("46000000"));
            Assert.That(new RmpReader(new byte[] { 0, 0, 0, 70 }, bigEndian: true).ReadUInt32(), Is.EqualTo(70));
        }

        [Test]
        public void TruncatedMessageThrowsProtocolException()
        {
            Assert.Throws<ProtocolException>(() => new RmpReader(new byte[] { 5, (byte)'a' }).ReadString());
        }

        [TestCase("9d:2e:bd:36", "9D2EBD36")]
        [TestCase("9D-2E-BD-36", "9D2EBD36")]
        [TestCase(" 9d2ebd36 ", "9D2EBD36")]
        public void FingerprintNormalization(string input, string expected)
        {
            Assert.That(AdminConnection.NormalizeFingerprint(input), Is.EqualTo(expected));
        }
    }
}
