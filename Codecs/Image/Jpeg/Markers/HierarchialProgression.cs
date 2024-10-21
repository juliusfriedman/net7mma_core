using Media.Codec.Jpeg;
using Media.Common;

namespace Codec.Jpeg.Markers;

public class HierarchialProgression : Marker
{
    public new const int Length = 3;

    public int Eh
    {
        get
        {
            var bitOffset = Binary.BytesToBits(Data.Offset);
            return (int)this.ReadBits(bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(Data.Offset);
            this.WriteBits(ref bitOffset, Binary.Four, Binary.BitOrder.MostSignificant, value);
        }
    }

    public int Ev
    {
        get
        {
            var bitOffset = Binary.BytesToBits(Data.Offset) + Binary.Four;
            return (int)this.ReadBits(bitOffset, Binary.Four, Binary.BitOrder.MostSignificant);
        }
        set
        {
            var bitOffset = Binary.BytesToBits(Data.Offset) + Binary.Four;
            this.WriteBits(ref bitOffset, Binary.Four, Binary.BitOrder.MostSignificant, value);
        }
    }

    public HierarchialProgression()
        : base(Media.Codec.Jpeg.Markers.HierarchialProgression, Length)
    {
    }

    public HierarchialProgression(MemorySegment data)
      : base(data)
    {
    }
}
