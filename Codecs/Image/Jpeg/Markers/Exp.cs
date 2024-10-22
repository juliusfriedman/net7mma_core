using Media.Codec.Jpeg;
using Media.Common;

namespace Codec.Jpeg.Markers;

public class Exp : Marker
{
    public new const int Length = 3;

    public int Eh
    {
        get
        {
            var bitOffset = Binary.BytesToBits(DataOffset);
            return (int)this.ReadBits(bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(DataOffset);
            this.WriteBits(ref bitOffset, Binary.Four, value, Binary.BitOrder.MostSignificant);
        }
    }

    public int Ev
    {
        get
        {
            var bitOffset = Binary.BytesToBits(DataOffset) + Binary.Four;
            return (int)this.ReadBits(bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(DataOffset) + Binary.Four;
            this.WriteBits(ref bitOffset, Binary.Four, value, Binary.BitOrder.MostSignificant);
        }
    }

    public Exp(MemorySegment data)
      : base(data)
    {
    }

    public Exp()
        : base(Media.Codec.Jpeg.Markers.Expand, LengthBytes + Length)
    {
    }
}