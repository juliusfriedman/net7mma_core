using Media.Common;
using System;
using System.Linq;

namespace Codec.Jpeg.Classes;

internal class DerivedTable : MemorySegment
{
    public static DerivedTable Create(ReadOnlySpan<byte> bits, ReadOnlySpan<byte> val)
    {
        int nsymbols = 0;
        for (int len = Length; len <= CodeLength; len++)
            nsymbols += bits[len];

        var derivedTable = new DerivedTable(nsymbols + Length + CodeLength);

        bits.CopyTo(derivedTable.Bits);

        val.CopyTo(new Span<byte>(derivedTable.HuffVal, 0, nsymbols));

        var span = derivedTable.ToSpan();

        span = span.Slice(Length);

        bits.CopyTo(span);

        span = span.Slice(CodeLength);

        val.CopyTo(span);

        return derivedTable;
    }

    public const int Length = 1;

    public const int CodeLength = 16;

    /* These two fields directly represent the contents of a JPEG DHT marker */
    internal readonly byte[] Bits = new byte[17];     /* bits[k] = # of symbols with codes of */

    /* length k bits; bits[0] is unused */
    internal readonly byte[] HuffVal = new byte[256];     /* The symbols, in order of incr code length */

    public DerivedTable(int size) : base(size)
    {
    }

    public DerivedTable(byte index, byte[] bits, byte[] huffval, int size) : base(new byte[Length + CodeLength + size])
    {
        Array[Offset] = index;
        Bits = bits;
        HuffVal = huffval;
    }

    /// <summary>
    /// Table class, 0 = DC table or lossless table, 1 = AC table
    /// Baseline 0 or 1, Progressive DCT or Lossless = 0
    /// </summary>
    public int Te
    {
        get
        {
            var bitOffset = Binary.BytesToBits(Offset);
            return (int)this.ReadBits(bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(Offset);
            this.WriteBits(bitOffset, Binary.Four, (uint)value, Binary.BitOrder.MostSignificant);
        }
    }

    /// <summary>
    /// Huffman table destination identifier, (one of four possible destination identifiers).
    /// Baseline 0 or 1, Extended 0 to 3
    /// </summary>
    public int Th
    {
        get
        {
            var bitOffset = Binary.BytesToBits(Offset) + Binary.Four;
            return (int)this.ReadBits(bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(Offset) + Binary.Four;
            this.WriteBits(bitOffset, Binary.Four, (uint)value, Binary.BitOrder.MostSignificant);
        }
    }

    /// <summary>
    /// Number of Huffman codes of length i bits (1-16)
    /// </summary>
    public MemorySegment Li
    {
        get => this.Slice(Offset + Length, CodeLength);
        set => value.CopyTo(Array, Offset + Length, CodeLength);
    }

    public int CodeLengthSum 
    {
        get
        {
            using var slice = Li;
            return slice.Sum(li => li);
        }
    }

    /// <summary>
    /// Values associated with each Huffman code of length i bits (1-16)
    /// </summary>
    public MemorySegment Vi
    {
        get => this.Slice(Offset + Length +  CodeLength, CodeLengthSum);
        set => value.CopyTo(Array, Offset + Length + CodeLength, CodeLengthSum);
    }
}
