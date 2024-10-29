using System.IO;

namespace Media.Codec.Jpeg.Classes;

internal abstract class JpegScan
{
    protected JpegScan()
    {
    }

    protected readonly int BlockSize = JpegCodec.BlockSize;

    public abstract void Compress(JpegImage jpegImage, Stream output);

    public abstract void Decompress(JpegImage jpegImage);
}
