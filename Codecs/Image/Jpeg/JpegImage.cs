using System.IO;
using System;
using System.Numerics;
using Media.Codecs.Image;
using Media.Common;
using System.Linq;
using Media.Common.Collections.Generic;
using Codec.Jpeg;

namespace Media.Codec.Jpeg;

public class JpegImage : Image
{
    public readonly bool Progressive;
    public readonly ConcurrentThesaurus<byte, Marker> Markers;

    public JpegImage(ImageFormat imageFormat, int width, int height)
        : base(imageFormat, width, height, new JpegCodec())
    {
    }

    private JpegImage(ImageFormat imageFormat, int width, int height, MemorySegment data, bool progressive, ConcurrentThesaurus<byte, Marker> markers)
        : base(imageFormat, width, height, data, new JpegCodec())
    {
        Progressive = progressive;
        Markers = markers;

    }

    public static JpegImage FromStream(Stream stream)
    {
        // Read the SOF0 (Start of Frame) marker
        int width = 0, height = 0;
        ImageFormat imageFormat = default;
        MemorySegment dataSegment = default;
        bool progressive = false;
        Common.Collections.Generic.ConcurrentThesaurus<byte, Marker> markers = new Common.Collections.Generic.ConcurrentThesaurus<byte, Marker>();
        foreach (var marker in JpegCodec.ReadMarkers(stream))
        {
            if (marker.FunctionCode == Jpeg.Markers.StartOfBaselineFrame || marker.FunctionCode == Jpeg.Markers.StartOfProgressiveHuffmanFrame) // SOF0 marker
            {
                progressive = marker.FunctionCode == Jpeg.Markers.StartOfProgressiveHuffmanFrame;

                var offset = 0;
                byte bitDepth = marker.Data[offset++];                
                height = Binary.Read16(marker.Data, ref offset, Binary.IsLittleEndian);
                width = Binary.Read16(marker.Data, ref offset, Binary.IsLittleEndian);
                var numberOfComponents = marker.Data[offset++];

                int bitsPerComponent = bitDepth / numberOfComponents;

                MediaComponent[] mediaComponents = new MediaComponent[numberOfComponents];
                int[] widths = new int[numberOfComponents];
                int[] heights = new int[numberOfComponents];

                // Read the components and sampling information
                for (int componentIndex = 0; componentIndex < numberOfComponents; componentIndex++)
                {
                    var componentId = marker.Data[offset++];
                    var samplingFactors = marker.Data[offset++];
                    widths[componentIndex] = samplingFactors & 0x0F;
                    heights[componentIndex] = samplingFactors >> 4;
                    
                    //TODO CMYK image throws this off?
                    var quantizationTableNumber = offset >= marker.DataSize ? (byte)componentIndex : marker.Data[offset++];

                    var mediaComponent = new JpegComponent(quantizationTableNumber, componentId, bitsPerComponent);

                    mediaComponents[componentIndex] = mediaComponent;
                }

                // Create the image format based on the SOF0 data
                imageFormat = new ImageFormat(Binary.ByteOrder.Little, DataLayout.Packed, mediaComponents);
                imageFormat.Widths = widths;
                imageFormat.Heights = heights;
            }
            else if (marker.FunctionCode == Jpeg.Markers.StartOfScan) // SOS marker
            {
                var dataSegmentSize = Image.CalculateSize(imageFormat, width, height);
                dataSegment = new MemorySegment(dataSegmentSize);
                stream.Read(dataSegment.Array, dataSegment.Offset, dataSegment.Count);
                break;
            }
            else if (marker.FunctionCode != Jpeg.Markers.StartOfInformation)
            {
                markers.Add(marker.FunctionCode, marker);
            }
        }

        if (imageFormat == null || dataSegment == null)
            throw new InvalidDataException("The provided stream does not contain valid JPEG image data.");

        // Create and return the JpegImage
        return new JpegImage(imageFormat, width, height, dataSegment, progressive, markers);
    }

