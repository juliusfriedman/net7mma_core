using Media.Common;

namespace Codec.Jpeg.Classes;

public class FrameComponent : MemorySegment
{
    /// <summary>
    /// The length of a <see cref="FrameComponent"/> in bytes.
    /// </summary>
    public const int Length = 3;

    public int ComponentIdentifier
    {
        get => Array[Offset];
        set => Array[Offset] = (byte)value;
    }
    public int HorizontalSamplingFactor
    {
        get
        {
            var bitOffset= Binary.BytesToBits(Offset + 1);
            return (int)this.ReadBits(ref bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitoffset = Binary.BytesToBits(Offset + 1);
            this.WriteBits(bitoffset, Binary.Four, value, Binary.BitOrder.MostSignificant);
        }
    }

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
    public int QuantizationTableDestinationSelector
    {
        get => Count > 3 ? Array[Offset + 3] : 0;
        set => Array[Offset + 3] = (byte)value;
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
