namespace Media.Codecs.Audio.Ac3
{
    public class SyncFrame
    {
        // [ATSC Digital Audio Compression (AC-3) Standard]
        // "Each synchronization frame contains 6 coded audio blocks (AB), each of which represent 256 new audio samples"
        public const uint SampleCount = 1536;
    }
}
