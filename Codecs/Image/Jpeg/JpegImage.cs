using System.IO;
using System;
using System.Numerics;
using Media.Codecs.Image;
using Media.Common;
using Media.Common.Collections.Generic;
using Media.Codec.Jpeg.Classes;
using Media.Codec.Jpeg.Segments;
using System.Runtime.InteropServices;

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
        ImageFormat imageFormat = default;
        MemorySegment dataSegment = default;
        MemorySegment thumbnailData = default;
        JpegState jpegState = new(Jpeg.Markers.Prefix, 0, 63, 0, 0);
        ConcurrentThesaurus<byte, Marker> markers = new ConcurrentThesaurus<byte, Marker>();        
        foreach (var marker in JpegCodec.ReadMarkers(stream))
        {
            //Handle the marker to decode.
            switch (marker.FunctionCode)
            {
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

                    var mediaComponents = new JpegComponent[numberOfComponents];
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

                        var mediaComponent = new JpegComponent((byte)quantizationTableNumber, (byte)componentId, bitsPerComponent);

                        mediaComponents[componentIndex] = mediaComponent;

                        remains -= frameComponent.Count;
                    }

                    // Create the image format based on the SOF0 data
                    imageFormat = new ImageFormat(Binary.ByteOrder.Big, DataLayout.Planar, mediaComponents);
                    imageFormat.HorizontalSamplingFactors = widths;
                    imageFormat.VerticalSamplingFactors = heights;
                    tag.Dispose();
                    tag = null;
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
                        for (int ns = Binary.Min(4, sos.Ns), i = 0; i < ns; ++i)
                        {
                            using var scanComponentSelector = sos[i];
                            var jpegComponent = imageFormat.GetComponentById(scanComponentSelector.Csj) as JpegComponent ?? imageFormat.Components[i] as JpegComponent;
                            jpegComponent.Tdj = scanComponentSelector.Tdj;
                            jpegComponent.Taj = scanComponentSelector.Taj;
                        }

                        var dataSegmentSize = CalculateSize(imageFormat, width, height);

                        dataSegment = new MemorySegment(Math.Abs(dataSegmentSize));
                        
                        var read = stream.Read(dataSegment.Array, dataSegment.Offset, dataSegment.Count);
                        
                        if (read < dataSegment.Count)
                            dataSegment = dataSegment.Slice(0, read);                        

                        break;
                    }
                case Jpeg.Markers.AppFirst:
                case Jpeg.Markers.AppLast:
                    {
                        using var app = new App(marker);

                        if (app.MajorVersion >= 1 && app.MinorVersion >= 2)
                        {
                            using var appExtension = new AppExtension(app);
                            thumbnailData = appExtension.ThumbnailData;
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
                                        using var ms = thumbnailData.ToMemoryStream();
                                        using var jpegImage = JpegImage.FromStream(ms);
                                        imageFormat = jpegImage.ImageFormat;
                                        thumbnailData = jpegImage.Data;
                                        break;
                                    }
                            }
                        }
                        else
                        {
                            thumbnailData = app.ThumbnailData;
                            imageFormat = ImageFormat.RGB(8);
                            width = app.XThumbnail;
                            height = app.YThumbnail;
                        }
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

        // If the required tags were not present throw an exception.
        if (imageFormat == null || dataSegment == null && thumbnailData == null)
            throw new InvalidDataException("The provided stream does not contain valid JPEG image data.");

        // Create the JpegImage which still contains compressed image data.
        var result = new JpegImage(imageFormat, width, height, dataSegment ?? thumbnailData, jpegState, markers);
        
        //Decompress the JpegImage using the JpegCodec.
        JpegCodec.Decompress(result);

        //Return the result.
        return result;
    }

    public void Save(Stream stream, int quality = 99)
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
        }

        foreach (var marker in JpegState.QuantizationTables)
        {
            JpegCodec.WriteMarker(stream, marker);
        }

        JpegCodec.WriteStartOfFrame(this, stream);

        foreach (var marker in JpegState.HuffmanTables)
        {
            if (marker == null) continue;

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
            JpegCodec.Compress(this, stream);

            JpegCodec.WriteInformationMarker(Jpeg.Markers.EndOfInformation, stream);
        }
    }

    private void ProcessComponent(Span<byte> span, Span<short> quantizationTable, Span<double> coefficients, Span<short> quantizedCoefficients, BitWriter writer)
    {
        // Step 4.1: Perform Forward Discrete Cosine Transform (FDCT)
        ref Span<double> dctCoefficients = ref coefficients;

        if (Vector.IsHardwareAccelerated)
            JpegCodec.VFDCT(MemoryMarshal.Cast<byte, double>(span), dctCoefficients);
        else
            JpegCodec.FDCT(MemoryMarshal.Cast<byte, double>(span), dctCoefficients);

        // Step 4.2: Quantize the DCT coefficients
        JpegCodec.Quantize(dctCoefficients, quantizationTable, quantizedCoefficients);

        // Step 4.3: Huffman encode the quantized coefficients
        JpegCodec.HuffmanEncode(quantizedCoefficients, writer, JpegState.HuffmanTables);
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