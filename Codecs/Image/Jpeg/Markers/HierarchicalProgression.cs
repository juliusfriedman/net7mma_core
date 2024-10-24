using Media.Common;

namespace Codec.Jpeg.Markers;

public class HeirarchicalProgression : StartOfFrame
{
    public HeirarchicalProgression(int componentCount) 
        : base(Media.Codec.Jpeg.Markers.HeirarchicalProgression, componentCount)
    {
    }

    public HeirarchicalProgression(MemorySegment data)
        : base(data)
    {
    }
}
