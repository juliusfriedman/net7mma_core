using Media.Common;

namespace Media.Codec.Jpeg.Segments;

public class Expand : Marker
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

    public Expand(MemorySegment data)
      : base(data)
    {
    }

    public Expand()
        : base(Media.Codec.Jpeg.Markers.Expand, LengthBytes + Length)
    {
    }
}