/*
    Copyright(c) 2015 Neodymium

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.
*/

using SharpDX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace CodeWalker.GameFiles
{
    public enum Endianess
    {
        LittleEndian,
        BigEndian
    }

    public enum DataType
    {
        Byte = 0,
        Int16 = 1,
        Int32 = 2,
        Int64 = 3,
        Uint16 = 4,
        Uint32 = 5,
        Uint64 = 6,
        Float = 7,
        Double = 8,
        String = 9,
    }

    public class DataReader
    {
        protected Stream baseStream;

        [ThreadStatic]
        private static byte[] _threadBuffer;

        protected static byte[] GetThreadBuffer()
        {
            return _threadBuffer ?? (_threadBuffer = new byte[64]);
        }

        /// <summary>
        /// Gets or sets the endianess of the underlying stream.
        /// </summary>
        public Endianess Endianess { get; set; }

        /// <summary>
        /// Gets the length of the underlying stream.
        /// </summary>
        public virtual long Length => baseStream?.Length ?? 0;

        /// <summary>
        /// Gets or sets the position within the underlying stream.
        /// </summary>
        public virtual long Position
        {
            get => baseStream?.Position ?? 0;
            set
            {
                if (baseStream != null) baseStream.Position = value;
            }
        }

        public Stream BaseStream => baseStream;

        public DataReader(Stream stream, Endianess endianess = Endianess.LittleEndian)
        {
            this.baseStream = stream;
            this.Endianess = endianess;
        }

        /// <summary>
        /// Reads data from the underlying stream into a new buffer.
        /// </summary>
        protected virtual byte[] ReadFromStream(int count, bool ignoreEndianess = false)
        {
            var buffer = new byte[count];
            ReadFromStreamInternal(buffer, 0, count, ignoreEndianess);
            return buffer;
        }

        protected virtual void ReadFromStreamInternal(byte[] buffer, int offset, int count, bool ignoreEndianess = false)
        {
            if (baseStream != null)
            {
                int read = 0;
                while (read < count)
                {
                    int r = baseStream.Read(buffer, offset + read, count - read);
                    if (r <= 0) break;
                    read += r;
                }
            }

            if (!ignoreEndianess && (Endianess == Endianess.BigEndian) && count > 1)
            {
                Array.Reverse(buffer, offset, count);
            }
        }

        /// <summary>
        /// Reads a single byte.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual byte ReadByte()
        {
            if (baseStream != null)
            {
                int b = baseStream.ReadByte();
                if (b >= 0) return (byte)b;
            }
            return ReadFromStream(1)[0];
        }

        /// <summary>
        /// Reads a sequence of bytes.
        /// </summary>
        public virtual byte[] ReadBytes(int count)
        {
            return ReadFromStream(count, true);
        }

        /// <summary>
        /// Reads a signed 16-bit value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual short ReadInt16()
        {
            return (short)ReadUInt16();
        }

        /// <summary>
        /// Reads a signed 32-bit value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual int ReadInt32()
        {
            return (int)ReadUInt32();
        }

        /// <summary>
        /// Reads a signed 64-bit value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual long ReadInt64()
        {
            return (long)ReadUInt64();
        }

        /// <summary>
        /// Reads an unsigned 16-bit value without allocating.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual ushort ReadUInt16()
        {
            byte[] buf = GetThreadBuffer();
            ReadFromStreamInternal(buf, 0, 2, false);
            return (ushort)(buf[0] | (buf[1] << 8));
        }

        /// <summary>
        /// Reads an unsigned 32-bit value without allocating.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual uint ReadUInt32()
        {
            byte[] buf = GetThreadBuffer();
            ReadFromStreamInternal(buf, 0, 4, false);
            return (uint)(buf[0] | (buf[1] << 8) | (buf[2] << 16) | (buf[3] << 24));
        }

        /// <summary>
        /// Reads an unsigned 64-bit value without allocating.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual ulong ReadUInt64()
        {
            byte[] buf = GetThreadBuffer();
            ReadFromStreamInternal(buf, 0, 8, false);
            uint lo = (uint)(buf[0] | (buf[1] << 8) | (buf[2] << 16) | (buf[3] << 24));
            uint hi = (uint)(buf[4] | (buf[5] << 8) | (buf[6] << 16) | (buf[7] << 24));
            return ((ulong)hi << 32) | lo;
        }

        /// <summary>
        /// Reads a single precision floating point value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual float ReadSingle()
        {
            uint val = ReadUInt32();
            return Unsafe.As<uint, float>(ref val);
        }

        /// <summary>
        /// Reads a double precision floating point value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual double ReadDouble()
        {
            ulong val = ReadUInt64();
            return Unsafe.As<ulong, double>(ref val);
        }

        /// <summary>
        /// Reads a null-terminated UTF-8 string without per-byte allocations.
        /// </summary>
        public virtual string ReadString()
        {
            byte[] buf = GetThreadBuffer();
            byte[] strBytes = buf;
            int len = 0;

            while (true)
            {
                byte b = ReadByte();
                if (b == 0) break;

                if (len >= strBytes.Length)
                {
                    if (strBytes == buf)
                    {
                        strBytes = new byte[128];
                        Buffer.BlockCopy(buf, 0, strBytes, 0, len);
                    }
                    else
                    {
                        Array.Resize(ref strBytes, strBytes.Length * 2);
                    }
                }
                strBytes[len++] = b;
            }

            if (len == 0) return string.Empty;
            return Encoding.UTF8.GetString(strBytes, 0, len);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual Vector3 ReadVector3()
        {
            return new Vector3(ReadSingle(), ReadSingle(), ReadSingle());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual Vector4 ReadVector4()
        {
            return new Vector4(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        }

        public virtual Matrix ReadMatrix()
        {
            Matrix m = new Matrix();
            m.M11 = ReadSingle(); m.M21 = ReadSingle(); m.M31 = ReadSingle(); m.M41 = ReadSingle();
            m.M12 = ReadSingle(); m.M22 = ReadSingle(); m.M32 = ReadSingle(); m.M42 = ReadSingle();
            m.M13 = ReadSingle(); m.M23 = ReadSingle(); m.M33 = ReadSingle(); m.M43 = ReadSingle();
            m.M14 = ReadSingle(); m.M24 = ReadSingle(); m.M34 = ReadSingle(); m.M44 = ReadSingle();
            return m;
        }

        public static uint SizeOf(DataType type)
        {
            switch (type)
            {
                default:
                case DataType.Byte: return 1;
                case DataType.Int16: return 2;
                case DataType.Int32: return 4;
                case DataType.Int64: return 8;
                case DataType.Uint16: return 2;
                case DataType.Uint32: return 4;
                case DataType.Uint64: return 8;
                case DataType.Float: return 4;
                case DataType.Double: return 8;
                case DataType.String: return 0;
            }
        }
    }

    public class DataWriter
    {
        protected Stream baseStream;

        [ThreadStatic]
        private static byte[] _threadWriteBuffer;

        protected static byte[] GetThreadBuffer()
        {
            return _threadWriteBuffer ?? (_threadWriteBuffer = new byte[64]);
        }

        public Endianess Endianess { get; set; }

        public virtual long Length => baseStream?.Length ?? 0;

        public virtual long Position
        {
            get => baseStream?.Position ?? 0;
            set
            {
                if (baseStream != null) baseStream.Position = value;
            }
        }

        public Stream BaseStream => baseStream;

        public DataWriter(Stream stream, Endianess endianess = Endianess.LittleEndian)
        {
            this.baseStream = stream;
            this.Endianess = endianess;
        }

        protected virtual void WriteToStream(byte[] value, bool ignoreEndianess = false)
        {
            if (value == null || value.Length == 0 || baseStream == null) return;

            if (!ignoreEndianess && (Endianess == Endianess.BigEndian) && value.Length > 1)
            {
                var buffer = (byte[])value.Clone();
                Array.Reverse(buffer);
                baseStream.Write(buffer, 0, buffer.Length);
            }
            else
            {
                baseStream.Write(value, 0, value.Length);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(byte value)
        {
            if (baseStream != null)
                baseStream.WriteByte(value);
            else
                WriteToStream(new byte[] { value });
        }

        public void Write(byte[] value)
        {
            WriteToStream(value, true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(short value)
        {
            Write((ushort)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(int value)
        {
            Write((uint)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(long value)
        {
            Write((ulong)value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ushort value)
        {
            byte[] buf = GetThreadBuffer();
            if (Endianess == Endianess.LittleEndian)
            {
                buf[0] = (byte)value;
                buf[1] = (byte)(value >> 8);
            }
            else
            {
                buf[0] = (byte)(value >> 8);
                buf[1] = (byte)value;
            }
            baseStream.Write(buf, 0, 2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(uint value)
        {
            byte[] buf = GetThreadBuffer();
            if (Endianess == Endianess.LittleEndian)
            {
                buf[0] = (byte)value;
                buf[1] = (byte)(value >> 8);
                buf[2] = (byte)(value >> 16);
                buf[3] = (byte)(value >> 24);
            }
            else
            {
                buf[0] = (byte)(value >> 24);
                buf[1] = (byte)(value >> 16);
                buf[2] = (byte)(value >> 8);
                buf[3] = (byte)value;
            }
            baseStream.Write(buf, 0, 4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ulong value)
        {
            byte[] buf = GetThreadBuffer();
            uint lo = (uint)value;
            uint hi = (uint)(value >> 32);

            if (Endianess == Endianess.LittleEndian)
            {
                buf[0] = (byte)lo;
                buf[1] = (byte)(lo >> 8);
                buf[2] = (byte)(lo >> 16);
                buf[3] = (byte)(lo >> 24);
                buf[4] = (byte)hi;
                buf[5] = (byte)(hi >> 8);
                buf[6] = (byte)(hi >> 16);
                buf[7] = (byte)(hi >> 24);
            }
            else
            {
                buf[0] = (byte)(hi >> 24);
                buf[1] = (byte)(hi >> 16);
                buf[2] = (byte)(hi >> 8);
                buf[3] = (byte)hi;
                buf[4] = (byte)(lo >> 24);
                buf[5] = (byte)(lo >> 16);
                buf[6] = (byte)(lo >> 8);
                buf[7] = (byte)lo;
            }
            baseStream.Write(buf, 0, 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(float value)
        {
            uint val = Unsafe.As<float, uint>(ref value);
            Write(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(double value)
        {
            ulong val = Unsafe.As<double, ulong>(ref value);
            Write(val);
        }

        public void Write(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value);
                Write(bytes);
            }
            Write((byte)0);
        }

        public void Write(Vector3 value)
        {
            Write(value.X);
            Write(value.Y);
            Write(value.Z);
        }

        public void Write(Vector4 value)
        {
            Write(value.X);
            Write(value.Y);
            Write(value.Z);
            Write(value.W);
        }

        public void Write(Matrix value)
        {
            Write(value.M11); Write(value.M21); Write(value.M31); Write(value.M41);
            Write(value.M12); Write(value.M22); Write(value.M32); Write(value.M42);
            Write(value.M13); Write(value.M23); Write(value.M33); Write(value.M43);
            Write(value.M14); Write(value.M24); Write(value.M34); Write(value.M44);
        }
    }
}
