using System.IO;

namespace Media.Codec.Jpeg.Classes;

internal class LosslessScan : Scan
{
    public override void Compress(JpegImage jpegImage, Stream output)
    {
        throw new System.NotImplementedException();
    }

    public override void Decompress(JpegImage jpegImage)
    {
        throw new System.NotImplementedException();
    }
}