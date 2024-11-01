using System;
using System.Collections.Generic;
using Media.Codec.Jpeg.Segments;
using Media.Codecs.Image;
using Media.Common;
using Media.Common.Interfaces;

namespace Media.Codec.Jpeg.Classes;

/// <summary>
/// Contains useful information about the current state of the Jpeg image.
/// </summary>
internal sealed class JpegState : IEquatable<JpegState>
{
    /// <summary>
    /// The function code which corresponds to the StartOfScan marker.
    /// </summary>
    public byte StartOfFrameFunctionCode;

    /// <summary>
    /// The <see cref="Classes.Scan"/> which is currently being processed."/>
    /// </summary>
    public Scan? Scan;

    /// <summary>
    /// Start of spectral or predictor selection
    /// </summary>
    public byte Ss;

    /// <summary>
    /// End of spectral selection
    /// </summary>
    public byte Se;

    /// <summary>
    /// Successive approximation bit position high
    /// </summary>
    public byte Ah;

    /// <summary>
    /// Successive approximation bit position low or point transform
    /// </summary>
    public byte Al;

    /// <summary>
    /// Defines the precision of any contained <see cref="QuantizationTables"/>
    /// </summary>
    public byte Precision;

    /// <summary>
    /// </summary>
    public byte MaximumHorizontalSamplingFactor;

    /// <summary>
    /// </summary>
    public byte MaximumVerticalSamplingFactor;

    public int McusPerLine;

    public int McusPerColumn;

    /// <summary>
    /// Any <see cref="Segments.QuantizationTables"/> which are contained in the image.
    /// </summary>
    public readonly List<QuantizationTables> QuantizationTables = new();

    /// <summary>
    /// Get a <see cref="QuantizationTable"/> by its table id.
    /// </summary>
    /// <param name="tableId"></param>
    /// <returns></returns>
    public QuantizationTable? GetQuantizationTable(int tableId)
    {
        foreach (var quantizationTable in QuantizationTables)
        {
            foreach (var table in quantizationTable.Tables)
            {
                if (table.Tq == tableId)
                    return table;
            }
        }

        return null;
    }

    /// <summary>
    /// Any <see cref="Segments.HuffmanTables"/> which are contained in the image.
    /// </summary>
    public readonly List<HuffmanTables> HuffmanTables = new();

    /// <summary>
    /// Get a <see cref="HuffmanTable"/> by its table class and table id.
    /// </summary>
    /// <param name="te">Table class</param>
    /// <param name="th">Table id</param>
    /// <returns></returns>
    public HuffmanTable? GetHuffmanTable(int te, int th)
    {
        foreach(var huffmanTable in HuffmanTables)
        {
            foreach (var table in huffmanTable.Tables)
            {
                if (table.Te == te && table.Th == th)
                    return table;
            }
        }
        
        return null;
    }

    /// <summary>
    /// Any <see cref="Segments.ArithmeticConditioningTables"/> which are contained in the image.
    /// </summary>
    public readonly List<ArithmeticConditioningTables> ArithmeticConditioningTables = new();

    /// <summary>
    /// Gets a <see cref="ArithmeticConditioningTable"/> by its table class and table id."/>
    /// </summary>
    /// <param name="tableClass"></param>
    /// <param name="tableId"></param>
    /// <returns></returns>
    public ArithmeticConditioningTable? GetArithmeticConditioningTable(int tableClass, int tableId)
    {
        foreach (var arithmeticConditioningTable in ArithmeticConditioningTables)
        {
            foreach (var table in arithmeticConditioningTable.Tables)
            {
                if (table.Tc == tableClass && table.Tb == tableId)
                    return table;
            }
        }

        return null;
    }

    /// <summary>
    /// Stores any Thumbnail data.
    /// </summary>
    public MemorySegment? ThumbnailData;
    
    /// <summary>
    /// Data from the <see cref="Scan"/> (Compressed or Decompressed)
    /// </summary>
    public MemorySegment? ScanData;

    /// <summary>
    /// Constructs a <see cref="JpegState"/>
    /// </summary>
    /// <param name="startOfScanFunctionCode"></param>
    /// <param name="ss"></param>
    /// <param name="se"></param>
    /// <param name="ah"></param>
    /// <param name="al"></param>
    public JpegState(byte startOfScanFunctionCode, byte ss, byte se, byte ah, byte al)
    {
        StartOfFrameFunctionCode = startOfScanFunctionCode;
        Ss = ss;
        Se = se;
        Ah = ah;
        Al = al;
    }

