using System.IO;
using System;
using System.Numerics;
using Media.Codecs.Image;
using Media.Common;
using System.Linq;
using Media.Common.Collections.Generic;
using Codec.Jpeg;
using Codec.Jpeg.Markers;
using Codec.Jpeg.Classes;

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
        int width = 0, height = 0;
        ImageFormat imageFormat = default;
        MemorySegment dataSegment = default;
        bool progressive = false;
        Common.Collections.Generic.ConcurrentThesaurus<byte, Marker> markers = new Common.Collections.Generic.ConcurrentThesaurus<byte, Marker>();
        foreach (var marker in JpegCodec.ReadMarkers(stream))
        {
            if (marker.IsEmpty) continue;

            //Handle the marker to decode.
            switch (marker.FunctionCode)
            {                
                case Jpeg.Markers.StartOfBaselineFrame:
                case Jpeg.Markers.StartOfHuffmanFrame:
                case Jpeg.Markers.StartOfDifferentialSequentialArithmeticFrame:
                case Jpeg.Markers.StartOfLosslessHuffmanFrame:
                case Jpeg.Markers.StartOfLosslessArithmeticFrame:
                case Jpeg.Markers.StartOfDifferentialLosslessArithmeticFrame:
                case Jpeg.Markers.StartOfDifferentialLosslessHuffmanFrame:
                case Jpeg.Markers.StartOfDifferentialProgressiveHuffmanFrame:
                case Jpeg.Markers.StartOfDifferentialProgressiveArithmeticFrame:
                case Jpeg.Markers.StartOfProgressiveArithmeticFrame:
                case Jpeg.Markers.StartOfProgressiveHuffmanFrame:
                    switch (marker.FunctionCode)
                    {
                        case Jpeg.Markers.StartOfDifferentialProgressiveHuffmanFrame:
                        case Jpeg.Markers.StartOfDifferentialProgressiveArithmeticFrame:
                        case Jpeg.Markers.StartOfProgressiveArithmeticFrame:
                        case Jpeg.Markers.StartOfProgressiveHuffmanFrame:
                            progressive = true;
                            break;
                    }
                    var sof = new StartOfFrame(marker);
                    int bitDepth = Binary.Max(Binary.BitsPerByte, sof.P);
                    height = sof.Y;
                    width = sof.X;

                    //Warning, check for invalid number of components.
                    var numberOfComponents = Binary.Min(Binary.Four, sof.Nf);

                    int bitsPerComponent = bitDepth / numberOfComponents;

                    MediaComponent[] mediaComponents = new MediaComponent[numberOfComponents];
                    int[] widths = new int[numberOfComponents];
                    int[] heights = new int[numberOfComponents];

                    // Read the components and sampling information
                    for (int componentIndex = 0; componentIndex < numberOfComponents; componentIndex++)
                    {
                        var frameComponent = sof[componentIndex];
                        var componentId = frameComponent.ComponentIdentifier;
                        widths[componentIndex] = frameComponent.HorizontalSamplingFactor;
                        heights[componentIndex] = frameComponent.VerticalSamplingFactor;

                        var quantizationTableNumber = frameComponent.QuantizationTableDestinationSelector;

                        var mediaComponent = new JpegComponent((byte)quantizationTableNumber, (byte)componentId, bitsPerComponent);

                        mediaComponents[componentIndex] = mediaComponent;
                    }

                    // Create the image format based on the SOF0 data
                    imageFormat = new ImageFormat(Binary.ByteOrder.Little, DataLayout.Planar, mediaComponents);
                    imageFormat.HorizontalSamplingFactors = widths;
                    imageFormat.VerticalSamplingFactors = heights;
                    continue;
                case Jpeg.Markers.StartOfScan:
                    {
                        var dataSegmentSize = CalculateSize(imageFormat, width, height);
                        dataSegment = new MemorySegment(Math.Abs(dataSegmentSize));
                        var read = stream.Read(dataSegment.Array, dataSegment.Offset, dataSegment.Count);
                        if (read < dataSegment.Count)
                            dataSegment = dataSegment.Slice(read);
                        break;
                    }
                case Jpeg.Markers.HierarchialProgression:
                    {
                        var exp = new HierarchialProgression(marker);
                        var dataSegmentSize = CalculateSize(imageFormat, width, height);
                        dataSegment = new MemorySegment(Math.Abs(dataSegmentSize));
                        var read = stream.Read(dataSegment.Array, dataSegment.Offset, dataSegment.Count);
                        if (read < dataSegment.Count)
                            dataSegment = dataSegment.Slice(read);
                        continue;
                    }
                case Jpeg.Markers.AppFirst:
                case Jpeg.Markers.AppLast:
                    var app = new App(marker);

                    if(app.MajorVersion >= 1 && app.MinorVersion >= 2)
                    {
                        var appExtension = new AppExtension(app);
                        var thumbnailData = appExtension.ThumbnailData;
                        dataSegment = thumbnailData;
                    }
                    else
                    {
                        var thumbnailData = app.ThumbnailData;
                        dataSegment = thumbnailData;
                        imageFormat = ImageFormat.RGB(8);
                        width = app.XThumbnail;
                        height = app.YThumbnail;
                    }

                    continue;
                default:
                    markers.Add(marker.FunctionCode, marker);
                    continue;
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
        // Write the SOF marker
        WriteStartOfFrame(Progressive ? Jpeg.Markers.StartOfProgressiveHuffmanFrame : Jpeg.Markers.StartOfBaselineFrame, stream);

        if (Markers != null)
        {
            foreach (var marker in Markers.TryGetValue(Jpeg.Markers.HuffmanTable, out var huffmanTables) ? huffmanTables : Enumerable.Empty<Marker>())
            {
                WriteMarker(stream, marker.FunctionCode, (s) => s.Write(marker.Data.Array, marker.Data.Offset, marker.Data.Count));
            }
        }

        // Write the SOS marker
        WriteStartOfScan(stream);

        // Write the image data
        stream.Write(Data.Array, Data.Offset, Data.Count);

        // Write the EOI marker
        WriteMarker(stream, Jpeg.Markers.EndOfInformation, WriteEmptyMarker);
    }

    private void WriteStartOfFrame(byte functionCode, Stream stream)
    {
        var componentCount = ImageFormat.Components.Length;
        using StartOfFrame sof = new StartOfFrame(functionCode, componentCount);
        sof.P = ImageFormat.Size;
        sof.Y = Height;
        sof.X = Width;
        for (var i = 0; i < componentCount; ++i)
        {
            var imageComponent = ImageFormat.Components[i];

            if (imageComponent is JpegComponent jpegComponent)
            {
                var frameComponent = new FrameComponent(jpegComponent.Id, ImageFormat.HorizontalSamplingFactors[i], ImageFormat.VerticalSamplingFactors[i], jpegComponent.QuantizationTableNumber);
                sof[i] = frameComponent;
            }
            else
            {
                var frameComponent = new FrameComponent(imageComponent.Id, ImageFormat.HorizontalSamplingFactors[i], ImageFormat.VerticalSamplingFactors[i], i);
                sof[i] = frameComponent;
            }
        }
        stream.Write(sof.Array, sof.Offset, sof.Count);
    }

    private void WriteStartOfScan(Stream stream)
    {
        var numberOfComponents = ImageFormat.Components.Length;
        var sos = new StartOfScan(numberOfComponents);
        sos.Ss = 0;
        sos.Se = 0;
        sos.Ah = 0;
        sos.Al = 0;
        for (var i = 0; i < numberOfComponents; ++i)
        {
            var imageComponent = ImageFormat.Components[i];

            if (imageComponent is JpegComponent jpegComponent)
            {
                var componentSelector = new ScanComponentSelectorType();
                componentSelector.Csj = jpegComponent.Id;
                componentSelector.Tdj = jpegComponent.Id;
                componentSelector.Taj = jpegComponent.Id;
                sos[i] = componentSelector;
            }
            else
            {
                var componentSelector = new ScanComponentSelectorType();
                componentSelector.Csj = (byte)i;
                componentSelector.Tdj = (byte)i;
                componentSelector.Taj = (byte)i;
                sos[i] = componentSelector;
            }
        }
        stream.Write(sos.Array, sos.Offset, sos.Count);
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