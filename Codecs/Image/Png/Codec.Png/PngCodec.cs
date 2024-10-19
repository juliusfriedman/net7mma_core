using System;
using System.Text;
using Media.Codec;
using Media.Codec.Interfaces;
using Media.Codecs.Image;
using Media.Common;

namespace Codec.Png
{
    public class PngCodec : ImageCodec, IEncoder, IDecoder
    {
        const int ComponentCount = 4;

        public PngCodec()
            : base("PNG", Binary.ByteOrder.Little, ComponentCount, Binary.BitsPerByte)
        {
        }

        public override MediaType MediaTypes => MediaType.Image;

        public override bool CanEncode => true;

        public override bool CanDecode => true;

        public IEncoder Encoder => this;

        public IDecoder Decoder => this;

        public int Encode(PngImage image, Stream outputStream)
        {
            // Implement PNG encoding logic here
            throw new NotImplementedException();
        }

        public PngImage Decode(Stream inputStream)
        {
            // Implement PNG decoding logic here
            throw new NotImplementedException();
        }
    }
}