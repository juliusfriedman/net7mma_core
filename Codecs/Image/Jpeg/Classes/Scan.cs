using System.IO;

namespace Media.Codec.Jpeg.Classes;

internal abstract class Scan
{
    //Todo should scan hold memory or jpegState?

    protected Scan()
    {
    }

    protected readonly int BlockSize = JpegCodec.BlockSize;

    public abstract void Compress(JpegImage jpegImage, Stream output);

    public abstract void Decompress(JpegImage jpegImage);
}
