using Media.Container;
using System;

public abstract class MediaFileWriter : MediaFileStream
{
    protected MediaFileWriter(Uri location, System.IO.FileAccess access = System.IO.FileAccess.ReadWrite)
        : base(location, access)
    {
    }

    // Implement methods for writing video frames and audio samples

    //public abstract Node CreateHeader();
    //public abstract void WriteNode(Node node);

    public abstract void WriteHeader();

    public abstract void WriteVideoFrame(byte[] frameData);

    public abstract void WriteAudioSamples(byte[] audioData);
}