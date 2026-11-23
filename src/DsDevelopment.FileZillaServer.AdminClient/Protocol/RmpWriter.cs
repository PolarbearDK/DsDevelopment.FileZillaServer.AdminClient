using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DsDevelopment.FileZillaServer.AdminClient.Protocol
{
    /// <summary>
    /// Writes values in FileZilla Server's binary archive format. Always writes little-endian
    /// (the frame header tells the server which byte order the payload uses).
    /// </summary>
    internal sealed class RmpWriter
    {
        /// <summary>Sizes below this value are encoded in a single byte.</summary>
        internal const int SmallSize = 255 - sizeof(uint);

        private readonly MemoryStream _stream = new MemoryStream();

        public byte[] ToArray() => _stream.ToArray();

        public void WriteByte(byte value) => _stream.WriteByte(value);

        public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

        public void WriteUInt16(ushort value)
        {
            WriteByte((byte)value);
            WriteByte((byte)(value >> 8));
        }

        public void WriteInt16(short value) => WriteUInt16((ushort)value);

        public void WriteUInt32(uint value)
        {
            for (var i = 0; i < 4; i++)
                WriteByte((byte)(value >> (8 * i)));
        }

        public void WriteUInt64(ulong value)
        {
            for (var i = 0; i < 8; i++)
                WriteByte((byte)(value >> (8 * i)));
        }

        public void WriteRaw(byte[] data) => _stream.Write(data, 0, data.Length);

        /// <summary>Fixed-length binary data (no size prefix), e.g. hashes and IP addresses.</summary>
        public void WriteFixed(byte[] data, int length)
        {
            if (data == null || data.Length != length)
                throw new ArgumentException($"Expected exactly {length} bytes, got {data?.Length ?? 0}.");
            WriteRaw(data);
        }

        /// <summary>
        /// Container/string size in the "shortened" encoding: values below 251 take one byte;
        /// larger values are written as (251 + n) followed by n little-endian bytes of (value - 251).
        /// </summary>
        public void WriteSize(long size)
        {
            if (size < 0 || size > uint.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(size));

            if (size < SmallSize)
            {
                WriteByte((byte)size);
                return;
            }

            var rest = (ulong)(size - SmallSize);
            var extra = new List<byte>();
            while (rest != 0)
            {
                extra.Add((byte)(rest & 0xFF));
                rest >>= 8;
            }

            WriteByte((byte)(SmallSize + extra.Count));
            foreach (var b in extra)
                WriteByte(b);
        }

        public void WriteString(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            WriteSize(bytes.Length);
            WriteRaw(bytes);
        }

        public void WriteBytes(byte[] value)
        {
            value = value ?? Array.Empty<byte>();
            WriteSize(value.Length);
            WriteRaw(value);
        }

        public void WriteList<T>(ICollection<T> items, Action<RmpWriter, T> writeItem)
        {
            var count = items?.Count ?? 0;
            WriteSize(count);
            if (items == null)
                return;
            foreach (var item in items)
                writeItem(this, item);
        }

        public void WriteStringList(ICollection<string> items) => WriteList(items, (w, s) => w.WriteString(s));

        /// <summary>std::optional: a has-value byte followed by the value when present.</summary>
        public void WriteOptional<T>(T value, Action<RmpWriter, T> writeValue) where T : class
        {
            WriteBool(value != null);
            if (value != null)
                writeValue(this, value);
        }

        /// <summary>std::variant: a 16-bit alternative index followed by the alternative.</summary>
        public void WriteVariantIndex(int index) => WriteInt16(checked((short)index));
    }
}
