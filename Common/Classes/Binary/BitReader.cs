namespace Media.Common//.Binary
{
    /// <summary>
    /// Allows for reading bits from a <see cref="System.IO.Stream"/> with a variable sized buffer (should be a streamreader?)
    /// </summary>
    public class BitReader : BaseDisposable
    {
        #region Fields

        //Todo, should inherit Stream and should also skip copying to read if possibly by applying mask or alignment on stream methods...
        //https://github.com/tknpow22/BitReader.project/blob/master/BitReader/BitReader.cs

        internal readonly MemorySegment m_ByteCache;

        internal readonly System.IO.Stream m_BaseStream;

        internal protected int m_ByteIndex = 0, m_BitIndex = 0;

        internal protected bool m_LeaveOpen;

        internal protected Common.Binary.BitOrder m_BitOrder;

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
        /// Indicates if the <see cref="BitIndex"/> is aligned to a byte boundary.
        /// </summary>
        public bool IsAligned { get { return m_BitIndex % Common.Binary.BitsPerByte == 0; } }

        /// <summary>
        /// Gets or Sets the current <see cref="System.Byte"/> in the <see cref="Buffer"/> based on the <see cref="ByteIndex"/>
        /// </summary>
        public byte CurrentByte
        {
            get { return m_ByteCache[m_ByteIndex]; }
            set { m_ByteCache[m_ByteIndex] = value; }
        }

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
        /// Gets or sets the index in the bytes
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
            if (source == null) throw new System.ArgumentNullException("source");

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
            m_ByteIndex += System.Math.DivRem(bitCount, Binary.BitsPerByte, out m_BitIndex);
            m_BaseStream.Position += Common.Binary.BitsToBytes(ref bitCount);
        }

        /// <summary>
        /// Advances reading to the next byte boundary.
        /// </summary>
        public void ByteAlign()
        {
            m_BitIndex = 0;
            ++m_ByteIndex;
            //if(m_ByteIndex >= m_ByteCache.Count)
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
        /// Reads the given amount of bits into the cache.
        /// </summary>
        /// <param name="countOfBits">The amount of bits to read</param>
        internal void ReadBytesForBits(int countOfBits)
        {
            if (countOfBits <= 0) return;

            int bytesToRead = Common.Binary.BitsToBytes(countOfBits);

            if (bytesToRead > m_ByteCache.Count)
            {
                System.Array.Resize(ref m_ByteCache.m_Array, bytesToRead);
                m_ByteCache.IncreaseLength(bytesToRead);
            }

            if (m_ByteIndex + bytesToRead >= m_ByteCache.Count)
            {
                if(m_BitIndex > 0) Recycle();
                m_BitIndex = m_ByteIndex = 0;
            }

            int bytesRead = 0;

            while (bytesToRead > 0 && Remaining > 0)
            {
                bytesRead = m_BaseStream.Read(m_ByteCache.Array, m_ByteCache.Offset + m_ByteIndex + bytesRead, bytesToRead);

                bytesToRead -= bytesRead;
            }
        }

        /// <summary>
        /// Copies the bits which are left in the cache to the beginning of the cache
        /// </summary>
        internal protected void Recycle()
        {
            Common.Binary.CopyBitsTo(m_ByteCache.Array, m_ByteIndex, m_BitIndex, m_ByteCache.Array, 0, 0, Common.Binary.BytesToBits(m_ByteCache.Count - m_ByteIndex) + m_BitIndex);

            m_ByteIndex = m_BitIndex = 0;
        }
        
        public bool PeekBit()
        {
            return Common.Binary.GetBit(ref m_ByteCache.Array[m_ByteIndex], m_BitIndex);
        }

        public byte Peek8(bool reverse = false)
        {
            int bits = Media.Common.Binary.BytesToBits(ref m_ByteIndex) + m_BitIndex;

            switch (m_BitOrder)
            {
                case Binary.BitOrder.LeastSignificant:
                    return (byte)(reverse ? Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, Common.Binary.BitsPerByte) : Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, Common.Binary.BitsPerByte));
                case Binary.BitOrder.MostSignificant:
                    return (byte)(reverse ? Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, Common.Binary.BitsPerByte) : Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, Common.Binary.BitsPerByte));
                default: throw new System.NotSupportedException("Please create an issue for your use case");
            }
        }

        public short Peek16(bool reverse = false)
        {
            int bits = Media.Common.Binary.BytesToBits(ref m_ByteIndex) + m_BitIndex;

            switch (m_BitOrder)
            {
                case Binary.BitOrder.LeastSignificant:
                    return (short)(reverse ? Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, Common.Binary.BitsPerShort) : Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, Common.Binary.BitsPerShort));
                case Binary.BitOrder.MostSignificant:
                    return (short)(reverse ? Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, Common.Binary.BitsPerShort) : Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, Common.Binary.BitsPerShort));
                default: throw new System.NotSupportedException("Please create an issue for your use case");
            }
        }

        public int Peek24(bool reverse = false)
        {
            int bits = Media.Common.Binary.BytesToBits(ref m_ByteIndex) + m_BitIndex;

            switch (m_BitOrder)
            {
                case Binary.BitOrder.LeastSignificant:
                    return (int)(reverse ? Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, Common.Binary.TripleBitSize) : Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, Common.Binary.TripleBitSize));
                case Binary.BitOrder.MostSignificant:
                    return (int)(reverse ? Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, Common.Binary.TripleBitSize) : Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, Common.Binary.TripleBitSize));
                default: throw new System.NotSupportedException("Please create an issue for your use case");
            }
        }

        public int Peek32(bool reverse = false)
        {
            int bits = Media.Common.Binary.BytesToBits(ref m_ByteIndex) + m_BitIndex;

            switch (m_BitOrder)
            {
                case Binary.BitOrder.LeastSignificant:
                    return (int)(reverse ? Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, Common.Binary.BitsPerInteger) : Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, Common.Binary.BitsPerInteger));
                case Binary.BitOrder.MostSignificant:
                    return (int)(reverse ? Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, Common.Binary.BitsPerInteger) : Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, Common.Binary.BitsPerInteger));
                default: throw new System.NotSupportedException("Please create an issue for your use case");
            }
        }

        public long Peek64(bool reverse = false)
        {
            int bits = Media.Common.Binary.BytesToBits(ref m_ByteIndex) + m_BitIndex;

            switch (m_BitOrder)
            {
                case Binary.BitOrder.LeastSignificant:
                    return (long)(reverse ? Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, Common.Binary.BitsPerLong) : Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, Common.Binary.BitsPerLong));
                case Binary.BitOrder.MostSignificant:
                    return (long)(reverse ? Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, Common.Binary.BitsPerLong) : Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, Common.Binary.BitsPerLong));
                default: throw new System.NotSupportedException("Please create an issue for your use case");
            }
        }
        
        [System.CLSCompliant(false)]
        public ulong PeekBits(int count, bool reverse = false)
        {
            int bits = Media.Common.Binary.BytesToBits(ref m_ByteIndex) + m_BitIndex;

            switch (m_BitOrder)
            {
                case Binary.BitOrder.LeastSignificant:
                    return (reverse ? Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, count) : Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, count));
                case Binary.BitOrder.MostSignificant:
                    return (reverse ? Common.Binary.ReadBitsLSB(m_ByteCache.Array, bits, count) : Common.Binary.ReadBitsMSB(m_ByteCache.Array, bits, count));
                default: throw new System.NotSupportedException("Please create an issue for your use case");
            }
        }

        //Reading methods need to keep track of current index, as well as where data in the cache ends.

        public bool ReadBit()
        {
            try
            {
                return Common.Binary.GetBit(ref m_ByteCache.Array[m_ByteIndex], m_BitIndex);
            }
            finally
            {
                Common.Binary.ComputeBits(1, ref m_BitIndex, ref m_ByteIndex);
            }
        }

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
            }
        }

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
            }
        }

        public int Read24(bool reverse = false)
        {
            try
            {
                ReadBytesForBits(Common.Binary.TripleBitSize);

                return Peek24(reverse);
            }
            finally
            {
                Common.Binary.ComputeBits(24, ref m_BitIndex, ref m_ByteIndex);
            }
        }

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
            }
        }

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
            }
        }

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
            }
        }

        /// <summary>
        /// Reads 0 - 7 bytes into <see cref="Buffer"/> and returns an indication if the value was encoded correctly.
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

        public void CopyBits(int count, byte[] dest, int destByteOffset, int destBitOffset)
        {
            //Should accept dest and offsets for direct reads?
            //Would pass m_ByteIndex and m_BitIndex for normal cases.
            ReadBytesForBits(count);

            Common.Binary.CopyBitsTo(m_ByteCache.Array, m_ByteIndex, m_BitIndex, dest, destByteOffset, destBitOffset, count);
        }

        //ReadBigEndian16

        //ReadBigEndian32

        //ReadBigEndian64

        #endregion

        #region Overrides

        public override void Dispose()
        {
            if (IsDisposed || false == ShouldDispose) return;

            base.Dispose();

            m_ByteCache.Dispose();
            
            if (m_LeaveOpen) return;

            m_BaseStream.Dispose();
        }

        #endregion
    }
}

