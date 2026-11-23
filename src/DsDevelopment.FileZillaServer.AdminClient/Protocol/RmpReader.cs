using System;
using System.Collections.Generic;
using System.Text;

namespace DsDevelopment.FileZillaServer.AdminClient.Protocol
{
    /// <summary>
    /// Reads values in FileZilla Server's binary archive format.
    /// Multi-byte integers follow the byte order announced in the frame header.
    /// </summary>
    internal sealed class RmpReader
    {
        private readonly byte[] _data;
        private int _position;

        public RmpReader(byte[] data, bool bigEndian = false)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            BigEndian = bigEndian;
        }

        public bool BigEndian { get; }

        public int Position => _position;

        public int Remaining => _data.Length - _position;

        public bool AtEnd => _position >= _data.Length;

        private void Require(int count)
        {
            if (count < 0 || Remaining < count)
                throw new ProtocolException($"Unexpected end of message: needed {count} byte(s) at offset {_position}, {Remaining} left.");
        }

        public byte ReadByte()
        {
            Require(1);
            return _data[_position++];
        }

        public bool ReadBool() => ReadByte() != 0;

        private ulong ReadInteger(int size)
        {
            Require(size);
            ulong value = 0;
            for (var i = 0; i < size; i++)
            {
                var b = _data[_position + i];
                value |= BigEndian
                    ? (ulong)b << (8 * (size - 1 - i))
                    : (ulong)b << (8 * i);
            }
            _position += size;
            return value;
        }

        public ushort ReadUInt16() => (ushort)ReadInteger(2);

        public short ReadInt16() => (short)ReadUInt16();

        public uint ReadUInt32() => (uint)ReadInteger(4);

        public ulong ReadUInt64() => ReadInteger(8);

        public byte[] ReadFixed(int length)
        {
            Require(length);
            var result = new byte[length];
            Buffer.BlockCopy(_data, _position, result, 0, length);
            _position += length;
            return result;
        }

        /// <summary>Reads a size in the "shortened" encoding (see <see cref="RmpWriter.WriteSize"/>).</summary>
        public int ReadSize()
        {
            var first = ReadByte();
            if (first < RmpWriter.SmallSize)
                return first;

            var count = first - RmpWriter.SmallSize;
            if (count > sizeof(uint))
                throw new ProtocolException($"Invalid size tag 0x{first:X2} at offset {_position - 1}.");

            ulong value = 0;
            for (var i = 0; i < count; i++)
                value |= (ulong)ReadByte() << (8 * i);
            value += RmpWriter.SmallSize;

            if (value > int.MaxValue || (long)value > Remaining)
                throw new ProtocolException($"Size {value} at offset {_position} exceeds the message.");
            return (int)value;
        }

        public string ReadString()
        {
            var length = ReadSize();
            var bytes = ReadFixed(length);
            return Encoding.UTF8.GetString(bytes);
        }

        public byte[] ReadBytes() => ReadFixed(ReadSize());

        public List<T> ReadList<T>(Func<RmpReader, T> readItem)
        {
            var count = ReadSize();
            var list = new List<T>(count);
            for (var i = 0; i < count; i++)
                list.Add(readItem(this));
            return list;
        }

        public List<string> ReadStringList() => ReadList(r => r.ReadString());

        public T ReadOptional<T>(Func<RmpReader, T> readValue) where T : class
            => ReadBool() ? readValue(this) : null;

        public int ReadVariantIndex(int alternatives)
        {
            var index = ReadInt16();
            if (index < 0 || index >= alternatives)
                throw new ProtocolException($"Variant index {index} out of range (0..{alternatives - 1}) at offset {_position - 2}.");
            return index;
        }
    }
}
