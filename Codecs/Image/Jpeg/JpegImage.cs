using System.IO;
using System;
using System.Numerics;
using Media.Codecs.Image;
using Media.Common;
using Media.Common.Collections.Generic;
using Media.Codec.Jpeg.Classes;
using Media.Codec.Jpeg.Segments;

namespace Media.Codec.Jpeg;

public class JpegImage : Image
{
    internal readonly JpegState JpegState;    
    internal readonly ConcurrentThesaurus<byte, Marker>? Markers;

    public JpegImage(ImageFormat imageFormat, int width, int height)
        : base(imageFormat, width, height, new JpegCodec())
    {
        JpegState = new JpegState(Jpeg.Markers.StartOfBaselineFrame, 0, 63, 0, 0);
    }

    private JpegImage(ImageFormat imageFormat, int width, int height, MemorySegment data, JpegState jpegState, ConcurrentThesaurus<byte, Marker> markers)
        : base(imageFormat, width, height, data, new JpegCodec())
    {
        JpegState = jpegState;
        Markers = markers;
    }

    public static JpegImage FromStream(Stream stream)
    {
        int width = 0, height = 0;
        ImageFormat? imageFormat = default;
        MemorySegment? dataSegment = default;
        JpegState jpegState = new(Jpeg.Markers.Prefix, 0, 63, 0, 0);
        ConcurrentThesaurus<byte, Marker> markers = new ConcurrentThesaurus<byte, Marker>();        
        foreach (var marker in JpegCodec.ReadMarkers(stream))
        {
            //Handle the marker to decode.
            switch (marker.FunctionCode)
            {
                case Jpeg.Markers.ArithmeticConditioning:
                    var dac = new ArithmeticConditioningTables(marker);
                    jpegState.ArithmeticConditioningTables.Add(dac);
                    continue;
                case Jpeg.Markers.QuantizationTable:
                    var dqt = new QuantizationTables(marker);
                    jpegState.QuantizationTables.Add(dqt);
                    continue;
                case Jpeg.Markers.HuffmanTable:
                    var dht = new HuffmanTables(marker);
                    jpegState.HuffmanTables.Add(dht);
                    continue;
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

                    jpegState.StartOfFrameFunctionCode = marker.FunctionCode;

                    StartOfFrame tag;

                    if (marker.FunctionCode == Jpeg.Markers.HeirarchicalProgression)
                    {
                        tag = new HeirarchicalProgression(marker);
                    }
                    else
                    {
                        tag = new StartOfFrame(marker);                        
                    }                    

                    int bitDepth = Binary.Clamp(tag.P, Binary.BitsPerByte, Binary.BitsPerInteger);
                    height = tag.Y;
                    width = tag.X;

                    //Warning, check for invalid number of components.
                    var numberOfComponents = Binary.Min(Binary.Four, tag.Nf);

                    var bitsPerComponent = bitDepth / numberOfComponents;

                    if (bitsPerComponent == 0)
                        bitsPerComponent = Binary.BitsPerByte;

                    var mediaComponents = new Component[numberOfComponents];
                    var widths = new int[numberOfComponents];
                    var heights = new int[numberOfComponents];

                    var remains = tag.DataLength - StartOfFrame.Length;

                    // Read the components and sampling information
                    for (int componentIndex = 0; componentIndex < numberOfComponents && remains > 0; componentIndex++)
                    {
                        var frameComponent = tag[componentIndex];
                        var componentId = frameComponent.ComponentIdentifier;
                        widths[componentIndex] = frameComponent.HorizontalSamplingFactor;
                        heights[componentIndex] = frameComponent.VerticalSamplingFactor;

                        var quantizationTableNumber = frameComponent.QuantizationTableDestinationSelector;

                        var mediaComponent = new Component((byte)quantizationTableNumber, (byte)componentId, bitsPerComponent);

                        mediaComponents[componentIndex] = mediaComponent;

                        remains -= frameComponent.Count;
                    }

                    // Create the image format based on the SOF0 data
                    imageFormat = new ImageFormat(Binary.ByteOrder.Big, DataLayout.Planar, mediaComponents);
                    imageFormat.HorizontalSamplingFactors = widths;
                    imageFormat.VerticalSamplingFactors = heights;
                    tag.Dispose();
                    continue;
                case Jpeg.Markers.StartOfScan:
                    {
                        using var sos = new StartOfScan(marker);                        
                        
                        jpegState.Ss = (byte)sos.Ss;
                        jpegState.Se = (byte)sos.Se;
                        jpegState.Ah = (byte)sos.Ah;
                        jpegState.Al = (byte)sos.Al;

                        if (jpegState.StartOfFrameFunctionCode == Jpeg.Markers.StartOfProgressiveHuffmanFrame)
                            jpegState.Al = 1;

                        if (imageFormat == null)
                            imageFormat = JpegCodec.DefaultImageFormat;

                        ///If Ns > 1, the following restriction shall be placed on the image components contained in the scan:
                        ///(The summation of all components products of thier respective values correspond to 10.
                        ///1, 2, 3, 4 => 1 + 2 + 3 + 4 = 10
                        for (int ns = Binary.Min(4, sos.Ns), i = 0; i < ns; ++i)
                        {
                            using var scanComponentSelector = sos[i];

                            var jpegComponent = imageFormat.GetComponentById(scanComponentSelector.Csj) as Component ?? imageFormat.Components[i] as Component;

                            if (jpegComponent == null)
                                continue;

                            jpegComponent.Tdj = scanComponentSelector.Tdj;
                            jpegComponent.Taj = scanComponentSelector.Taj;
                        }

                        //Calculate the size of the raw image data. (We will decompress in place)
                        var dataSegmentSize = imageFormat.Length * width * height * JpegCodec.BlockSize;

                        //Create a new segment to hold the compressed data
                        dataSegment = new MemorySegment(Binary.Abs(dataSegmentSize));

                        //Read the compressed data into the data segment
                        var read = stream.Read(dataSegment.Array, dataSegment.Offset, dataSegment.Count);
                        
                        break;
                    }
                case Jpeg.Markers.AppFirst:
                case Jpeg.Markers.AppLast:
                    {
                        using var app = new App(marker);

                        if (app.MajorVersion >= 1 && app.MinorVersion >= 2)
                        {
                            using var appExtension = new AppExtension(app);
                            jpegState.ThumbnailData = appExtension.ThumbnailData;
                            switch (appExtension.ThumbnailFormatType)
                            {
                                default:
                                case ThumbnailFormatType.RGB:
                                    imageFormat = ImageFormat.RGB(8);
                                    break;
                                case ThumbnailFormatType.YCbCr:
                                    imageFormat = ImageFormat.YUV(8);
                                    break;
                                case ThumbnailFormatType.Jpeg:
                                    {
                                        using var ms = jpegState.ThumbnailData.ToMemoryStream();
                                        using var jpegImage = FromStream(ms);
                                        jpegImage.JpegState.InitializeScan(jpegImage);
                                        jpegImage.JpegState.Scan!.Decompress(jpegImage);
                                        imageFormat = jpegImage.ImageFormat;
                                        jpegState.ThumbnailData = jpegImage.Data;
                                        break;
                                    }
                            }
                        }
                        else
                        {
                            jpegState.ThumbnailData = app.ThumbnailData;
                            imageFormat = ImageFormat.RGB(8);
                            width = app.XThumbnail;
                            height = app.YThumbnail;
                        }
                    }
                    goto default;
                case Jpeg.Markers.StartOfInformation:
                    continue;
                case Jpeg.Markers.EndOfInformation:
                    break;
                default:
                    markers.Add(marker.FunctionCode, marker);
                    continue;
            }
        }       

        // If the required tags were not present throw an exception.
        if (imageFormat == null || dataSegment == null)
            throw new InvalidDataException("The provided stream does not contain valid JPEG image data.");

        // Create the JpegImage which still contains compressed image data.
        return new JpegImage(imageFormat, width, height, dataSegment ?? MemorySegment.Empty, jpegState, markers);
    }

