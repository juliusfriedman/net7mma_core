using Media.Codec.Interfaces;
using Media.Codecs.Image;
using Media.Common;

namespace Media.Codec.Png
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
            var position = outputStream.Position;
            // Implement PNG encoding logic here
            image.Save(outputStream);
            return (int)(outputStream.Position - position);
        }

        public PngImage Decode(Stream inputStream)
        {
            // Implement PNG decoding logic here
            return PngImage.FromStream(inputStream);
        }

        public static IEnumerable<Chunk> ReadChunks(Stream inputStream)
        {
            // Read and validate the PNG signature
            using MemorySegment bytes = new MemorySegment(new byte[Binary.BytesPerLong]);
            if (Binary.BytesPerLong != inputStream.Read(bytes.Array, bytes.Offset, bytes.Count))
                throw new InvalidDataException("Not enough bytes for PNGSignature.");
            ulong signature = Binary.ReadU64(bytes.Array, bytes.Offset, Binary.IsLittleEndian);
            if (signature != PngImage.PNGSignature)
                throw new InvalidDataException("The provided stream is not a valid PNG file.");

            while (inputStream.Position < inputStream.Length)
            {
                using var chunk = Chunk.ReadChunk(inputStream);
                yield return chunk;
            }
        }
    }
}