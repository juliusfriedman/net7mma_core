using Media.Common;

namespace Codec.Jpeg.Classes;

public sealed class ScanComponentSelectorType : MemorySegment
{
    /// <summary>
    /// The amount of bytes in a <see cref="ScanComponentSelectorType"/>.
    /// </summary>
    public const int Length = 2;

    /// <summary>
    /// Scan component selector.
    /// </summary>
    public byte Csj
    {
        get => Array[Offset];
        set => Array[Offset] = value;
    }

    /// <summary>
    /// Entropy coding table selector DC.
    /// </summary>
    public byte Tdj
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
    /// Entropy coding table selector AC.
    /// </summary>
    public byte Taj
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

    /// <summary>
    /// Constructs a new <see cref="ScanComponentSelectorType"/> instance from the given <paramref name="other"/> <see cref="MemorySegment"/>.
    /// </summary>
    /// <param name="other">Data which corresponds to a <see cref="ScanComponentSelectorType"/></param>
    public ScanComponentSelectorType(MemorySegment other):
        base(other)
    {
    }

    public ScanComponentSelectorType()
        : base(Length)
    {
    }
}
