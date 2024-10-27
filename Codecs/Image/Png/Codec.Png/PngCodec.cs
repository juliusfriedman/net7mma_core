using Media.Codec.Interfaces;
using Media.Codecs.Image;
using Media.Common;
using System.Buffers.Binary;
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
        internal const int MaxBlockSize = ushort.MaxValue;

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
            Span<byte> bytes = stackalloc byte[ChunkHeader.ChunkHeaderLength];
            if (ChunkHeader.ChunkHeaderLength != inputStream.Read(bytes))
                throw new InvalidDataException("Not enough bytes for signature.");
            ulong signature = BinaryPrimitives.ReadUInt64BigEndian(bytes);
            if (signature != expectedSignature)
                throw new InvalidDataException($"Invalid signature, Expected: {expectedSignature}, Found: ${signature}");
            while (inputStream.Position < inputStream.Length)
            {
                using var chunk = Chunk.ReadChunk(inputStream);
                yield return chunk;
            }
        }

        internal static void WriteHeader(Stream stream, PngImage pngImage)
        {
            using var ihdr = new Chunks.Header();
            ihdr.Width = pngImage.Width;
            ihdr.Height = pngImage.Height;
            ihdr.BitDepth = pngImage.PngState.BitDepth;
            ihdr.ColourType = pngImage.PngState.ColourType;
            ihdr.CompressionMethod = pngImage.PngState.CompressionMethod;
            ihdr.FilterMethod = pngImage.PngState.FilterMethod;
            ihdr.InterlaceMethod = pngImage.PngState.InterlaceMethod;
            WriteChunk(stream, ihdr);
        }

        /// <summary>
        /// Mostly working but has unexpected end of data.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="pngImage"></param>
        /// <param name="compressionLevel"></param>
        internal static void WriteDataChunk(Stream stream, PngImage pngImage, CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            using (MemoryStream ms = new MemoryStream(pngImage.Data.Count))
            {
                using (DeflateStream deflateStream = new DeflateStream(ms, compressionLevel, true))
                {
                    deflateStream.Write(pngImage.Data.Array, pngImage.Data.Offset, pngImage.Data.Count);
                    deflateStream.Close();

                    ms.TryGetBuffer(out var buffer);

                    const int Cmf = 0x78;
                    int flg = 218;

                    // http://stackoverflow.com/a/2331025/277304
                    if ((int)compressionLevel >= 5 && (int)compressionLevel <= 6)
                    {
                        flg = 156;
                    }
                    else if ((int)compressionLevel >= 3 && (int)compressionLevel <= 4)
                    {
                        flg = 94;
                    }
                    else if ((int)compressionLevel <= 2)
                    {
                        flg = 1;
                    }

                    // Just in case
                    flg -= ((Cmf * 256) + flg) % 31;

                    if (flg < 0)
                    {
                        flg += 31;
                    }

                    // Create the data chunk memory segment
                    using (var idat = new Chunk(ChunkName.Data, buffer.Count + ZLibHeaderLength))
                    {
                        // Write the ZLib header.
                        idat[idat.DataOffset] = Cmf;
                        idat[idat.DataOffset + 1] = (byte)flg;

                        // Copy the compressed data.
                        Buffer.BlockCopy(buffer.Array!, buffer.Offset, idat.Array, idat.DataOffset + ZLibHeaderLength, buffer.Count);

                        //Wrote the data chunk
                        WriteChunk(stream, idat);
                    }
                }
            }
        }

        /// <summary>
        /// Not yet working 100% (unexpected end of data)
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="pngImage"></param>
        /// <param name="compressionLevel"></param>
        internal static void WriteDataChunks(Stream stream, PngImage pngImage, CompressionLevel compressionLevel = CompressionLevel.Optimal)
        {
            const int Cmf = 0x78;
            int flg = 218;

            // http://stackoverflow.com/a/2331025/277304
            if ((int)compressionLevel >= 5 && (int)compressionLevel <= 6)
            {
                flg = 156;
            }
            else if ((int)compressionLevel >= 3 && (int)compressionLevel <= 4)
            {
                flg = 94;
            }
            else if ((int)compressionLevel <= 2)
            {
                flg = 1;
            }

            // Just in case
            flg -= ((Cmf * 256) + flg) % 31;

            if (flg < 0)
            {
                flg += 31;
            }

            using (MemoryStream ms = new MemoryStream(pngImage.Data.Count))
            {
                using (DeflateStream deflateStream = new DeflateStream(ms, compressionLevel, true))
                {
                    //Compress the data
                    deflateStream.Write(pngImage.Data.Array, pngImage.Data.Offset, pngImage.Data.Count);

                    //Close the stream so headers are written.
                    deflateStream.Close();

                    //Get the compressed data buffer.
                    ms.TryGetBuffer(out var buffer);

                    var remains = buffer.Count;

                    var bufferOffset = buffer.Offset;

                    // Determine the size of the chunk
                    var chunkLength = Binary.Min(remains, MaxBlockSize);

                    // Create the chunk memory segment containing the zlib header.
                    using (var idat = new Chunk(ChunkName.Data, chunkLength + ZLibHeaderLength))
                    {
                        //Write the zlib header into the data
                        idat.Array[idat.DataOffset] = Cmf;
                        idat.Array[idat.DataOffset + 1] = (byte)flg;

                        // Copy the compressed data.
                        Buffer.BlockCopy(buffer.Array!, bufferOffset, idat.Array, idat.DataOffset + ZLibHeaderLength, chunkLength);

                        WriteChunk(stream, idat);

                        remains -= chunkLength;

                        bufferOffset += chunkLength;
                    }

                    while (remains > 0)
                    {
                        // Determine the size of the chunk
                        chunkLength = Binary.Min(remains, MaxBlockSize);

                        // Create the chunk memory segment
                        using (var idat = new Chunk(ChunkName.Data, chunkLength))
                        {
                            // Copy the compressed data.
                            Buffer.BlockCopy(buffer.Array!, bufferOffset, idat.Array, idat.DataOffset, chunkLength);

                            WriteChunk(stream, idat);
                            
                            remains -= chunkLength;

                            bufferOffset += chunkLength;
                        }
                    }
                }
            }
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