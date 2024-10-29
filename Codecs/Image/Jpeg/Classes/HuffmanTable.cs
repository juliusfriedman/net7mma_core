using Media.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Media.Codec.Jpeg.Classes;

/// <summary>
/// A class which represents a single Huffman table.
/// </summary>
internal class HuffmanTable : MemorySegment
{
    public const int Length = 1;

    public const int CodeLength = 16;

    private readonly Dictionary<byte, (ushort code, byte length)> _codeTable = new();

    internal void BuildCodeTable()
    {
        if (_codeTable.Count > 0)
        {
            return;
        }
        using var Bits = Li;
        using var Values = Vi;

        int code = 0;
        int valueIndex = 0;

        for (int i = 0; i < Bits.Count; i++)
        {
            int bitLength = Bits[i];

            for (int j = 0; j < bitLength; j++)
            {
                byte value = Values[valueIndex++];
                _codeTable[value] = ((ushort)code, (byte)(i + 1));
                code++;
            }

            code <<= 1;
        }
    }

    public (ushort code, byte length) GetCode(byte value)
    {
        if (_codeTable.TryGetValue(value, out var codeInfo))
        {
            return codeInfo;
        }

        throw new ArgumentException($"Value {value} not found in Huffman table.");
    }

    public bool TryGetCode(int code, out (ushort code, byte length) value)
    {
        if (_codeTable.TryGetValue((byte)code, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    public HuffmanTable(ReadOnlySpan<byte> bits, ReadOnlySpan<byte> values) 
        : base(Length +bits.Length + values.Length)
    {
        using var li = Li;
        bits.CopyTo(li.ToSpan());
        using var vi = Vi;
        values.CopyTo(vi.ToSpan());
    }

    public HuffmanTable(int size) : base(Length + size)
    {
    }

    public HuffmanTable(MemorySegment segment) : base(segment)
    {
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
        get => this.Slice(Length, CodeLength);
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
        get => this.Slice(Length + CodeLength, CodeLengthSum);
        set => value.CopyTo(Array, Length + CodeLength, CodeLengthSum);
    }

    /// <summary>
    /// The total length of the <see cref="HuffmanTable"/> in bytes.
    /// </summary>
    public int TotalLength => Length + CodeLength + CodeLengthSum;
}
