using Media.Common;

namespace Codec.Jpeg.Classes;

public sealed class ScanComponentSelectorType : MemorySegment
{   
    /// <summary>
    /// Csj
    /// </summary>
    public byte ScanComponentSelector
    {
        get => Array[Offset];
        set => Array[Offset] = value;
    }

    /// <summary>
    /// Tdj
    /// </summary>
    public byte EntropyCodingTableSelectorDC
    {
        get
        {
            var bitOffset = Binary.BytesToBits(Offset + 1);
            return (byte)Binary.ReadBits(RawData.Array, bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(Offset + 1);
            Binary.WriteBits(RawData.Array, bitOffset, Binary.Four, value, Binary.BitOrder.MostSignificant);
        }
    }

    /// <summary>
    /// Taj
    /// </summary>
    public byte EntropyCodingTableSelectorAC
    {
        get
        {
            var bitOffset = Binary.BytesToBits(Offset + 1) + Binary.Four;
            return (byte)Binary.ReadBits(RawData.Array, bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(Offset + 1) + Binary.Four;
            Binary.WriteBits(RawData.Array, bitOffset, Binary.Four, value, Binary.BitOrder.MostSignificant);
        }
    }

    /// <summary>
    /// This Raw Data of this <see cref="ScanComponentSelectorType"/> which should be blittable.
    /// </summary>
    public MemorySegment RawData => new MemorySegment(Array, Offset, Binary.Two);

    public ScanComponentSelectorType(MemorySegment other):
        base(other)
    {
    }
}
