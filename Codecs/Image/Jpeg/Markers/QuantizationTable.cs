using Media.Common;

namespace Media.Codec.Jpeg;

public class QuantizationTable : Marker
{
    public new const int Length = 1;

    public QuantizationTable(int size) : base(Markers.QuantizationTable, LengthBytes + Length + size)
    {

    }

    public QuantizationTable(MemorySegment segment)
        : base(segment)
    {

    }

    /// <summary>
    /// Quantization table element precision.
    /// 0 indicates 8 bit values, 1 indicates 16 bit values.
    /// </summary>
    public int Pq
    {
        get
        {
            var bitOffset = Binary.BytesToBits(DataOffset);
            return (int)this.ReadBits(bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(DataOffset);
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
            var bitOffset = Binary.BytesToBits(DataOffset) + Binary.Four;
            return (int)this.ReadBits(bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(DataOffset) + Binary.Four;
            this.WriteBits(bitOffset, Binary.Four, (uint)value, Binary.BitOrder.MostSignificant);
        }
    }

    public MemorySegment Qk
    {
        get => this.Slice(DataOffset + 1);
        set => value.CopyTo(Array, DataOffset + 1);
    }
}