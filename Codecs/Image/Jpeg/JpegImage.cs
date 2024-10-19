using System.IO;
using System;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using Media.Codecs.Image;
using Media.Common;
using Media.Codecs.Image.Jpeg;
using System.Net.WebSockets;
using System.Linq;

namespace Codec.Jpeg;

public class JpegImage : Image
{
    public readonly byte ColorType;

    public JpegImage(ImageFormat imageFormat, int width, int height)
        : base(imageFormat, width, height, new JpegCodec())
    {
    }

    private JpegImage(ImageFormat imageFormat, int width, int height, MemorySegment data)
        : base(imageFormat, width, height, data)
    {
    }

    private JpegImage(ImageFormat imageFormat, int width, int height, MemorySegment data, byte colorType)
        : this(imageFormat, width, height, data)
    {
        ColorType = colorType;
    }

    public static JpegImage FromStream(Stream stream)
    {
        using var markerRead = new MarkerReader(stream);

        // Read the SOF0 (Start of Frame) marker
        int width = 0, height = 0;
        ImageFormat? imageFormat = default;
        MemorySegment? dataSegment = default;
        byte colorType = default;
        foreach (var marker in markerRead.ReadMarkers())
        {
            if (marker.Code == Markers.StartOfBaselineFrame) // SOF0 marker
            {
                var offset = 3;
                byte bitDepth = marker.Data[offset++];
                height = Binary.Read16(marker.Data, offset, Binary.IsLittleEndian);
                offset += 2;
                width = Binary.Read16(marker.Data, offset, Binary.IsLittleEndian);
                offset += 2;
                colorType = marker.Data[offset];

                // Create the image format based on the SOF0 data
                imageFormat = CreateImageFormat(bitDepth, colorType);
            }
            else if (marker.Code == Markers.StartOfScan) // SOS marker
            {
                // Read the image data
                dataSegment = new MemorySegment((int)(stream.Length - stream.Position));

                if (dataSegment.Count != stream.Read(dataSegment.Array, dataSegment.Offset, dataSegment.Count))
                    throw new InvalidDataException("Not enough bytes for image data.");
                break;
            }
            else
            {
                stream.Seek(marker.Length - Binary.BytesPerShort, SeekOrigin.Current);
            }
        }

        if (imageFormat == null || dataSegment == null)
            throw new InvalidDataException("The provided stream does not contain valid JPEG image data.");

        // Create and return the JpegImage
        return new JpegImage(imageFormat, width, height, dataSegment, colorType);
    }

    private static ImageFormat CreateImageFormat(byte bitDepth, byte colorType)
    {
        switch (colorType)
        {
            case 1: // Grayscale
                return ImageFormat.Monochrome(bitDepth);
            case 3: // YCbCr
                return ImageFormat.YUV(bitDepth);
            case 4: // CMYK
                return ImageFormat.CMYK(bitDepth);
            default:
                throw new NotSupportedException($"Color type {colorType} is not supported.");
        }
    }

    public void Save(Stream stream)
    {
        // Write the JPEG signature
        WriteMarker(stream, Markers.StartOfInformation, WriteEmptyMarker);

        // Write the SOF0 marker
        WriteMarker(stream, Markers.StartOfBaselineFrame, WriteSOF0Marker);

        // Write the SOS marker
        WriteMarker(stream, Markers.StartOfScan, WriteSOSMarker);

        // Write the image data
        stream.Write(Data.Array, Data.Offset, Data.Count);

        // Write the EOI marker
        WriteMarker(stream, Markers.EndOfInformation, WriteEmptyMarker);
    }

    private void WriteSOF0Marker(Stream stream)
    {
        stream.Write(Binary.GetBytes((ushort)(8 + 3 * ImageFormat.Components.Length), Binary.IsLittleEndian));
        stream.WriteByte((byte)ImageFormat.Size);
        stream.Write(Binary.GetBytes((ushort)Height, Binary.IsLittleEndian));
        stream.Write(Binary.GetBytes((ushort)Width, Binary.IsLittleEndian));
        stream.WriteByte(ColorType);
        for (int i = 0; i < ImageFormat.Components.Length; i++)
        {
            stream.WriteByte((byte)(i + 1)); // Component ID
            stream.WriteByte(0x11); // Sampling factors
            stream.WriteByte(0); // Quantization table number
        }        
    }

    private void WriteMarker(Stream writer, byte functionCode, Action<Stream> writeMarkerData)
    {
        Marker marker = new Marker();
        marker.PrefixLength = 1;
        marker.Code = functionCode;
        using (MemoryStream ms = new MemoryStream())
        {
            writeMarkerData(ms);
            ms.TryGetBuffer(out var markerData);
            marker.Length = markerData.Count;
            marker.Data = markerData.Array;
            writer.Write(marker.Prepare().ToArray());
        }
    }

    private void WriteSOSMarker(Stream stream)
    {
        stream.Write(Binary.GetBytes((ushort)(6 + 2 * ImageFormat.Components.Length), Binary.IsLittleEndian));
        stream.WriteByte((byte)ImageFormat.Components.Length);
        for (int i = 0; i < ImageFormat.Components.Length; i++)
        {
            stream.WriteByte((byte)(i + 1)); // Component ID
            stream.WriteByte(0); // Huffman table number
        }
        stream.WriteByte(0); // Start of spectral selection
        stream.WriteByte(63); // End of spectral selection
        stream.WriteByte(0); // Successive approximation
    }

    private void WriteEmptyMarker(Stream stream)
    {
        // EOI marker has no data
    }

    public MemorySegment GetPixelDataAt(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return MemorySegment.Empty;

        // JPEG format stores pixels from top to bottom
        int bytesPerPixel = ImageFormat.Length;
        int rowSize = Width * bytesPerPixel;
        int offset = (y * rowSize) + (x * bytesPerPixel);

        return Data.Slice(offset, ImageFormat.Length);
    }

    public Vector<byte> GetVectorDataAt(int x, int y)
    {
        // JPEG format stores pixels from top to bottom
        int bytesPerPixel = ImageFormat.Length;
        int rowSize = Width * bytesPerPixel;
        int offset = (y * rowSize) + (x * bytesPerPixel);
        offset -= offset % Vector<byte>.Count; // Align the offset to vector size
        return new Vector<byte>(Data.Array, Data.Offset + offset);
    }

    public void SetVectorDataAt(int x, int y, Vector<byte> vectorData)
    {
        // JPEG format stores pixels from top to bottom
        int bytesPerPixel = ImageFormat.Length;
        int rowSize = Width * bytesPerPixel;
        int offset = (y * rowSize) + (x * bytesPerPixel);
        offset -= offset % Vector<byte>.Count; // Align the offset to vector size
        vectorData.CopyTo(new Span<byte>(Data.Array, Data.Offset + offset, Vector<byte>.Count));
    }
}