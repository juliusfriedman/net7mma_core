namespace Media.Common//.Binary
{
    /// <summary>
    /// Allows for reading bits from a <see cref="System.IO.Stream"/> with a variable sized buffer (should be a streamreader?)
    /// </summary>
    public class BitReader : BaseDisposable
    {

        #region Statics

        public static readonly byte[] ByteToUnary = new byte[]
        {
            8, 7, 6, 6, 5, 5, 5, 5, 4, 4, 4, 4, 4, 4, 4, 4,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        };

        #endregion

        #region Fields

        //Todo, should inherit Stream and should also skip copying to read if possibly by applying mask or alignment on stream methods...
        //https://github.com/tknpow22/BitReader.project/blob/master/BitReader/BitReader.cs

        internal readonly MemorySegment m_ByteCache;

        internal readonly System.IO.Stream m_BaseStream;

        protected internal int m_ByteIndex = 0, m_BitIndex = 0, m_Remaining = 0;

        protected internal bool m_LeaveOpen;

        protected internal Common.Binary.BitOrder m_BitOrder;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets a value which indicates if the <see cref="BaseStream"/> should be closed on <see cref="Dispose"/>
        /// </summary>
        public bool LeaveOpen { get { return m_LeaveOpen; } set { m_LeaveOpen = value; } }

        /// <summary>
        /// Gets a value which indicates the amount of bytes which are available without reading more data from the <see cref="BaseStream"/>
        /// </summary>
        public int BufferBytesRemaining { get { return m_ByteCache.Count - m_ByteIndex; } }

        /// <summary>
        /// Gets a value which indicates the amount of bits which remain in the current Byte.
        /// </summary>
        public int BufferBitsRemaining { get { return Common.Binary.BitsPerByte - m_BitIndex; } }

        /// <summary>
        /// Gets the amount of bytes remaining in the stream.
        /// </summary>
        public long Remaining { get { return m_BaseStream.Length - m_BaseStream.Position; } }

        /// <summary>
        /// Gets the amount of bits remaining in the <see cref="Buffer"/>
        /// </summary>
        public long RemainingBits { get { return m_Remaining; } }

        /// <summary>
        /// Indicates if the <see cref="BitIndex"/> is aligned to a byte boundary.
        /// </summary>
        public bool IsAligned { get { return Binary.Zero == (m_BitIndex & Binary.Septem); } }

        /// <summary>
        /// Gets the array in which values are stored, the array may be larger than the size usable by the <see cref="Cache"/>
        /// </summary>
        public byte[] Buffer { get { return m_ByteCache.Array; } }

        /// <summary>
        /// Gets the <see cref="Common.MemorySegment"/> in which the <see cref="Buffer"/> is stored.
        /// </summary>
        public Common.MemorySegment Cache { get { return m_ByteCache; } }

        /// <summary>
        /// Gets or Sets the index in the bits
        /// </summary>
        public int BitIndex
        {
            get { return m_BitIndex; }
            set { m_BitIndex = value; }
        }

        /// <summary>
        /// Gets or sets the index in the bytes of the <see cref="Buffer"/>
        /// </summary>
        public int ByteIndex
        {
            get { return m_ByteIndex; }
            set { m_ByteIndex = value; }
        }

        /// <summary>
        /// Gets the <see cref="System.IO.Stream"/> from which the data is read.
        /// </summary>
        public System.IO.Stream BaseStream { get { return m_BaseStream; } }

        /// <summary>
        /// Gets the bit position of the stream
        /// </summary>
        public long BitPosition { get { return m_BitIndex + (m_BaseStream.Position * Common.Binary.BitsPerByte); } }

        /// <summary>
        /// Gets or Sets the underlying <see cref="Binary.BitOrder"/> which is used to write values.
        /// </summary>
        public Common.Binary.BitOrder BitOrder { get { return m_BitOrder; } set { m_BitOrder = value; } }

        #endregion

        #region Constructor / Destructor

        /// <summary>
        /// Creates an instance with the specified properties
        /// </summary>
        /// <param name="source">The source <see cref="System.IO.Stream"/></param>
        /// <param name="bitOrder">The <see cref="Binary.BitOrder"/></param>
        /// <param name="bitIndex">Starting bit offset</param>
        /// <param name="byteIndex">Starting byte offset</param>
        /// <param name="writable">Indicates if the bytes should allow writing past the end</param>
        /// <param name="cacheSize">Cache array size</param>
        /// <param name="leaveOpen">Indicates if Dispose will close the stream</param>
        public BitReader(byte[] source, Binary.BitOrder bitOrder, int bitIndex, int byteIndex, bool writable, int cacheSize = 32, bool leaveOpen = false)
            : this(new System.IO.MemoryStream(source, writable), bitOrder, cacheSize, leaveOpen)
        {
            m_BitIndex = bitIndex;

            m_ByteIndex = byteIndex;
        }

        /// <summary>
        /// Creates an instance with the specified properties
        /// </summary>
        /// <param name="source">The source <see cref="System.IO.Stream"/></param>
        /// <param name="bitOrder">The <see cref="Binary.BitOrder"/></param>
        /// <param name="cacheSize">Cache array size</param>
        /// <param name="leaveOpen">Indicates if Dispose will close the stream</param>
        public BitReader(System.IO.Stream source, Binary.BitOrder bitOrder, int cacheSize = 32, bool leaveOpen = false)
            : base(true)
        {
            if (source is null) throw new System.ArgumentNullException("source");

            m_BaseStream = source;

            m_LeaveOpen = leaveOpen;

            m_ByteCache = new MemorySegment(cacheSize);

            m_BitOrder = bitOrder;
        }

        /// <summary>
        /// Creates an instance with the specified properties and <see cref="Common.Binary.SystemByteOrder"/>
        /// </summary>
        /// <param name="source">The source <see cref="System.IO.Stream"/></param>
        /// <param name="cacheSize">Cache array size</param>
        /// <param name="leaveOpen">Indicates if Dispose will close the stream</param>
        public BitReader(System.IO.Stream source, int cacheSize = 32, bool leaveOpen = false)
            : this(source, Binary.SystemBitOrder, cacheSize, leaveOpen) { }

        /// <summary>
        /// Calls <see cref="Dispose"/>
        /// </summary>
        ~BitReader() { Dispose(); }

        #endregion

        #region Methods

        /// <summary>
        /// Seeks the <see cref="BitIndex"/> and <see cref="ByteIndex"/> and if needed the <see cref="BaseStream.Position"/>.
        /// </summary>
        /// <param name="bitCount"></param>
        public void SeekBits(int bitCount)
        {
            m_Remaining = 0;
            //if (m_Remaining < 0) m_Remaining = 0;
            int bytes = System.Math.DivRem(bitCount, Binary.BitsPerByte, out m_BitIndex);
            m_ByteIndex += bytes;
            if (m_ByteIndex < 0) m_ByteIndex = 0;
            //Todo, should determine if the Position NEEDS to be moved here or if that is natural
            //when remaining is > than bitCount the bytes are already in the buffer and we can ignore seeking.
            //if (m_Remaining >= bitCount) return;
            /*m_Remaining -= Common.Binary.BitsPerByte * (int)*/
            m_BaseStream.Seek(bytes, System.IO.SeekOrigin.Current);
        }

        /// <summary>
        /// Advances reading to the next byte boundary.
        /// </summary>
        public void ByteAlign()
        {
            if (m_BitIndex == Binary.Zero) return;
            m_BitIndex = Binary.Zero;
            ++m_ByteIndex;
        }

        /// <summary>
        /// Find the specified bits within the stream
        /// </summary>
        /// <param name="bitPattern"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public bool Find(byte[] bitPattern, int offset, int length)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Find the specified bits within the stream starting from the end.
        /// Note: must support <see cref="System.IO.Stream.Seek(long, System.IO.SeekOrigin)"/>
        /// </summary>
        /// <param name="bitPattern"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public bool ReverseFind(byte[] bitPattern, int offset, int length)
        {
            throw new System.NotImplementedException();
        }

        /// <summary>
        /// Reads the given amount of bits into the <see cref="Cache"/>.
        /// </summary>
        /// <param name="countOfBits">The amount of bits to read</param>
        /// <returns>The number of bytes read into the <see cref="Cache"/></returns>
        internal int ReadBytesForBits(int countOfBits)
        {
            if (countOfBits <= m_Remaining) return 0;

            int bytesToRead = Common.Binary.BitsToBytes(countOfBits);

            //Call recycle to use the buffer efficiently and determine the index in reading.
            if (Recycle()) m_ByteIndex = m_ByteCache.Offset;

            //If the bytesToRead cannot fit in the buffer then resize it
            if (bytesToRead > m_ByteCache.Count)
            {
                System.Array.Resize(ref m_ByteCache.m_Array, bytesToRead);
                m_ByteCache.IncreaseLength(bytesToRead - m_ByteCache.Count);
            }

            //How many bytes read
            int bytesRead = 0;

            //While there are bytes to read
            while (bytesToRead > 0)
            {
                //Read into the buffer
                bytesRead = m_BaseStream.Read(m_ByteCache.Array, m_ByteCache.Offset + m_ByteIndex + bytesRead, bytesToRead);

                //Check for EOF
                if (bytesRead is 0) break;

                //Adjust for the bytesRead
                bytesToRead -= bytesRead;
            }

            //Compute how many bits are in the buffer.
            m_Remaining += Common.Binary.BitsPerByte * bytesRead;

            //Indicate if reading was complete.
            return bytesToRead;
        }

        /// <summary>
        /// Copies the bits which are left in the cache to the beginning of the cache
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        protected internal bool Recycle(bool clear = false)
        {
            //If there are any bytes then copy them to the offset of the cache from the index
            if (m_Remaining > 0 && m_ByteIndex > Common.Binary.Zero && m_ByteIndex < m_ByteCache.Count)
            {
                int count = m_ByteCache.Count - m_ByteIndex;
                System.Buffer.BlockCopy(m_ByteCache.Array, m_ByteIndex, m_ByteCache.Array, m_ByteCache.Offset, count);
                if (clear) System.Array.Clear(m_ByteCache.Array, m_ByteCache.Offset + m_ByteIndex, count);
                //Indicate to Reset the ByteIndex
                return true;
            }
            //Leave the BitIndex in place
            //Indicate if the ByteIndex needs to be reset.
            return m_ByteIndex >= m_ByteCache.Count;
        }

        /// <summary>
        /// Peek a bit from the <see cref="Cache"/>
        /// </summary>
        /// <param name="reverse">Indicates if the bit will be read using <see cref="Common.Binary.GetBit"/> or <see cref="Common.Binary.GetBitReverse"/></param>
        /// <returns></returns>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool PeekBit(bool reverse = false)
        {
            return reverse switch
            {
                true => Common.Binary.GetBit(ref m_ByteCache.Array[m_ByteIndex], m_BitIndex),
                //.net core 3.1 requires this or the build error is that this method doesn't return on all code paths....
                _ => Common.Binary.GetBitReverse(ref m_ByteCache.Array[m_ByteIndex], m_BitIndex),
            };
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public byte Peek8(bool reverse = false)
        {
            int bits = Media.Common.Binary.BytesToBits(ref m_ByteIndex) + m_BitIndex;

            return m_BitOrder switch
            {
                Binary.BitOrder.LeastSignificant => (byte)(reverse ? Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, Common.Binary.BitsPerByte) : Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, Common.Binary.BitsPerByte)),
                Binary.BitOrder.MostSignificant => (byte)(reverse ? Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, Common.Binary.BitsPerByte) : Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, Common.Binary.BitsPerByte)),
                _ => throw new System.NotSupportedException("Please create an issue for your use case"),
            };
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public short Peek16(bool reverse = false)
        {
            int bits = Media.Common.Binary.BytesToBits(ref m_ByteIndex) + m_BitIndex;

            return m_BitOrder switch
            {
                Binary.BitOrder.LeastSignificant => (short)(reverse ? Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, Common.Binary.BitsPerShort) : Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, Common.Binary.BitsPerShort)),
                Binary.BitOrder.MostSignificant => (short)(reverse ? Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, Common.Binary.BitsPerShort) : Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, Common.Binary.BitsPerShort)),
                _ => throw new System.NotSupportedException("Please create an issue for your use case"),
            };
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public int Peek24(bool reverse = false)
        {
            int bits = Media.Common.Binary.BytesToBits(ref m_ByteIndex) + m_BitIndex;

            return m_BitOrder switch
            {
                Binary.BitOrder.LeastSignificant => (int)(reverse ? Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, Common.Binary.TripleBitSize) : Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, Common.Binary.TripleBitSize)),
                Binary.BitOrder.MostSignificant => (int)(reverse ? Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, Common.Binary.TripleBitSize) : Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, Common.Binary.TripleBitSize)),
                _ => throw new System.NotSupportedException("Please create an issue for your use case"),
            };
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public int Peek32(bool reverse = false)
        {
            int bits = Media.Common.Binary.BytesToBits(ref m_ByteIndex) + m_BitIndex;

            return m_BitOrder switch
            {
                Binary.BitOrder.LeastSignificant => (int)(reverse ? Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, Common.Binary.BitsPerInteger) : Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, Common.Binary.BitsPerInteger)),
                Binary.BitOrder.MostSignificant => (int)(reverse ? Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, Common.Binary.BitsPerInteger) : Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, Common.Binary.BitsPerInteger)),
                _ => throw new System.NotSupportedException("Please create an issue for your use case"),
            };
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public long Peek64(bool reverse = false)
        {
            int bits = Media.Common.Binary.BytesToBits(ref m_ByteIndex) + m_BitIndex;

            return m_BitOrder switch
            {
                Binary.BitOrder.LeastSignificant => (long)(reverse ? Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, Common.Binary.BitsPerLong) : Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, Common.Binary.BitsPerLong)),
                Binary.BitOrder.MostSignificant => (long)(reverse ? Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, Common.Binary.BitsPerLong) : Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, Common.Binary.BitsPerLong)),
                _ => throw new System.NotSupportedException("Please create an issue for your use case"),
            };
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        [System.CLSCompliant(false)]
        public ulong PeekBits(int count, bool reverse = false)
        {
            int bits = Media.Common.Binary.BytesToBits(ref m_ByteIndex) + m_BitIndex;

            return m_BitOrder switch
            {
                Binary.BitOrder.LeastSignificant => (reverse ? Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, count) : Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, count)),
                Binary.BitOrder.MostSignificant => (reverse ? Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, count) : Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, count)),
                _ => throw new System.NotSupportedException("Please create an issue for your use case"),
            };
        }


        /// <summary>
        /// Reads a single bit from the <see cref="Cache"/> at the <see cref="ByteIndex"/> and <see cref="BitIndex"/>.
        /// Up to <see cref="Common.Binary.BitsPerByte"/> bits maybe read.
        /// </summary>
        /// <param name="reverse">Indicates if the <see cref="BitIndex"/> should be used verbatim</param>
        /// <returns>The bool which represents the value</returns>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool ReadBit(bool reverse = false)
        {
            try
            {
                ReadBytesForBits(Common.Binary.One);

                return PeekBit(reverse);
            }
            finally
            {
                Common.Binary.ComputeBits(Common.Binary.One, ref m_BitIndex, ref m_ByteIndex);
                --m_Remaining;
            }
        }

        /// <summary>
        /// Reads <see cref="Common.Binary.BitsPerByte"/> bits
        /// </summary>
        /// <param name="reverse">Indicates if the <see cref="BitOrder"/> is reversed</param>
        /// <returns>The value read</returns>
        public byte Read8(bool reverse = false)
        {
            try
            {
                ReadBytesForBits(Common.Binary.BitsPerByte);

                return Peek8(reverse);
            }
            finally
            {
                Common.Binary.ComputeBits(Common.Binary.BitsPerByte, ref m_BitIndex, ref m_ByteIndex);
                m_Remaining -= Common.Binary.BitsPerByte;
            }
        }

        /// <summary>
        /// Reads <see cref="Common.Binary.BitsPerShort"/> bits
        /// </summary>
        /// <param name="reverse">Indicates if the <see cref="BitOrder"/> is reversed</param>
        /// <returns>The value read</returns>
        public short Read16(bool reverse = false)
        {
            try
            {
                ReadBytesForBits(Common.Binary.BitsPerShort);

                return Peek16(reverse);
            }
            finally
            {
                Common.Binary.ComputeBits(Common.Binary.BitsPerShort, ref m_BitIndex, ref m_ByteIndex);
                m_Remaining -= Common.Binary.BitsPerShort;
            }
        }

        /// <summary>
        /// Reads <see cref="Common.Binary.TripleBitSize"/> bits
        /// </summary>
        /// <param name="reverse">Indicates if the <see cref="BitOrder"/> is reversed</param>
        /// <returns>The value read</returns>
        public int Read24(bool reverse = false)
        {
            try
            {
                ReadBytesForBits(Common.Binary.TripleBitSize);

                return Peek24(reverse);
            }
            finally
            {
                Common.Binary.ComputeBits(Common.Binary.TripleBitSize, ref m_BitIndex, ref m_ByteIndex);
                m_Remaining -= Common.Binary.TripleBitSize;
            }
        }

        /// <summary>
        /// Reads <see cref="Common.Binary.BitsPerInteger"/> bits
        /// </summary>
        /// <param name="reverse">Indicates if the <see cref="BitOrder"/> is reversed</param>
        /// <returns>The value read</returns>
        public int Read32(bool reverse = false)
        {
            try
            {
                ReadBytesForBits(Common.Binary.BitsPerInteger);

                return Peek32(reverse);
            }
            finally
            {
                Common.Binary.ComputeBits(Common.Binary.BitsPerInteger, ref m_BitIndex, ref m_ByteIndex);
                m_Remaining -= Common.Binary.BitsPerInteger;
            }
        }

        /// <summary>
        /// Reads <see cref="Common.Binary.BitsPerLong"/> bits
        /// </summary>
        /// <param name="reverse">Indicates if the <see cref="BitOrder"/> is reversed</param>
        /// <returns>The value read</returns>
        public long Read64(bool reverse = false)
        {
            try
            {
                ReadBytesForBits(Common.Binary.BitsPerLong);

                return Peek64(reverse);
            }
            finally
            {
                Common.Binary.ComputeBits(Common.Binary.BitsPerLong, ref m_BitIndex, ref m_ByteIndex);
                m_Remaining -= Common.Binary.BitsPerLong;
            }
        }

        //For Flac.BitReader
        ////public int ReadUnarySigned(bool reverse = false) 
        ////{
        ////    return (int)ReadUnary(reverse);
        ////}

        ////[System.CLSCompliant(false)]
        ////public uint ReadUnary(bool reverse = false)
        ////{
        ////    uint val = Binary.Zero;
        ////    ulong result = ReadBits(Binary.BitsPerByte, reverse) >> 56;
        ////    while (result == Binary.Zero)
        ////    {
        ////        result = ReadBits(Binary.BitsPerByte, reverse) >> 56;
        ////    }
        ////    val += ByteToUnary[result];
        ////    SeekBits((int)(val & Binary.Septem) + Binary.One);
        ////    return val;
        ////}

        /// <summary>
        /// /// Reads data from the <see cref="BaseStream"/> into the <see cref="Cache"/>
        /// </summary>
        /// <returns>The number of bits read or 0 for EOF.</returns>
        public int Fill()
        {
            return m_Remaining = Common.Binary.BitsPerByte * m_BaseStream.Read(m_ByteCache.Array, m_ByteCache.Offset, m_ByteCache.Count - m_ByteIndex);
        }

        /// <summary>
        /// Reads an arbitary amount of bits specified by <paramref name="count"/>
        /// </summary>
        /// <param name="count">The amount of bits to read</param>
        /// <param name="reverse">Indicates if the <see cref="BitOrder"/> is reversed</param>
        /// <returns></returns>
        [System.CLSCompliant(false)]
        public ulong ReadBits(int count, bool reverse = false)
        {
            try
            {
                ReadBytesForBits(count);

                return PeekBits(count);
            }
            finally
            {
                Common.Binary.ComputeBits(count, ref m_BitIndex, ref m_ByteIndex);

                m_Remaining -= count;
            }
        }

        /// <summary>
        /// Reads an arbitary amount of bits specified by <paramref name="count"/>
        /// </summary>
        /// <param name="count"></param>
        /// <param name="reverse">Indicates if the <see cref="BitOrder"/> is reversed</param>
        /// <returns></returns>
        public long ReadBitsSigned(int count, bool reverse = false)
        {
            try
            {
                ReadBytesForBits(count);

                return (long)PeekBits(count);
            }
            finally
            {
                Common.Binary.ComputeBits(count, ref m_BitIndex, ref m_ByteIndex);

                m_Remaining -= count;
            }
        }

        /// <summary>
        /// Reads 1 - 7 bytes into <see cref="Buffer"/> and returns an indication if the value was encoded correctly.
        /// </summary>
        /// <param name="result">The decoded UTF8 value</param>
        /// <returns>True if the value was encoded correctly, otherwise False</returns>
        internal bool ReadUTF8(out ulong result)
        {
            ulong v = 0, x;
            int i;
            x = m_ByteCache[++m_ByteIndex] = Read8();
            if (0 == (x & 0x80))
            {
                v = x;
                i = 0;
            }
            else if (0xC0 == (x & 0xE0)) /* 110xxxxx */
            {
                v = x & 0x1F;
                i = 1;
            }
            else if (0xE0 == (x & 0xF0)) /* 1110xxxx */
            {
                v = x & 0x0F;
                i = 2;
            }
            else if (0xF0 == (x & 0xF8)) /* 11110xxx */
            {
                v = x & 0x07;
                i = 3;
            }
            else if (0xF8 == (x & 0xFC)) /* 111110xx */
            {
                v = x & 0x03;
                i = 4;
            }
            else if (0xFC == (x & 0xFE)) /* 1111110x */
            {
                v = x & 0x01;
                i = 5;
            }
            else if (0xFE == x) /* 11111110 */
            {
                v = 0;
                i = 6;
            }
            else
            {
                result = v;
                return false;
            }

            for (; i > 0; i--)
            {
                x = m_ByteCache[++m_ByteIndex] = Read8();

                if (0x80 != (x & 0xC0))  /* 10xxxxxx */
                {
                    result = v;
                    return false;
                }

                v <<= 6;
                v |= (x & 0x3F);
            }

            result = v;

            return true;
        }

        /// <summary>
        /// Copies to <paramref name="dest"/> at the <paramref name="destByteOffset"/> and <paramref name="destBitOffset"/>
        /// </summary>
        /// <param name="count">The amount of bits to copy</param>
        /// <param name="dest">The destination</param>
        /// <param name="destByteOffset">The offset in the destination</param>
        /// <param name="destBitOffset">The bit offset within the <paramref name="destByteOffset"/></param>
        public void CopyBits(int count, byte[] dest, int destByteOffset, int destBitOffset)
        {
            //Should accept dest and offsets for direct reads?
            //Would pass m_ByteIndex and m_BitIndex for normal cases.
            ReadBytesForBits(count);

            Common.Binary.CopyBitsTo(m_ByteCache.Array, m_ByteIndex, m_BitIndex, dest, destByteOffset, destBitOffset, count);
        }

        #endregion

        #region Overrides

        public override void Dispose()
        {
            if (IsDisposed || ShouldDispose is false) return;

            base.Dispose();

            m_ByteCache.Dispose();

            if (m_LeaveOpen) return;

            m_BaseStream.Dispose();
        }

        #endregion
    }
}

