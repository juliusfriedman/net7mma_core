using Media.Codec;
using Media.Codec.Interfaces;
using Media.Codecs.Image;
using Media.Common;

namespace Codec.Bmp
{
    public class BmpCodec : ImageCodec, IEncoder, IDecoder
    {
        const int ComponentCount = 4;
        
        public BmpCodec()
            : base("BMP", Binary.SystemByteOrder, ComponentCount, Binary.BitsPerByte)
        {
        }

        public override MediaType MediaTypes => MediaType.Image;

        public override bool CanEncode => true;

        public override bool CanDecode => true;

        public IEncoder Encoder => this;

        public IDecoder Decoder => this;

        public int Encode(BmpImage image, Stream outputStream)
        {
            image.Save(outputStream);
            return (int)image.BitmapHeader.FileSize;
        }

        public BmpImage Decode(Stream inputStream)
        {
            return BmpImage.FromStream(inputStream);
        }
    }
}