    public void Save(Stream stream, int quality = 100)
    {
        // Write the JPEG signature
        JpegCodec.WriteInformationMarker(Jpeg.Markers.StartOfInformation, stream);

        if (Markers != null)
        {
            foreach (var marker in Markers.Values)
            {
                JpegCodec.WriteMarker(stream, marker);
            }
        }
        else
        {
            JpegState.InitializeDefaultHuffmanTables();

            JpegState.InitializeDefaultQuantizationTables(JpegState.Precision, quality);

            JpegState.InitializeScan(this);
        }

        foreach (var marker in JpegState.QuantizationTables)
        {
            JpegCodec.WriteMarker(stream, marker);
        }

        JpegCodec.WriteStartOfFrame(this, stream);

        foreach (var marker in JpegState.HuffmanTables)
        {
            JpegCodec.WriteMarker(stream, marker);
        }

        foreach(var marker in JpegState.ArithmeticConditioningTables)
        {
            JpegCodec.WriteMarker(stream, marker);
        }

        JpegCodec.WriteStartOfScan(this, stream);

        if (Markers != null)
        {
            // Write the compressed image data to the stream
            stream.Write(Data.Array, Data.Offset, Data.Count);

            if (Data[Data.Count - 1] != Jpeg.Markers.EndOfInformation)
            {
                // Write the EOI marker
                JpegCodec.WriteInformationMarker(Jpeg.Markers.EndOfInformation, stream);
            }
        }
        else
        {
            // Compress this image data to the stream
            JpegState.Scan!.Compress(this, stream);

            JpegCodec.WriteInformationMarker(Jpeg.Markers.EndOfInformation, stream);
        }
    }    

