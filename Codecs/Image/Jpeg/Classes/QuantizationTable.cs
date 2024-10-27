using Media.Common;

namespace Codec.Jpeg.Classes;

internal class QuantizationTable : MemorySegment
{
    /// <summary>
    /// The length of <see cref="Pq"/> and <see cref="Tq"/>
    /// </summary>
    public const int Length = 1;

    public QuantizationTable(int pq, int tq) 
        : base(Length + (pq == 0 ? 64 : 128))
    {
        Pq = pq;
        Tq = tq;
    }

    public QuantizationTable(MemorySegment segment) 
        : base(segment)
    {

    }

    /// <summary>
    ///  Quantization table element precision – Specifies the precision of the Qk values. 
    ///  Value 0 indicates 8-bit Qk values; value 1 indicates 16-bit Qk values.
    /// </summary>
    public int Pq
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
    /// Quantization table destination identifier.
    /// Specifies one of four possible destinations at the decoder into which the quantization table shall be installed.
    /// </summary>
    public int Tq
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
    ///  Quantization table elements.
    ///  Specifies the kth element out of 64 elements, where k is the index in the zigzag ordering of the DCT coefficients.
    ///  The quantization elements shall be specified in zig-zag scan order.
    /// </summary>
    public MemorySegment Qk
    {
        get => this.Slice(Offset + Length, TableLength);
        set => value.CopyTo(Array, Offset + TableLength);
    }

    /// <summary>
    /// The amount of bytes contained in the <see cref="Qk"/> segment.
    /// </summary>
    public int TableLength => Pq == 0 ? 64 : 128;

    /// <summary>
    /// The total amount of bytes contained.
    /// </summary>
    public int TotalLength => Length + TableLength;
}
