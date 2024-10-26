using Media.Common;

namespace Media.Codec.Jpeg.Segments;

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
