using System.IO.Compression;
using System.Numerics;
using System.Text;
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

    private static byte[] CalculateCrc(string chunkType, byte[] chunkData)
    {
        var chunkTagSize = Encoding.ASCII.GetByteCount(chunkType);
        var ms = new MemorySegment(chunkTagSize + chunkData.Length);
        Encoding.ASCII.GetBytes(chunkType).CopyTo(ms.Array, chunkTagSize);
        uint checksum = 0;
        foreach(var data in ms.Array)
            checksum = (checksum >> 8) ^ BitOperations.Crc32C(checksum, data);
        return Binary.GetBytes(checksum, BitConverter.IsLittleEndian);
    }


    public readonly byte ColorType;

    public PngImage(ImageFormat imageFormat, int width, int height)
        : base(imageFormat, width, height, new PngCodec())
    {
    }

    private PngImage(ImageFormat imageFormat, int width, int height, MemorySegment data)
        : base(imageFormat, width, height, data)
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
        MemorySegment bytes = new MemorySegment(new byte[Binary.BytesPerLong]);
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
        while (stream.Position < stream.Length)
        {
            if (Binary.BytesPerInteger != stream.Read(bytes.Array, bytes.Offset, bytes.Count))
                throw new InvalidDataException("Not enough bytes for chunk length.");

            int chunkLength = Binary.Read32(bytes.Array, bytes.Offset, Binary.IsLittleEndian);
            string chunkType = Encoding.ASCII.GetString(bytes.Array, Binary.BitsPerInteger, Binary.BitsPerInteger);

            if (chunkType == "IHDR")
            {
                if (bytes.Count != stream.Read(bytes.Array, bytes.Offset, bytes.Count))
                    throw new InvalidDataException("Not enough bytes for IHDR.");

                width = Binary.Read32(bytes.Array, bytes.Offset, Binary.IsLittleEndian);
                height = Binary.Read32(bytes.Array, bytes.Offset + Binary.BitsPerInteger, Binary.IsLittleEndian);
                int read = stream.ReadByte();
                if (read == -1)
                    throw new InvalidDataException("Not enough bytes to read bitDepth");
                byte bitDepth = (byte)read;
                if (read == -1)
                    throw new InvalidDataException("Not enough bytes to read colorType");
                colorType = (byte)read;
                if (read == -1)
                    throw new InvalidDataException("Not enough bytes to read compressionMethod");
                byte compressionMethod = (byte)read;
                if (read == -1)
                    throw new InvalidDataException("Not enough bytes to read filterMethod");
                byte filterMethod = (byte)read;
                if (read == -1)
                    throw new InvalidDataException("Not enough bytes to read interlaceMethod");
                byte interlaceMethod = (byte)read;

                // Create the image format based on the IHDR data
                imageFormat = CreateImageFormat(bitDepth, colorType);
            }
            else if (chunkType == "IDAT")
            {
                // Read the image data
                dataSegment = new MemorySegment(chunkLength);

                if(chunkLength != stream.Read(dataSegment.Array, dataSegment.Offset, dataSegment.Count))
                    throw new InvalidDataException("Not enough bytes for IDAT.");                
            }
            else
            {
                // Skip the chunk data and CRC
                stream.Seek(chunkLength + 4, SeekOrigin.Current);
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

        // Write the IHDR chunk
        WriteChunk(stream, "IHDR", WriteIHDRChunk);

        // Write the IDAT chunk
        WriteChunk(stream, "IDAT", WriteIDATChunk);

        // Write the IEND chunk
        WriteChunk(stream, "IEND", WriteIENDChunk);
    }

    private void WriteIHDRChunk(Stream stream)
    {
        stream.Write(BitConverter.GetBytes(Width).Reverse().ToArray());
        stream.Write(BitConverter.GetBytes(Height).Reverse().ToArray());
        stream.Write(Binary.GetBytes(ImageFormat.Size, Binary.IsLittleEndian));
        stream.WriteByte(ColorType);
        stream.WriteByte(0); // Compression method
        stream.WriteByte(0); // Filter method
        stream.WriteByte(0); // Interlace method
    }

    private void WriteChunk(Stream writer, string chunkType, Action<Stream> writeChunkData)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            writeChunkData(writer);
            byte[] chunkData = ms.ToArray();
            writer.Write(BitConverter.GetBytes(chunkData.Length).Reverse().ToArray());
            writer.Write(Encoding.ASCII.GetBytes(chunkType));
            writer.Write(chunkData);
            writer.Write(CalculateCrc(chunkType, chunkData));
        }
    }

    private void WriteIDATChunk(Stream stream)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            using (DeflateStream deflateStream = new DeflateStream(ms, CompressionLevel.Optimal, true))
            {
                deflateStream.Write(Data.Array, Data.Offset, Data.Count);
            }
            ms.Seek(0, SeekOrigin.Begin);
            ms.CopyTo(stream);
        }
    }

    private void WriteIENDChunk(Stream stream)
    {
        // IEND chunk has no data
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
}