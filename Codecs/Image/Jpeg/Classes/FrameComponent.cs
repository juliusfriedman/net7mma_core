using Media.Common;

namespace Media.Codec.Jpeg.Classes;

public sealed class FrameComponent : MemorySegment
{
    /// <summary>
    /// The length of a <see cref="FrameComponent"/> in bytes.
    /// </summary>
    public const int Length = 3;

    /// <summary>
    /// Ci
    /// </summary>
    public int ComponentIdentifier
    {
        get => Array[Offset];
        set => Array[Offset] = (byte)value;
    }

    /// <summary>
    /// Hi
    /// </summary>
    public int HorizontalSamplingFactor
    {
        get
        {
            var bitOffset = Binary.BytesToBits(Offset + 1);
            return (int)this.ReadBits(ref bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitoffset = Binary.BytesToBits(Offset + 1);
            this.WriteBits(bitoffset, Binary.Four, value, Binary.BitOrder.MostSignificant);
        }
    }

    /// <summary>
    /// Vi
    /// </summary>
    public int VerticalSamplingFactor
    {
        get
        {
            var bitOffset = Binary.BytesToBits(Offset + 1) + Binary.Four;
            return (int)this.ReadBits(ref bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitoffset = Binary.BytesToBits(Offset + 1) + Binary.Four;
            this.WriteBits(bitoffset, Binary.Four, value, Binary.BitOrder.MostSignificant);
        }
    }

    /// <summary>
    /// Tqi
    /// </summary>
    public int QuantizationTableDestinationSelector
    {
        get => Array[Offset + 2];
        set => Array[Offset + 2] = (byte)value;
    }

    public FrameComponent(MemorySegment other)
        : base(other)
    {
    }

    public FrameComponent(int componentIdentifier, int horizontalSamplingFactor, int verticalSamplingFactor, int quantizationTableNumber)
        : base(Length)
    {
        ComponentIdentifier = componentIdentifier;
        HorizontalSamplingFactor = horizontalSamplingFactor;
        VerticalSamplingFactor = verticalSamplingFactor;
        QuantizationTableDestinationSelector = quantizationTableNumber;
    }
}
