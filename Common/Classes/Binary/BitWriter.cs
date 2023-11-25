namespace Media.Common//.Binary
{
    /// <summary>
    /// Allows for writing bits from a <see cref="System.IO.Stream"/> with a variable sized buffer
    /// </summary>
    public class BitWriter : Common.BaseDisposable
    {
        #region Fields

        internal readonly MemorySegment m_ByteCache;

        internal readonly System.IO.Stream m_BaseStream;

        internal Binary.BitOrder m_BitOrder = Binary.SystemBitOrder;

        int m_ByteIndex = 0, m_BitIndex = 0;

        internal bool m_LeaveOpen;

        #endregion

        #region Properties

        //IsAligned

        /// <summary>
        /// Gets or sets a value which indicates if the <see cref="BaseStream"/> should be closed on <see cref="Dispose"/>
        /// </summary>
        public bool LeaveOpen { get { return m_LeaveOpen; } set { m_LeaveOpen = value; } }

        /// <summary>
        /// Gets a value which indicates the amount of bytes which are available to write flushing to the <see cref="BaseStream"/>
        /// </summary>
        public int BytesRemaining { get { return m_ByteCache.Count - m_ByteIndex; } }

        /// <summary>
        /// Gets a value which indicates the amount of bits which remain in the current Byte.
        /// </summary>
        public int BitsRemaining { get { return Common.Binary.BitsPerByte - m_BitIndex; } }

        /// <summary>
        /// Gets a value which indicates the amount of bytes which are available without calling <see cref="Flush"/>
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
        public bool IsAligned { get { return m_BitIndex % Common.Binary.BitsPerByte is 0; } }

        /// <summary>
        /// Gets the <see cref="System.IO.Stream"/> from which the data is written.
        /// </summary>
        public System.IO.Stream BaseStream { get { return m_BaseStream; } }

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
        /// Gets or Sets the underlying <see cref="Binary.BitOrder"/> which is used to read values.
        /// </summary>
        public Common.Binary.BitOrder BitOrder { get { return m_BitOrder; } set { m_BitOrder = value; } }

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

        #endregion

        #region Constructor / Destructor

        /// <summary>
        /// Creates an instance with the specified properties
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="writable"></param>
        /// <param name="bitOrder"></param>
        /// <param name="bitOffset"></param>
        /// <param name="byteOffset"></param>
        /// <param name="leaveOpen"></param>
        public BitWriter(byte[] buffer, bool writable, Common.Binary.BitOrder bitOrder, int bitOffset, int byteOffset, bool leaveOpen = false)
            : this(buffer, writable, bitOrder, leaveOpen)
        {
            m_BitIndex = bitOffset;

            m_ByteIndex = byteOffset;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="writable"></param>
        /// <param name="bitOrder"></param>
        /// <param name="cacheSize"></param>
        /// <param name="leaveOpen"></param>
        public BitWriter(byte[] buffer, bool writable, Common.Binary.BitOrder bitOrder, bool leaveOpen = false) : base(true)
        {
            m_BaseStream = new System.IO.MemoryStream(buffer, writable);

            m_LeaveOpen = leaveOpen;

            m_ByteCache = new MemorySegment(buffer);

            m_BitOrder = bitOrder;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="bitOrder"></param>
        /// <param name="cacheSize"></param>
        /// <param name="leaveOpen"></param>
        public BitWriter(System.IO.Stream source, Common.Binary.BitOrder bitOrder, int cacheSize = 32, bool leaveOpen = false)
            : base(true)
        {
            if (source is null) throw new System.ArgumentNullException("source");

            m_BaseStream = source;

            m_LeaveOpen = leaveOpen;

            m_ByteCache = new MemorySegment(cacheSize);

            m_BitOrder = bitOrder;
        }

        /// <summary>
        /// Constructs an instance of the writer using the specified options and the <see cref="Common.Binary.SystemByteOrder"/>
        /// </summary>
        /// <param name="source">The underlying <see cref="System.IO.Stream"/></param>
        /// <param name="cacheSize">The amount of bytes to be used for writing before <see cref="Flush"/> is called.</param>
        /// <param name="leaveOpen">Indicates if the <paramref name="source"/> should be left open when calling <see cref="Dispose"/></param>
        public BitWriter(System.IO.Stream source, int cacheSize = 32, bool leaveOpen = false)
            : this(source, Common.Binary.SystemBitOrder, cacheSize, leaveOpen)
        {

        }

        ~BitWriter() { Dispose(); }

        #endregion

        #region Methods

        /// <summary>
        /// Advances reading to the next byte boundary.
        /// </summary>
        public void ByteAlign()
        {
            m_BitIndex = Common.Binary.Zero;
            ++m_ByteIndex;
        }

        /// <summary>
        /// Writes all data from the <see cref="Cache"/> to the <see cref="BaseStream"/>
        /// </summary>
        public void Flush()
        {
            int toWrite = m_ByteIndex;

            if (m_BitIndex > 0) ++toWrite;

            if (toWrite <= 0) return;

            m_BaseStream.Write(m_ByteCache.Array, m_ByteCache.Offset, toWrite);

            m_ByteIndex = m_BitIndex = Common.Binary.Zero;
        }

        /// <summary>
        /// Write a bit to the <see cref="Cache"/>
        /// </summary>
        /// <param name="value"></param>
        public void WriteBit(bool value)
        {
            try
            {
                //Set the bit and move the bit index
                Binary.ExchangeBit(ref m_ByteCache.Array[m_ByteIndex], m_BitIndex, value);
            }
            finally
            {
                Common.Binary.ComputeBits(Common.Binary.One, ref m_BitIndex, ref m_ByteIndex);
            }
        }

        /// <summary>
        /// Write a sbyte to the <see cref="Cache"/>
        /// </summary>
        /// <param name="value"></param>
        /// <param name="reverse"></param>
        [System.CLSCompliant(false)]
        public void WriteS8(sbyte value, bool reverse = false)
        {
            try
            {
                switch (m_BitOrder)
                {
                    case Binary.BitOrder.LeastSignificant:
                        if (reverse) Common.Binary.WriteBitsMSB(m_ByteCache.Array, m_BitIndex, (byte)value, Common.Binary.BitsPerByte);
                        else Common.Binary.WriteBitsLSB(m_ByteCache.Array, m_BitIndex, (byte)value, Common.Binary.BitsPerByte);
                        return;
                    case Binary.BitOrder.MostSignificant:
                        if (reverse) Common.Binary.WriteBitsLSB(m_ByteCache.Array, m_BitIndex, (byte)value, Common.Binary.BitsPerByte);
                        else Common.Binary.WriteBitsMSB(m_ByteCache.Array, m_BitIndex, (byte)value, Common.Binary.BitsPerByte);
                        return;
                    default: throw new System.NotSupportedException("Please create an issue for your use case");
                }
            }
            finally
            {
                Common.Binary.ComputeBits(Common.Binary.BitsPerByte, ref m_BitIndex, ref m_ByteIndex);
            }
        }

        /// <summary>
        /// Write a byte to the <see cref="Cache"/>
        /// </summary>
        /// <param name="value"></param>
        /// <param name="reverse"></param>
        public void Write8(byte value, bool reverse = false)
        {
            try
            {
                switch (m_BitOrder)
                {
                    case Binary.BitOrder.LeastSignificant:
                        if (reverse) Common.Binary.WriteBitsMSB(m_ByteCache.Array, m_BitIndex, value, Common.Binary.BitsPerByte);
                        else Common.Binary.WriteBitsLSB(m_ByteCache.Array, m_BitIndex, value, Common.Binary.BitsPerByte);
                        return;
                    case Binary.BitOrder.MostSignificant:
                        if (reverse) Common.Binary.WriteBitsLSB(m_ByteCache.Array, m_BitIndex, value, Common.Binary.BitsPerByte);
                        else Common.Binary.WriteBitsMSB(m_ByteCache.Array, m_BitIndex, value, Common.Binary.BitsPerByte);
                        return;
                    default: throw new System.NotSupportedException("Please create an issue for your use case");
                }
            }
            finally
            {
                Common.Binary.ComputeBits(Common.Binary.BitsPerByte, ref m_BitIndex, ref m_ByteIndex);
            }
        }

        /// <summary>
        /// Write a ulong to the <see cref="Cache"/>
        /// </summary>
        /// <param name="value"></param>
        /// <param name="reverse"></param>
        [System.CLSCompliant(false)]
        public void WriteU64(ulong value, bool reverse = false)
        {
            try
            {
                switch (m_BitOrder)
                {
                    case Binary.BitOrder.LeastSignificant:
                        if (reverse) Common.Binary.WriteBitsMSB(m_ByteCache.Array, m_BitIndex, value, Common.Binary.BitsPerLong);
                        else Common.Binary.WriteBitsLSB(m_ByteCache.Array, m_BitIndex, value, Common.Binary.BitsPerLong);
                        return;
                    case Binary.BitOrder.MostSignificant:
                        if (reverse) Common.Binary.WriteBitsLSB(m_ByteCache.Array, m_BitIndex, value, Common.Binary.BitsPerLong);
                        else Common.Binary.WriteBitsMSB(m_ByteCache.Array, m_BitIndex, value, Common.Binary.BitsPerLong);
                        return;
                    default: throw new System.NotSupportedException("Please create an issue for your use case");
                }
            }
            finally
            {
                Common.Binary.ComputeBits(Common.Binary.BitsPerLong, ref m_BitIndex, ref m_ByteIndex);
            }
        }

        /// <summary>
        /// Write a long to the <see cref="Cache"/>
        /// </summary>
        /// <param name="value"></param>
        /// <param name="reverse"></param>
        public void Write64(long value, bool reverse = false)
        {
            try
            {
                switch (m_BitOrder)
                {
                    case Binary.BitOrder.LeastSignificant:
                        if (reverse) Common.Binary.WriteBitsMSB(m_ByteCache.Array, m_BitIndex, (ulong)value, Common.Binary.BitsPerLong);
                        else Common.Binary.WriteBitsLSB(m_ByteCache.Array, m_BitIndex, (ulong)value, Common.Binary.BitsPerLong);
                        return;
                    case Binary.BitOrder.MostSignificant:
                        if (reverse) Common.Binary.WriteBitsLSB(m_ByteCache.Array, m_BitIndex, (ulong)value, Common.Binary.BitsPerLong);
                        else Common.Binary.WriteBitsMSB(m_ByteCache.Array, m_BitIndex, (ulong)value, Common.Binary.BitsPerLong);
                        return;
                    default: throw new System.NotSupportedException("Please create an issue for your use case");
                }
            }
            finally
            {
                Common.Binary.ComputeBits(Common.Binary.BitsPerLong, ref m_BitIndex, ref m_ByteIndex);
            }
        }

        /// <summary>
        /// Writes UTF8 compliant data
        /// </summary>
        /// <param name="val"></param>
        [System.CLSCompliant(false)]
        public void WriteUTF8(uint val)
        {
            if (val < 0x80)
            {
                Write8((byte)val);
                return;
            }
            int bytes = (Binary.Log2i(val) + Common.Binary.Quattuor) / Common.Binary.Quinque;
            int shift = (bytes - 1) * Common.Binary.Sex;
            WriteBits(Common.Binary.BitsPerByte, (Binary.TrīgintāDuoBitSize - ((uint)Binary.TrīgintāDuoBitSize >> bytes)) | (val >> shift));
            while (shift >= Common.Binary.Sex)
            {
                shift -= Common.Binary.Sex;
                Write8((byte)(0x80 | ((val >> shift) & Common.Binary.SixBitMaxValue)));
            }
        }

        //For Flac.BitWriter
        ///// <summary>
        ///// Writes unary binary data
        ///// </summary>
        ///// <param name="val"></param>
        //public void WriteUnarySigned(int val, bool reverse = false)
        //{
        //    // convert signed to unsigned
        //    int v = -Binary.Two * val - Binary.One;
        //    v ^= (v >> Binary.ThirtyOne);

        //    // write quotient in unary
        //    int q = v + Binary.One;

        //    while (q > Binary.ThirtyOne)
        //    {
        //        WriteBits(Binary.ThirtyOne, Binary.Zero, reverse);
        //        q -= Binary.ThirtyOne;
        //    }

        //    WriteBits(q, Binary.One, reverse);
        //}

        /// <summary>
        /// Writes an arbitary amount of bits from val
        /// </summary>
        /// <param name="bits"></param>
        /// <param name="val"></param>
        public void WriteBits(int bits, long val, bool reverse = false)
        {
            WriteBits(bits, (ulong)val, reverse);
        }

        /// <summary>
        /// Writes an abitary amount of bits from val
        /// </summary>
        /// <param name="bits"></param>
        /// <param name="value"></param>
        /// <param name="reverse"></param>
        [System.CLSCompliant(false)]
        public void WriteBits(int bits, ulong value, bool reverse = false)
        {
            try
            {
                switch (m_BitOrder)
                {
                    case Binary.BitOrder.LeastSignificant:
                        if (reverse) Common.Binary.WriteBitsMSB(m_ByteCache.Array, m_BitIndex, value, bits);
                        else Common.Binary.WriteBitsLSB(m_ByteCache.Array, m_BitIndex, value, bits);
                        return;
                    case Binary.BitOrder.MostSignificant:
                        if (reverse) Common.Binary.WriteBitsLSB(m_ByteCache.Array, m_BitIndex, value, bits);
                        else Common.Binary.WriteBitsMSB(m_ByteCache.Array, m_BitIndex, value, bits);
                        return;
                    default: throw new System.NotSupportedException("Please create an issue for your use case");
                }
            }
            finally
            {
                Common.Binary.ComputeBits(bits, ref m_BitIndex, ref m_ByteIndex);
            }
        }

        /// <summary>
        /// Writs an arbitary amount of bits from the <paramref name="buffer"/>
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="byteOffset"></param>
        /// <param name="bitOffset"></param>
        /// <param name="bitCount"></param>
        public void WriteBits(byte[] buffer, int byteOffset, int bitOffset, int bitCount)
        {
            try
            {
                int bytes = Common.Binary.BitsToBytes(ref bitCount), toCopy = Common.Binary.Min(bytes, m_ByteCache.Array.Length);

                while (bytes > 0)
                {
                    Binary.CopyBitsTo(buffer, byteOffset, bitOffset, m_ByteCache.Array, m_ByteCache.Offset, m_BitIndex, toCopy);
                    Flush();
                    bytes -= toCopy;
                }
            }
            finally
            {
                Common.Binary.ComputeBits(bitCount, ref m_BitIndex, ref m_ByteIndex);
            }
        }

        #endregion

        #region Overrides

        public override void Dispose()
        {
            //Write remaining bits
            Flush();

            if (IsDisposed) return;

            base.Dispose();

            m_ByteCache.Dispose();

            if (m_LeaveOpen) return;

            m_BaseStream.Dispose();
        }

        #endregion

    }
}
