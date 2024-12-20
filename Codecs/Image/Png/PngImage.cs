﻿using System.IO.Compression;
using System.IO.Hashing;
using System.Numerics;
using Media.Codecs.Image;
using Media.Common;
using Media.Common.Collections.Generic;

namespace Media.Codec.Png;

public class PngImage : Image
{
    #region Constants

    //https://en.wikipedia.org/wiki/JPEG_Network_Graphics
    //https://en.wikipedia.org/wiki/Multiple-image_Network_Graphics
    public const ulong PNGSignature = 0x89504E470D0A1A0A;
    public const ulong MNGSignature = 0x8A4D4E470D0A1A0A;
    public const ulong JNGSignature = 0x8B4A4E470D0A1A0A;

    #endregion

    #region Private Static Functions

    private static ImageFormat CreateImageFormat(byte bitDepth, byte colorType)
    {
        switch (colorType)
        {
            case 0: // Grayscale
                return ImageFormat.Monochrome(bitDepth);
            case 3: // Indexed-color            
                return ImageFormat.Palette(bitDepth);
            case 4: // Grayscale with alpha
                return ImageFormat.WithPreceedingAlphaComponent(ImageFormat.Monochrome(bitDepth / 2), bitDepth / 2);
            case 2: // Truecolor (RGB)
                return ImageFormat.RGB(bitDepth / 3);            
            case 6: // Truecolor with alpha (RGBA)
                return ImageFormat.RGBA(bitDepth / 4);
            default:
                throw new NotSupportedException($"Color type {colorType} is not supported.");
        }
    }

    private static ColourType ResolveColourType(ImageFormat imageFormat)
    {
        switch (imageFormat.Components.Length)
        {
            case 1:
                return 
                    imageFormat.Components[0].Id == ImageFormat.PaletteChannelId 
                    ? ColourType.Palette 
                    : ColourType.Grayscale;
            case 2:
                return ColourType.GrayscaleWithAlpha;
            case 3:
                return ColourType.Rgb;
            case 4:
                return ColourType.RgbWithAlpha;
            default: throw new ArgumentException("Cannot determine ColorType from ImageFormat.");
        }
    }

    #endregion

    #region Fields    

    internal readonly PngState PngState;

    public readonly ConcurrentThesaurus<ChunkName, Chunk>? Chunks;

    #endregion

    #region Constructors

    /// <summary>
    /// Construct a new instance with the given format, width and height.
    /// </summary>
    /// <param name="imageFormat"></param>
    /// <param name="width"></param>
    /// <param name="height"></param>
    public PngImage(ImageFormat imageFormat, int width, int height, ulong fileSignature = PNGSignature)
        : base(imageFormat, width, height, new PngCodec())
    {
        PngState = new PngState()
        {
            FileSignature = fileSignature,
            BitDepth = (byte)imageFormat.Size,
            ColourType = (byte)ResolveColourType(imageFormat)
        };
    }

    /// <summary>
    /// Construct an instance from existing data
    /// </summary>
    /// <param name="imageFormat"></param>
    /// <param name="width"></param>
    /// <param name="height"></param>
    /// <param name="data"></param>
    private PngImage(ImageFormat imageFormat, int width, int height, MemorySegment data, PngState pngState)
        : base(imageFormat, width, height, data, new PngCodec())
    {
        PngState = pngState;
    }

    private PngImage(ImageFormat imageFormat, int width, int height, MemorySegment data, PngState pngState, ConcurrentThesaurus<ChunkName, Chunk> chunks)
        : this(imageFormat, width, height, data, pngState)
    {
        Chunks = chunks;
    }

    #endregion

    #region Reading

