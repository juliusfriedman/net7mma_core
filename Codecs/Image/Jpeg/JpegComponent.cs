using Media.Codec;

namespace Codec.Jpeg;

public class JpegComponent : MediaComponent
{
    public readonly byte QuantizationTableNumber;

    public JpegComponent(byte quantizationTableNumber, byte id, int size)
        : base(id, size)
            => QuantizationTableNumber = quantizationTableNumber;

    public override int GetHashCode()
        => System.HashCode.Combine(base.GetHashCode(), QuantizationTableNumber);
}