    public void InitializeDefaultHuffmanTables()
    {
        var huffmanTables = new HuffmanTable[4];
        huffmanTables[0] = new HuffmanTable(JpegCodec.DcLuminanceBits, JpegCodec.DcLuminanceValues);
        huffmanTables[0].Te = 0;
        huffmanTables[0].Th = 0;

        huffmanTables[1] = new HuffmanTable(JpegCodec.AcLuminanceBits, JpegCodec.AcLuminanceValues);
        huffmanTables[1].Te = 1;
        huffmanTables[1].Th = 0;

        huffmanTables[2] = new HuffmanTable(JpegCodec.DcChrominanceBits, JpegCodec.DcChrominanceValues);
        huffmanTables[2].Te = 0;
        huffmanTables[2].Th = 1;

        huffmanTables[3] = new HuffmanTable(JpegCodec.AcChrominanceBits, JpegCodec.AcChrominanceValues);
        huffmanTables[3].Te = 1;
        huffmanTables[3].Th = 1;

        var dht = new HuffmanTables(huffmanTables[0].Count + huffmanTables[1].Count + huffmanTables[2].Count + huffmanTables[3].Count);

        dht.Tables = huffmanTables;

        HuffmanTables.Add(dht);
    }

    public void InitializeDefaultQuantizationTables(int precision, int quality)
    {
        var quantizationTables = new QuantizationTable[2];

        quantizationTables[0] = QuantizationTable.CreateQuantizationTable(precision, 0, quality, QuantizationTableType.Luminance);

        quantizationTables[1] = QuantizationTable.CreateQuantizationTable(precision, 1, quality, QuantizationTableType.Chrominance);

        using var dqt = new QuantizationTables(quantizationTables[0].Count + quantizationTables[1].Count);

        dqt.Tables = quantizationTables;

        QuantizationTables.Add(dqt);
    }

    public void InitializeScan(JpegImage jpegImage)
    {
        switch (StartOfFrameFunctionCode)
        {
            case Markers.StartOfBaselineFrame:
            case Markers.StartOfHuffmanFrame:
            case Markers.StartOfProgressiveHuffmanFrame:
                Scan = new HuffmanScan();
                break;
            case Markers.StartOfExtendedSequentialArithmeticFrame:
            case Markers.StartOfProgressiveArithmeticFrame:
            case Markers.StartOfLosslessArithmeticFrame:
            case Markers.ArithmeticConditioning:
            case Markers.StartOfDifferentialSequentialArithmeticFrame:
            case Markers.StartOfDifferentialProgressiveArithmeticFrame:
                Scan = new ArithmeticScan();
                break;
            default:
                throw new NotImplementedException("Create an issue for your use case.");
        }        

        //Create Jpeg.Components from the MediaComponents
        for(var i = 0; i < jpegImage.ImageFormat.Components.Length; ++i)
        {
            var mediaComponent = jpegImage.ImageFormat.Components[i];

            if(mediaComponent is not Component jpegComponent)
            {
                jpegComponent = new Component((byte)i, (byte)i, mediaComponent.Size)
                {
                    HorizontalSamplingFactor = (byte)(jpegImage.ImageFormat.VerticalSamplingFactors[i] + 1),
                    VerticalSamplingFactor = (byte)(jpegImage.ImageFormat.HorizontalSamplingFactors[i] + 1)
                };
                jpegImage.ImageFormat.Components[i] = jpegComponent;
            }
        }

        //Create the memory which will store the scan data
        ScanData = new MemorySegment(jpegImage.Data.Count);
    }

    public override bool Equals(object? obj)
     => ReferenceEquals(this, obj) || obj is JpegState jpegState && Equals(jpegState);

    public bool Equals(JpegState? other)
        => other is not null &&
           StartOfFrameFunctionCode == other.StartOfFrameFunctionCode &&
           Ss == other.Ss &&
           Se == other.Se &&
           Ah == other.Ah &&
           Al == other.Al;

    public override int GetHashCode()
        => HashCode.Combine(StartOfFrameFunctionCode, Ss, Se, Ah, Al);

    public static bool operator ==(JpegState a, JpegState b)
        => a.Equals(b);

    public static bool operator !=(JpegState a, JpegState b)
        => false == a.Equals(b);
}