    public static PngImage FromStream(Stream stream, ulong expectedSignature = PNGSignature)
    {
        int width = 0, height = 0;
        ImageFormat? imageFormat = default;
        var dataSegments = new SegmentStream();
        var dataSegment = MemorySegment.Empty;
        PngState pngState = new()
        {
            FileSignature = expectedSignature,
        };
        var chunks = new ConcurrentThesaurus<ChunkName, Chunk>();
        var crc32 = new Crc32();
        foreach (var chunk in PngCodec.ReadChunks(stream, expectedSignature))
        {
            var actualCrc = chunk.Crc;
            if (actualCrc > 0)
            {
                crc32.Reset();
                using var header = chunk.Header;
                var headerSpan = header.ToSpan();
                crc32.Append(headerSpan.Slice(ChunkHeader.LengthBytes, ChunkHeader.NameLength));
                using var chunkData = chunk.Data;
                var chunkSpan = chunkData.ToSpan();
                crc32.Append(chunkSpan);
                var expectedCrc = crc32.GetCurrentHashAsUInt32();
                if (actualCrc > 0 && expectedCrc != actualCrc)
                    throw new InvalidDataException($"There is an error in the provided stream Crc32 failed, Expected: ${expectedCrc}, Found: {actualCrc}.");
            }

            var chunkName = chunk.ChunkName;
            switch (chunkName)
            {
                case ChunkName.Header:
                    {
                        using var header = new Chunks.Header(chunk);
                        width = header.Width;
                        height = header.Height;
                        pngState.BitDepth = header.BitDepth;
                        pngState.ColourType = header.ColourType;
                        pngState.CompressionMethod = header.CompressionMethod;
                        pngState.FilterMethod = header.FilterMethod;
                        pngState.InterlaceMethod = header.InterlaceMethod;
                        // Create the image format based on the IHDR data
                        imageFormat = CreateImageFormat(pngState.BitDepth, pngState.ColourType);
                        continue;
                    }
                case ChunkName.Data:
                    {
                        if (dataSegments.Length == 0)
                        {
                            //Todo, need option to not read zlib headers.

                            //Zlib header, http://tools.ietf.org/html/rfc1950
                            //Compression Method and flags
                            var cmf = chunk[chunk.DataOffset];
                            var flg = chunk[chunk.DataOffset + 1];

                            var offset = chunk.DataOffset + PngCodec.ZLibHeaderLength;

                            //Preset dictionary.
                            bool fdict = (flg & 32) != 0;
                            if (fdict)
                            {
                                offset += Chunk.ChecksumLength;
                            }

                            dataSegment = chunk.Slice(offset);

                            dataSegments.AddMemory(dataSegment);
                        }
                    }
                    continue;
                case ChunkName.End:
                    using (MemoryStream decompressedStream = new MemoryStream())
                    {
                        using (DeflateStream deflateStream = new DeflateStream(dataSegments, CompressionMode.Decompress))
                        {
                            try
                            {
                                deflateStream.CopyTo(decompressedStream);
                                decompressedStream.TryGetBuffer(out var buffer);
                                dataSegment = new MemorySegment(buffer.Array, buffer.Offset, buffer.Count);
                            }
                            catch (InvalidDataException)
                            {
                                throw;
                            }
                        }
                    }
                    goto LoadImage;
                default:
                    chunks.Add(chunkName, chunk);
                    continue;
            }
        }

    LoadImage:

        if (imageFormat == null || dataSegment.Count == 0)
            throw new InvalidDataException("The provided stream does not contain valid PNG image data.");

        // Create and return the PngImage
        return new PngImage(imageFormat, width, height, dataSegment, pngState, chunks);
    }

    #endregion

    #region Writing    

    public void Save(Stream stream, CompressionLevel compressionLevel = CompressionLevel.Optimal)
    {
        // Write the file signature
        stream.Write(Binary.GetBytes(PngState.FileSignature, BitConverter.IsLittleEndian));

        // Write the IHDR chunk
        PngCodec.WriteHeader(stream, this);

        //Write any chunks we found while processing
        if (Chunks != null)
        {
            foreach (var chunk in Chunks.Values)
            {
                PngCodec.WriteChunk(stream, chunk);
            }
        }

        // Write multiple data chunks if needed.
        PngCodec.WriteDataChunks(stream, this, compressionLevel);

        // Write the IEND chunk
        PngCodec.WriteEnd(stream);
    }

    #endregion

    #region Pixel Data Access

    public MemorySegment GetPixelDataAt(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return MemorySegment.Empty;

        // PNG format stores pixels from top to bottom
        int bytesPerPixel = ImageFormat.Length;
        int rowSize = Width * bytesPerPixel;
        int offset = (y * rowSize) + (x * bytesPerPixel);

        return Data.Slice(offset, ImageFormat.Length);
    }

    public Vector<byte> GetVectorDataAt(int x, int y)
    {
        // PNG format stores pixels from top to bottom
        int bytesPerPixel = ImageFormat.Length;
        int rowSize = Width * bytesPerPixel;
        int offset = (y * rowSize) + (x * bytesPerPixel);
        offset -= offset % Vector<byte>.Count; // Align the offset to vector size
        return new Vector<byte>(Data.Array, Data.Offset + offset);
    }

    public void SetVectorDataAt(int x, int y, Vector<byte> vectorData)
    {
        // PNG format stores pixels from top to bottom
        int bytesPerPixel = ImageFormat.Length;
        int rowSize = Width * bytesPerPixel;
        int offset = (y * rowSize) + (x * bytesPerPixel);
        offset -= offset % Vector<byte>.Count; // Align the offset to vector size
        vectorData.CopyTo(new Span<byte>(Data.Array, Data.Offset + offset, Vector<byte>.Count));
    }

    #endregion
}