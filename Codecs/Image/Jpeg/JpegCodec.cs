using System.Collections.Generic;
using System.IO;
using Media.Codec;
using Media.Codec.Interfaces;
using Media.Codecs.Image;
using Media.Codecs.Image.Jpeg;
using Media.Common;

namespace Codec.Jpeg
{
    public class JpegCodec : ImageCodec, IEncoder, IDecoder
    {
        const int ComponentCount = 3;

        public JpegCodec()
            : base("JPEG", Binary.ByteOrder.Little, ComponentCount, Binary.BitsPerByte)
        {
        }

        public override MediaType MediaTypes => MediaType.Image;

        public override bool CanEncode => true;

        public override bool CanDecode => true;

        public IEncoder Encoder => this;
        public IDecoder Decoder => this;

        public int Encode(JpegImage image, Stream outputStream)
        {
            var position = outputStream.Position;
            // Implement PNG encoding logic here
            image.Save(outputStream);
            return (int)(outputStream.Position - position);
        }

        public JpegImage Decode(Stream inputStream)
        {
            // Implement PNG decoding logic here
            return JpegImage.FromStream(inputStream);
        }

        public static IEnumerable<Marker> ReadMarkers(Stream inputStream)
        {
            MarkerReader markerReader = new MarkerReader(inputStream);
            foreach (var marker in markerReader.ReadMarkers())
                yield return marker;
        }
    }
}