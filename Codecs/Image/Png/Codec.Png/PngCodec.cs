using Media.Codec.Interfaces;
using Media.Codecs.Image;
using Media.Common;
using System.IO.Compression;
using System.IO.Hashing;

namespace Media.Codec.Png
{
    public class PngCodec : ImageCodec, IEncoder, IDecoder
    {
        internal const int ComponentCount = 4;
        internal const byte ZLibHeaderLength = 2;
        internal const byte Deflate32KbWindow = 120;
        internal const byte ChecksumBits = 1;

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

        public static IEnumerable<Chunk> ReadChunks(Stream inputStream, ulong expectedSignature = PngImage.PNGSignature)
        {
            // Read and validate the PNG signature
            using MemorySegment bytes = new MemorySegment(new byte[Binary.BytesPerLong]);
            if (Binary.BytesPerLong != inputStream.Read(bytes.Array, bytes.Offset, bytes.Count))
                throw new InvalidDataException("Not enough bytes for PNGSignature.");
            ulong signature = Binary.ReadU64(bytes.Array, bytes.Offset, Binary.IsLittleEndian);
            if (signature != expectedSignature)
                throw new InvalidDataException("The provided stream is not a valid PNG file.");

            while (inputStream.Position < inputStream.Length)
            {
                using var chunk = Chunk.ReadChunk(inputStream);
                yield return chunk;
            }
        }

        internal static void WriteHeader(Stream stream, PngImage pngImage)
        {
            using var ihdr = new Chunk(ChunkName.Header, 13);
            var offset = ihdr.DataOffset;
            Binary.Write32(ihdr.Array, ref offset, Binary.IsLittleEndian, pngImage.Width);
            Binary.Write32(ihdr.Array, ref offset, Binary.IsLittleEndian, pngImage.Height);
            Binary.Write8(ihdr.Array, ref offset, Binary.IsBigEndian, pngImage.PngState.BitDepth);
            Binary.Write8(ihdr.Array, ref offset, Binary.IsBigEndian, pngImage.PngState.ColorType);
            Binary.Write8(ihdr.Array, ref offset, Binary.IsBigEndian, pngImage.PngState.CompressionMethod);
            Binary.Write8(ihdr.Array, ref offset, Binary.IsBigEndian, pngImage.PngState.FilterMethod);
            Binary.Write8(ihdr.Array, ref offset, Binary.IsBigEndian, pngImage.PngState.InterlaceMethod);
            WriteChunk(stream, ihdr);
        }

        internal static void WriteData(Stream stream, PngImage pngImage, CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            Chunk idat;
            using (MemoryStream ms = new MemoryStream(pngImage.Data.Count))
            {
                using (DeflateStream deflateStream = new DeflateStream(ms, compressionLevel, true))
                {
                    deflateStream.Write(pngImage.Data.Array, pngImage.Data.Offset, pngImage.Data.Count);
                    deflateStream.Close();

                    ms.TryGetBuffer(out var buffer);

                    // Create the chunk memory segment
                    idat = new Chunk(ChunkName.Data, buffer.Count + ZLibHeaderLength);

                    // Write the ZLib header.
                    idat[idat.DataOffset] = Deflate32KbWindow;
                    idat[idat.DataOffset + 1] = ChecksumBits;

                    // Copy the compressed data.
                    Buffer.BlockCopy(buffer.Array!, buffer.Offset, idat.Array, idat.DataOffset + ZLibHeaderLength, buffer.Count);

                    //Todo calculate the CRC and write to idat.
                }
            }
            WriteChunk(stream, idat);
            idat.Dispose();
        }

        internal static void WriteEnd(Stream stream)
        {
            using var iend = new Chunk(ChunkName.End, 0);
            WriteChunk(stream, iend);
        }

        internal static void WriteChunk(Stream stream, Chunk chunk)
        {
            Crc32 crc32 = new();
            using var header = chunk.Header;
            var headerSpan = header.ToSpan();
            crc32.Append(headerSpan.Slice(ChunkHeader.LengthBytes, ChunkHeader.NameLength));
            using var chunkData = chunk.Data;
            var chunkSpan = chunkData.ToSpan();
            crc32.Append(chunkSpan);
            var expectedCrc = crc32.GetCurrentHashAsUInt32();
            chunk.Crc = (int)expectedCrc;
            stream.Write(chunk.Array, chunk.Offset, chunk.Count);
        }
    }
}