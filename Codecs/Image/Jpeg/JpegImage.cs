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
        MemorySegment thumbnailData = default;
        bool progressive = false;
        ConcurrentThesaurus<byte, Marker> markers = new ConcurrentThesaurus<byte, Marker>();
        foreach (var marker in JpegCodec.ReadMarkers(stream))
        {
            //Handle the marker to decode.
            switch (marker.FunctionCode)
            {
                case Jpeg.Markers.NumberOfLines:
                    var numberOfLines = new NumberOfLines(marker);
                    height = numberOfLines.Nl;
                    continue;
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
                case Jpeg.Markers.HeirarchicalProgression:                    
                    switch (marker.FunctionCode)
                    {
                        case Jpeg.Markers.StartOfDifferentialProgressiveHuffmanFrame:
                        case Jpeg.Markers.StartOfDifferentialProgressiveArithmeticFrame:
                        case Jpeg.Markers.StartOfProgressiveArithmeticFrame:
                        case Jpeg.Markers.StartOfProgressiveHuffmanFrame:
                        case Jpeg.Markers.HeirarchicalProgression:
                            progressive = true;
                            break;
                    }

                    StartOfFrame tag;

                    if(marker.FunctionCode == Jpeg.Markers.HeirarchicalProgression)
                    {
                        tag = new HeirarchicalProgression(marker);
                    }
                    else
                    {
                        tag = new StartOfFrame(marker);
                    }
                    
                    int bitDepth = Binary.Clamp(Binary.BitsPerByte, Binary.BitsPerInteger, tag.P);
                    height = tag.Y;
                    width = tag.X;

                    //Warning, check for invalid number of components.
                    var numberOfComponents = Binary.Min(Binary.Four, tag.Nf);

                    int bitsPerComponent = bitDepth / numberOfComponents;

                    if (bitsPerComponent == 0)
                        bitsPerComponent = Binary.BitsPerByte;

                    MediaComponent[] mediaComponents = new MediaComponent[numberOfComponents];
                    int[] widths = new int[numberOfComponents];
                    int[] heights = new int[numberOfComponents];

                    int remains = tag.DataLength - StartOfFrame.Length;

                    // Read the components and sampling information
                    for (int componentIndex = 0; componentIndex < numberOfComponents && remains > 0; componentIndex++)
                    {
                        var frameComponent = tag[componentIndex];
                        var componentId = frameComponent.ComponentIdentifier;
                        widths[componentIndex] = frameComponent.HorizontalSamplingFactor;
                        heights[componentIndex] = frameComponent.VerticalSamplingFactor;

                        var quantizationTableNumber = frameComponent.QuantizationTableDestinationSelector;

                        var mediaComponent = new JpegComponent((byte)quantizationTableNumber, (byte)componentId, bitsPerComponent);

                        mediaComponents[componentIndex] = mediaComponent;
                        
                        remains -= frameComponent.Count;
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
                            dataSegment = dataSegment.Slice(0, read);
                        break;
                    }                
                case Jpeg.Markers.AppFirst:
                case Jpeg.Markers.AppLast:
                    var app = new App(marker);

                    if(app.MajorVersion >= 1 && app.MinorVersion >= 2)
                    {
                        var appExtension = new AppExtension(app);
                        thumbnailData = appExtension.ThumbnailData;
                    }
                    else
                    {
                        thumbnailData = app.ThumbnailData;
                        imageFormat = ImageFormat.RGB(8);
                        width = app.XThumbnail;
                        height = app.YThumbnail;
                    }

                    goto default;
                case Jpeg.Markers.StartOfInformation:
                case Jpeg.Markers.EndOfInformation:
                    continue;
                default:
                    markers.Add(marker.FunctionCode, marker);
                    continue;
            }
        }

        if (imageFormat == null || dataSegment == null && thumbnailData == null)
            throw new InvalidDataException("The provided stream does not contain valid JPEG image data.");

        // Create and return the JpegImage
        return new JpegImage(imageFormat, width, height, dataSegment ?? thumbnailData, progressive, markers);
    }

    public void Save(Stream stream)
    {
        var markerBuffer = Markers != null ? new ConcurrentThesaurus<byte, Marker>(Markers) : null;

        // Write the JPEG signature
        WriteEmptyMarker(stream, Jpeg.Markers.StartOfInformation);

        if (markerBuffer != null)
        {
            foreach (var marker in Markers.TryGetValue(Jpeg.Markers.TextComment, out var textComments) ? textComments : Enumerable.Empty<Marker>())
            {
                WriteMarker(stream, marker);
            }

            markerBuffer.Remove(Jpeg.Markers.TextComment);

            foreach (var marker in Markers.TryGetValue(Jpeg.Markers.QuantizationTable, out var quantizationTables) ? quantizationTables : Enumerable.Empty<Marker>())
            {
                WriteMarker(stream, marker);
            }

            markerBuffer.Remove(Jpeg.Markers.QuantizationTable);

            foreach(var marker in markerBuffer.Values.Where(markerBuffer => Jpeg.Markers.IsApplicationMarker(markerBuffer.FunctionCode)))
            {
                WriteMarker(stream, marker);

                markerBuffer.Remove(marker.FunctionCode);
            }
        }

        // TODO revise to write correct start of scan header per coding.
        // Write the SOF marker
        WriteStartOfFrame(Progressive ? Jpeg.Markers.StartOfProgressiveHuffmanFrame : Jpeg.Markers.StartOfBaselineFrame, stream);

        if (markerBuffer != null)
        {
            foreach (var marker in Markers.TryGetValue(Jpeg.Markers.HuffmanTable, out var huffmanTables) ? huffmanTables : Enumerable.Empty<Marker>())
            {
                WriteMarker(stream, marker);
            }

            markerBuffer.Remove(Jpeg.Markers.HuffmanTable);
        }

        if (markerBuffer != null)
        {
            foreach (var marker in markerBuffer.Values)
            {
                WriteMarker(stream, marker);
                markerBuffer.Clear();
            }
        }

        // Write the SOS marker
        WriteStartOfScan(stream);

        // Write the image data
        stream.Write(Data.Array, Data.Offset, Data.Count);

        // Write the EOI marker
        WriteEmptyMarker(stream, Jpeg.Markers.EndOfInformation);
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
        WriteMarker(stream, sof);
    }

    private void WriteStartOfScan(Stream stream)
    {
        var numberOfComponents = ImageFormat.Components.Length;
        using var sos = new StartOfScan(numberOfComponents);
        // Set the Ss, Se, Ah, and Al fields
        // These values are typical for a baseline or progessive JPEG but not lossless. (1-7)
        sos.Ss = 0;  // Start of spectral selection
        sos.Se = 63; // End of spectral selection
        sos.Ah = 0;  // Successive approximation high
        sos.Al = 0;  // Successive approximation low
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
        WriteMarker(stream, sos);
    }

    private void WriteMarker(Stream stream, Marker marker)
    {
        stream.Write(marker.Array, marker.Offset, marker.Count);
    }

    private void WriteEmptyMarker(Stream stream, byte functionCode)
    {
        stream.WriteByte(Jpeg.Markers.Prefix);
        stream.WriteByte(functionCode);
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