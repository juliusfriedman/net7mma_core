using System.IO.Compression;
using System.Numerics;
using Media.Codecs.Image;
using Media.Common;

namespace Media.Codec.Png;

public class PngImage : Image
{
    public const ulong PNGSignature = 0x89504E470D0A1A0A;

    private static ImageFormat CreateImageFormat(byte bitDepth, byte colorType)
    {
        switch (colorType)
        {
            case 0: // Grayscale
            case 3: // Indexed-color
            case 4: // Grayscale with alpha
                return ImageFormat.Monochrome(bitDepth);
            case 2: // Truecolor (RGB)
                return ImageFormat.RGB(bitDepth);            
            case 6: // Truecolor with alpha (RGBA)
                return ImageFormat.RGBA(bitDepth);
            default:
                throw new NotSupportedException($"Color type {colorType} is not supported.");
        }
    }

    private static byte[] CalculateCrc(IEnumerable<byte> bytes)
    {
        uint checksum = 0;
        foreach (var b in bytes) 
            checksum = (checksum >> 8) ^ BitOperations.Crc32C(checksum, b);
        return Binary.GetBytes(checksum, BitConverter.IsLittleEndian);
    }


    public readonly byte ColorType;

    public PngImage(ImageFormat imageFormat, int width, int height)
        : base(imageFormat, width, height, new PngCodec())
    {
    }

    private PngImage(ImageFormat imageFormat, int width, int height, MemorySegment data)
        : base(imageFormat, width, height, data, new PngCodec())
    {
    }

    private PngImage(ImageFormat imageFormat, int width, int height, MemorySegment data, byte colorType)
        : this(imageFormat, width, height, data)
    {
        ColorType = colorType;
    }

    public static PngImage FromStream(Stream stream)
    {
        // Read the IHDR chunk
        int width = 0, height = 0;
        ImageFormat? imageFormat = default;
        MemorySegment? dataSegment = default;
        byte colorType = default;

        foreach(var chunk in PngCodec.ReadChunks(stream))
        {
            switch (chunk.ChunkName)
            {
                case ChunkNames.Header:
                    var offset = chunk.DataOffset;
                    width = Binary.Read32(chunk.Array, ref offset, Binary.IsLittleEndian);
                    height = Binary.Read32(chunk.Array, ref offset, Binary.IsLittleEndian);
                    byte bitDepth = chunk.Array[offset++];
                    colorType = chunk.Array[offset++];
                    byte compressionMethod = chunk.Array[offset++];
                    byte filterMethod = chunk.Array[offset++];
                    byte interlaceMethod = chunk.Array[offset++];

                    // Create the image format based on the IHDR data
                    imageFormat = CreateImageFormat(bitDepth, colorType);
                    //ToDo, Crc is in data segment, so we need to read it and validate it.
                    continue;
                case ChunkNames.Data:
                    //ToDo, Crc is in data segment, so we need to read it and validate it.
                    dataSegment = chunk.Data.Slice(0);
                    continue;
                case ChunkNames.End:
                    continue;
                default:
                    continue;
            }
        }

        if(imageFormat == null || dataSegment == null)
            throw new InvalidDataException("The provided stream does not contain valid PNG image data.");

        // Create and return the PngImage
        return new PngImage(imageFormat, width, height, dataSegment, colorType);
    }

    public void Save(Stream stream)
    {
        // Write the PNG signature
        stream.Write(Binary.GetBytes(PNGSignature, BitConverter.IsLittleEndian));

        //TODO have Chunks class to make handling of reading and write more robust
        //Should implement like MarkerReader and MarkerWriter in Codec.Jpeg

        // Write the IHDR chunk
        WriteIHDRChunk(stream);

        // Write the IDAT chunk
        WriteIDATChunk(stream);

        // Write the IEND chunk
        WriteIENDChunk(stream);
    }

    private void WriteIHDRChunk(Stream stream)
    {
        using var ihdr = new Chunk("IHDR", 13);
        var offset = ihdr.DataOffset;
        Binary.Write32(ihdr.Array, ref offset, Binary.IsLittleEndian, Width);
        Binary.Write32(ihdr.Array, ref offset, Binary.IsLittleEndian, Height);
        Binary.Write8(ihdr.Array, ref offset, Binary.IsBigEndian, (byte)ImageFormat.Size);
        Binary.Write8(ihdr.Array, ref offset, Binary.IsBigEndian, ColorType);
        Binary.Write8(ihdr.Array, ref offset, Binary.IsBigEndian, 0);
        Binary.Write8(ihdr.Array, ref offset, Binary.IsBigEndian, 0);
        Binary.Write8(ihdr.Array, ref offset, Binary.IsBigEndian, 0);
        stream.Write(ihdr.Array, ihdr.Offset, ihdr.Count);
    }

    private void WriteIDATChunk(Stream stream, CompressionLevel compressionLevel = CompressionLevel.Optimal)
    {
        Chunk idat;
        using (MemoryStream ms = new MemoryStream(Data.Count))
        {
            using (DeflateStream deflateStream = new DeflateStream(ms, compressionLevel, true))
            {
                deflateStream.Write(Data.Array, Data.Offset, Data.Count);
            }
            ms.Seek(0, SeekOrigin.Begin);
            ms.TryGetBuffer(out var buffer);
            idat = new Chunk("IDAT", buffer.Count);
            buffer.CopyTo(idat.Array, idat.DataOffset);            
        }
        stream.Write(idat.Array, idat.Offset, idat.Count);
    }

    private void WriteIENDChunk(Stream stream)
    {
        var iend = new Chunk("IEND", 0);
        stream.Write(iend.Array, iend.Offset, iend.Count);
    }

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
}