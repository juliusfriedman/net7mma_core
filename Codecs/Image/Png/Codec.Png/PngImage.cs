using System;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks.Sources;
using Media.Codecs.Image;
using Media.Common;

namespace Codec.Png;

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
        // Read and validate the PNG signature
        using MemorySegment bytes = new MemorySegment(new byte[Binary.BytesPerLong]);
        if (Binary.BytesPerLong != stream.Read(bytes.Array, bytes.Offset, bytes.Count))
            throw new InvalidDataException("Not enough bytes for PNGSignature.");
        ulong signature = Binary.ReadU64(bytes.Array, bytes.Offset, Binary.IsLittleEndian);
        if (signature != PNGSignature)
            throw new InvalidDataException("The provided stream is not a valid PNG file.");

        // Read the IHDR chunk
        int width = 0, height = 0;
        ImageFormat? imageFormat = default;
        MemorySegment? dataSegment = default;
        byte colorType = default;
        ChunkHeader chunkHeader;
        while (stream.Position < stream.Length)
        {
            chunkHeader = new ChunkHeader(bytes.Array, bytes.Offset);

            if (ChunkHeader.ChunkHeaderLength != stream.Read(bytes.Array, bytes.Offset, ChunkHeader.ChunkHeaderLength))
                throw new InvalidDataException("Not enough bytes for chunk length.");

            string chunkType = chunkHeader.Name;

            if (chunkType == "IHDR")
            {
                if (bytes.Count != stream.Read(bytes.Array, bytes.Offset, bytes.Count))
                    throw new InvalidDataException("Not enough bytes for IHDR.");

                width = Binary.Read32(bytes.Array, bytes.Offset, Binary.IsLittleEndian);
                height = Binary.Read32(bytes.Array, bytes.Offset + Binary.BytesPerInteger, Binary.IsLittleEndian);
                int read = stream.ReadByte();
                if (read == -1)
                    throw new InvalidDataException("Not enough bytes to read bitDepth");
                byte bitDepth = (byte)read;
                read = stream.ReadByte();
                if (read == -1)
                    throw new InvalidDataException("Not enough bytes to read colorType");
                colorType = (byte)read;
                read = stream.ReadByte();
                if (read == -1)
                    throw new InvalidDataException("Not enough bytes to read compressionMethod");
                byte compressionMethod = (byte)read;
                read = stream.ReadByte();
                if (read == -1)
                    throw new InvalidDataException("Not enough bytes to read filterMethod");
                byte filterMethod = (byte)read;
                read = stream.ReadByte();
                if (read == -1)
                    throw new InvalidDataException("Not enough bytes to read interlaceMethod");
                byte interlaceMethod = (byte)read;

                // Create the image format based on the IHDR data
                imageFormat = CreateImageFormat(bitDepth, colorType);

                stream.Seek(Binary.BytesPerInteger, SeekOrigin.Current); // Skip the CRC

            }
            else if (chunkType == "IDAT")
            {
                // Read the image data
                dataSegment = new MemorySegment(chunkHeader.Length);

                if(chunkHeader.Length != stream.Read(dataSegment.Array, dataSegment.Offset, dataSegment.Count))
                    throw new InvalidDataException("Not enough bytes for IDAT.");

                stream.Seek(Binary.BytesPerInteger, SeekOrigin.Current); // Skip the CRC
            }
            else
            {
                // Skip the chunk data and CRC
                stream.Seek(chunkHeader.TotalLength, SeekOrigin.Current);
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
        Binary.Write32(ihdr.Array, offset, Binary.IsLittleEndian, Width);
        offset += Binary.BytesPerInteger;
        Binary.Write32(ihdr.Array, offset, Binary.IsLittleEndian, Height);
        offset += Binary.BytesPerInteger;
        Binary.Write8(ihdr.Array, offset++, Binary.IsBigEndian, (byte)ImageFormat.Size);
        Binary.Write8(ihdr.Array, offset++, Binary.IsBigEndian, ColorType);
        Binary.Write8(ihdr.Array, offset++, Binary.IsBigEndian, 0);
        Binary.Write8(ihdr.Array, offset++, Binary.IsBigEndian, 0);
        Binary.Write8(ihdr.Array, offset++, Binary.IsBigEndian, 0);
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