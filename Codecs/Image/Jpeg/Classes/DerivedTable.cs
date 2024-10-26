using Media.Common;
using System.Linq;

namespace Codec.Jpeg.Classes;

internal class DerivedTable : MemorySegment
{
    public const int Length = 1;

    public const int CodeLength = 16;

    public DerivedTable(MemorySegment segment) : base(segment)
    {
    }

    public DerivedTable(int size) : base(new byte[Length + size])
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
        get => this.Slice(Offset + CodeLength, CodeLengthSum);
        set => value.CopyTo(Array, Offset + CodeLength, CodeLengthSum);
    }
}
