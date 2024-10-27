using Media.Common;
using System;
using System.Linq;

namespace Codec.Jpeg.Classes;

/// <summary>
/// A class which represents a single Huffman table.
/// </summary>
internal class HuffmanTable : MemorySegment
{
    /// <summary>
    /// Creates a <see cref="HuffmanTable"/> from the given bits and values.
    /// </summary>
    /// <param name="bits"></param>
    /// <param name="values"></param>
    /// <returns></returns>
    public static HuffmanTable Create(ReadOnlySpan<byte> bits, ReadOnlySpan<byte> values)
    {
        int numberOfSymbols = 0;
        for (int len = Length; len <= CodeLength; len++)
            numberOfSymbols += bits[len];

        var derivedTable = new HuffmanTable(numberOfSymbols + Length + CodeLength);

        bits.CopyTo(derivedTable.Bits);

        values.CopyTo(new Span<byte>(derivedTable.Values, 0, numberOfSymbols));

        var span = derivedTable.ToSpan();

        span = span.Slice(Length);

        bits.Slice(Length).CopyTo(span);

        span = span.Slice(CodeLength);

        values.CopyTo(span);

        return derivedTable;
    }

    public const int Length = 1;

    public const int CodeLength = 16;

    /* These two fields directly represent the contents of a JPEG DHT marker */
    internal readonly byte[] Bits = new byte[Length + CodeLength];     /* bits[k] = # of symbols with codes of */

    /* length k bits; bits[0] is unused */
    internal readonly byte[] Values = new byte[256];     /* The symbols, in order of incr code length */

    public HuffmanTable(int size) : base(size)
    {
    }

    public HuffmanTable(MemorySegment segment) : base(segment)
    {
    }   

    public HuffmanTable(byte index, byte[] bits, byte[] huffval, int size) : base(new byte[Length + CodeLength + size])
    {
        Array[Offset] = index;
        Bits = bits;
        Values = huffval;
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

    /// <summary>
    /// The sum of bytes of <see cref="Li"/>.
    /// Indicates the length of <see cref="Vi"/>.
    /// </summary>
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

    /// <summary>
    /// The total length of the <see cref="HuffmanTable"/> in bytes.
    /// </summary>
    public int TotalLength => Length + CodeLength + CodeLengthSum;
}
