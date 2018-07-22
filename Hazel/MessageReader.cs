﻿using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace Hazel
{
    ///
    public class MessageReader : IRecyclable
    {
        private static readonly ObjectPool<MessageReader> objectPool = new ObjectPool<MessageReader>(() => new MessageReader());

        public byte[] Buffer;
        public byte Tag;

        public int Length;

        public int Offset { get; private set; }
        public int Position
        {
            get { return this._position; }
            set
            {
                this._position = value;
                this.readHead = this._position + Offset;
            }
        }

        private int _position;

        private int readHead;
        
        public static MessageReader Get(byte[] buffer, int offset, int length)
        {
            var output = objectPool.GetObject();
            output.Buffer = buffer;
            output.Offset = offset;
            output.Position = 0;
            output.Length = length;
            output.Tag = output.ReadByte();
            
            return output;
        }

        public static MessageReader Get(byte[] buffer, int offset)
        {
            var output = objectPool.GetObject();
            output.Buffer = buffer;
            output.Offset = offset;
            output.Position = 0;

            output.Length = output.ReadUInt16();
            output.Tag = output.ReadByte();

            output.Offset += 3;
            output.Position = 0;

            return output;
        }

        ///
        public MessageReader ReadMessage()
        {
            var output = MessageReader.Get(this.Buffer, this.readHead);
            this.Position += output.Length + 3;
            return output;
        }

        ///
        public void Recycle()
        {
            this.Position = this.Length = 0;
            objectPool.PutObject(this);
        }

        #region Read Methods
        public bool ReadBoolean()
        {
            byte val = this.FastByte();
            return val != 0;
        }

        public sbyte ReadSByte()
        {
            return (sbyte)this.FastByte();
        }

        public byte ReadByte()
        {
            return this.FastByte();
        }

        public ushort ReadUInt16()
        {
            ushort output =
                (ushort)(this.FastByte()
                | this.FastByte() << 8);
            return output;
        }

        public short ReadInt16()
        {
            short output =
                (short)(this.FastByte()
                | this.FastByte() << 8);
            return output;
        }

        public int ReadInt32()
        {
            int output = this.FastByte()
                | this.FastByte() << 8
                | this.FastByte() << 16
                | this.FastByte() << 24;

            return output;
        }

        public unsafe float ReadSingle()
        {
            float output = 0;
            fixed (byte* bufPtr = &this.Buffer[this.readHead])
            {
                byte* outPtr = (byte*)&output;

                *outPtr = *bufPtr;
                *(outPtr + 1) = *(bufPtr + 1);
                *(outPtr + 2) = *(bufPtr + 2);
                *(outPtr + 3) = *(bufPtr + 3);
            }

            this.Position += 4;
            return output;
        }

        public string ReadString()
        {
            int len = this.ReadPackedInt32();
            string output = UTF8Encoding.UTF8.GetString(this.Buffer, this.readHead, len);

            this.Position += len;
            return output;
        }

        public byte[] ReadBytesAndSize()
        {
            int len = this.ReadPackedInt32();
            return this.ReadBytes(len);
        }

        public byte[] ReadBytes(int length)
        {
            byte[] output = new byte[length];
            Array.Copy(this.Buffer, this.readHead, output, 0, output.Length);
            this.Position += output.Length;
            return output;
        }

        ///
        public int ReadPackedInt32()
        {
            return (int)this.ReadPackedUInt32();
        }

        ///
        public uint ReadPackedUInt32()
        {
            bool readMore = true;
            int shift = 0;
            uint output = 0;

            while (readMore)
            {
                byte b = this.ReadByte();
                if (b >= 0x80)
                {
                    readMore = true;
                    b ^= 0x80;
                }
                else
                {
                    readMore = false;
                }

                output |= (uint)(b << shift);
                shift += 7;
            }

            return output;
        }
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte FastByte()
        {
            this._position++;
            return this.Buffer[this.readHead++];
        }

        public unsafe static bool IsLittleEndian()
        {
            byte b;
            unsafe
            {
                int i = 1;
                byte* bp = (byte*)&i;
                b = *bp;
            }

            return b == 1;
        }
    }
}