    internal void Decompress()
    {
        if(JpegState.ScanData != null)
            return; // Already decompressed
        JpegState.InitializeScan(this);
        JpegState.Scan!.Decompress(this);
    }

    public MemorySegment GetPixelDataAt(int x, int y)
    {
        Decompress();

        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return MemorySegment.Empty;

        // JPEG format stores pixels from top to bottom
        int bytesPerPixel = ImageFormat.Length;
        int rowSize = Width * bytesPerPixel;
        int offset = (y * rowSize) + (x * bytesPerPixel);

        return JpegState.ScanData.Slice(offset, ImageFormat.Length);
    }
    
    public Vector<byte> GetVectorDataAt(int x, int y)
    {
        Decompress();

        if(JpegState.ScanData is null)
            return Vector<byte>.Zero;

        // JPEG format stores pixels from top to bottom
        int bytesPerPixel = ImageFormat.Length;
        int rowSize = Width * bytesPerPixel;
        int offset = (y * rowSize) + (x * bytesPerPixel);
        offset -= offset % Vector<byte>.Count; // Align the offset to vector size
        return new Vector<byte>(JpegState.ScanData.Array, JpegState.ScanData.Offset + offset);
    }

    public void SetVectorDataAt(int x, int y, Vector<byte> vectorData)
    {
        Decompress();

        if (JpegState.ScanData is null)
            return;

        // JPEG format stores pixels from top to bottom
        int bytesPerPixel = ImageFormat.Length;
        int rowSize = Width * bytesPerPixel;
        int offset = (y * rowSize) + (x * bytesPerPixel);
        offset -= offset % Vector<byte>.Count; // Align the offset to vector size
        vectorData.CopyTo(new Span<byte>(JpegState.ScanData.Array, JpegState.ScanData.Offset + offset, Vector<byte>.Count));
    }
}