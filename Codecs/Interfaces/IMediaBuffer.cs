namespace Media.Codec.Interfaces
{
    public interface IMediaBuffer
    {
        ICodec Codec { get; }

        Media.Common.MemorySegment Data { get; }

        int SampleCount { get; }
    }
}