    public void Save(Stream stream)
    {
        // Write the JPEG signature
        WriteMarker(stream, Jpeg.Markers.StartOfInformation, WriteEmptyMarker);

        if (Markers != null)
        {
            foreach (var marker in Markers.TryGetValue(Jpeg.Markers.TextComment, out var textComments) ? textComments : Enumerable.Empty<Marker>())
            {
                WriteMarker(stream, marker.FunctionCode, (s) => s.Write(marker.Data.Array, marker.Data.Offset, marker.Data.Count));
            }

            foreach (var marker in Markers.TryGetValue(Jpeg.Markers.QuantizationTable, out var quantizationTables) ? quantizationTables : Enumerable.Empty<Marker>())
            {
                WriteMarker(stream, marker.FunctionCode, (s) => s.Write(marker.Data.Array, marker.Data.Offset, marker.Data.Count));
            }
        }

        // TODO revise to write correct start of scan header per coding.
        // Write the SOF0 marker
        WriteMarker(stream, Progressive ? Jpeg.Markers.StartOfProgressiveHuffmanFrame : Jpeg.Markers.StartOfBaselineFrame, WriteSOF0Marker);

        if (Markers != null)
        {
            foreach (var marker in Markers.TryGetValue(Jpeg.Markers.HuffmanTable, out var huffmanTables) ? huffmanTables : Enumerable.Empty<Marker>())
            {
                WriteMarker(stream, marker.FunctionCode, (s) => s.Write(marker.Data.Array, marker.Data.Offset, marker.Data.Count));
            }
        }

        // Write the SOS marker
        WriteMarker(stream, Jpeg.Markers.StartOfScan, WriteSOSMarker);

        // Write the image data
        stream.Write(Data.Array, Data.Offset, Data.Count);

        // Write the EOI marker
        WriteMarker(stream, Jpeg.Markers.EndOfInformation, WriteEmptyMarker);
    }

    private void WriteSOF0Marker(Stream stream)
    {
        stream.WriteByte((byte)ImageFormat.Size);
        stream.Write(Binary.GetBytes((ushort)Height, Binary.IsLittleEndian));
        stream.Write(Binary.GetBytes((ushort)Width, Binary.IsLittleEndian));
        stream.WriteByte((byte)ImageFormat.Components.Length);
        for (int i = 0; i < ImageFormat.Components.Length; i++)
        {
            var component = ImageFormat.Components[i];
            switch (component.Id)
            {
                case ImageFormat.LumaChannelId:
                case ImageFormat.CyanChannelId:
                    stream.WriteByte(1);
                    break;
                case ImageFormat.ChromaMajorChannelId:
                case ImageFormat.MagentaChannelId:
                    stream.WriteByte(2);
                    break;
                case ImageFormat.ChromaMinorChannelId:
                case ImageFormat.YellowChannelId:
                    stream.WriteByte(3);
                    break;
                case ImageFormat.KChannelId:
                    stream.WriteByte(4);
                    break;
                case ImageFormat.RedChannelId:
                    stream.WriteByte(82); 
                    break;
                case ImageFormat.GreenChannelId:
                    stream.WriteByte(71);
                    break;
                case ImageFormat.BlueChannelId:
                    stream.WriteByte(66);
                    break;
            }            
            stream.WriteByte((byte)(ImageFormat.Widths[i] << 4 | ImageFormat.Heights[i])); // Sampling factors
            stream.WriteByte(component is JpegComponent jpegComponent ? jpegComponent.QuantizationTableNumber : (byte)i); // Quantization table number
        }        
    }

    private void WriteMarker(Stream writer, byte functionCode, Action<Stream> writeMarkerData)
    {        
        using (MemoryStream ms = new MemoryStream())
        {
            writeMarkerData(ms);
            ms.TryGetBuffer(out var markerData);
            using Marker marker = new Marker(functionCode, markerData.Count > 0 ? markerData.Count + Binary.BytesPerShort : 0);
            markerData.CopyTo(marker.Data.Array, marker.Data.Offset);
            writer.Write(marker.Array, marker.Offset, marker.Count);
        }
    }

    private void WriteSOSMarker(Stream stream)
    {
        stream.Write(Binary.GetBytes((ushort)(6 + 2 * ImageFormat.Components.Length), Binary.IsLittleEndian));
        stream.WriteByte((byte)ImageFormat.Components.Length);
        for (int i = 0; i < ImageFormat.Components.Length; i++)
        {
            var component = ImageFormat.Components[i];
            stream.WriteByte(component.Id); // Component ID
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