using System;
using System.Collections.Generic;
using Media.Common;
using Media.Codec.Jpeg;
using Media.Codec.Jpeg.Segments;
using System.IO;

namespace Codec.Jpeg.Classes;

/// <summary>
/// Contains useful information about the current state of the Jpeg image.
/// </summary>
internal sealed class JpegState : IEquatable<JpegState>
{
    private static ReadOnlySpan<byte> bits_dc_luminance =>
           [ /* 0-base */ 0, 0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0];

    private static ReadOnlySpan<byte> val_dc_luminance => [ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 ];

    private static ReadOnlySpan<byte> bits_dc_chrominance =>
        [ /* 0-base */ 0, 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0 ];

    private static ReadOnlySpan<byte> val_dc_chrominance => [ 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 ];

    private static ReadOnlySpan<byte> bits_ac_luminance =>
        [ /* 0-base */ 0, 0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 0x7d];

    private static ReadOnlySpan<byte> val_ac_luminance =>
        [ 0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06,
              0x13, 0x51, 0x61, 0x07, 0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xa1, 0x08,
              0x23, 0x42, 0xb1, 0xc1, 0x15, 0x52, 0xd1, 0xf0, 0x24, 0x33, 0x62, 0x72,
              0x82, 0x09, 0x0a, 0x16, 0x17, 0x18, 0x19, 0x1a, 0x25, 0x26, 0x27, 0x28,
              0x29, 0x2a, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3a, 0x43, 0x44, 0x45,
              0x46, 0x47, 0x48, 0x49, 0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59,
              0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6a, 0x73, 0x74, 0x75,
              0x76, 0x77, 0x78, 0x79, 0x7a, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
              0x8a, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9a, 0xa2, 0xa3,
              0xa4, 0xa5, 0xa6, 0xa7, 0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4, 0xb5, 0xb6,
              0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3, 0xc4, 0xc5, 0xc6, 0xc7, 0xc8, 0xc9,
              0xca, 0xd2, 0xd3, 0xd4, 0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda, 0xe1, 0xe2,
              0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea, 0xf1, 0xf2, 0xf3, 0xf4,
              0xf5, 0xf6, 0xf7, 0xf8, 0xf9, 0xfa ];

    private static ReadOnlySpan<byte> bits_ac_chrominance =>
        [ /* 0-base */ 0, 0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 0x77 ];

    private static ReadOnlySpan<byte> val_ac_chrominance =>
        [ 0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21, 0x31, 0x06, 0x12, 0x41,
              0x51, 0x07, 0x61, 0x71, 0x13, 0x22, 0x32, 0x81, 0x08, 0x14, 0x42, 0x91,
              0xa1, 0xb1, 0xc1, 0x09, 0x23, 0x33, 0x52, 0xf0, 0x15, 0x62, 0x72, 0xd1,
              0x0a, 0x16, 0x24, 0x34, 0xe1, 0x25, 0xf1, 0x17, 0x18, 0x19, 0x1a, 0x26,
              0x27, 0x28, 0x29, 0x2a, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3a, 0x43, 0x44,
              0x45, 0x46, 0x47, 0x48, 0x49, 0x4a, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58,
              0x59, 0x5a, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6a, 0x73, 0x74,
              0x75, 0x76, 0x77, 0x78, 0x79, 0x7a, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87,
              0x88, 0x89, 0x8a, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9a,
              0xa2, 0xa3, 0xa4, 0xa5, 0xa6, 0xa7, 0xa8, 0xa9, 0xaa, 0xb2, 0xb3, 0xb4,
              0xb5, 0xb6, 0xb7, 0xb8, 0xb9, 0xba, 0xc2, 0xc3, 0xc4, 0xc5, 0xc6, 0xc7,
              0xc8, 0xc9, 0xca, 0xd2, 0xd3, 0xd4, 0xd5, 0xd6, 0xd7, 0xd8, 0xd9, 0xda,
              0xe2, 0xe3, 0xe4, 0xe5, 0xe6, 0xe7, 0xe8, 0xe9, 0xea, 0xf2, 0xf3, 0xf4,
              0xf5, 0xf6, 0xf7, 0xf8, 0xf9, 0xfa ];

    /// <summary>
    /// The function code which corresponds to the StartOfScan marker.
    /// </summary>
    public byte StartOfFrameFunctionCode;

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
    /// Any <see cref="Media.Codec.Jpeg.Segments.QuantizationTables"/> which are contained in the image.
    /// </summary>
    public readonly List<QuantizationTables> QuantizationTables = new();

    /// <summary>
    /// Any <see cref="Media.Codec.Jpeg.Segments.HuffmanTables"/> which are contained in the image.
    /// </summary>
    public readonly List<HuffmanTables> HuffmanTables = new();

    /// <summary>
    /// Constructors a <see cref="JpegState"/>
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

    internal void InitializeDefaultHuffmanTables()
    {
        var huffmanTables = new HuffmanTable[4];
        huffmanTables[0] = HuffmanTable.Create(bits_dc_luminance, val_dc_luminance);
        huffmanTables[0].Te = 0;
        huffmanTables[0].Th = 0;

        huffmanTables[1] = HuffmanTable.Create(bits_ac_luminance, val_ac_luminance);
        huffmanTables[1].Te = 1;
        huffmanTables[1].Th = 0;

        huffmanTables[2] = HuffmanTable.Create(bits_dc_chrominance, val_dc_chrominance);
        huffmanTables[2].Te = 0;
        huffmanTables[2].Th = 1;

        huffmanTables[3] = HuffmanTable.Create(bits_ac_chrominance, val_ac_chrominance);
        huffmanTables[3].Te = 1;
        huffmanTables[3].Th = 1;

        var dht = new HuffmanTables(huffmanTables[0].Count + huffmanTables[1].Count + huffmanTables[2].Count + huffmanTables[3].Count);

        dht.Tables = huffmanTables;

        HuffmanTables.Add(dht);
    }

    public void InitializeDefaultQuantizationTables(int precision, int quality)
    {
        var quantizationTables = new QuantizationTable[2];

        quantizationTables[0] = JpegCodec.GetQuantizationTable(precision, 0, quality, QuantizationTableType.Luminance);

        quantizationTables[1] = JpegCodec.GetQuantizationTable(precision, 1, quality, QuantizationTableType.Chrominance);

        using var dqt = new QuantizationTables(quantizationTables[0].Count + quantizationTables[1].Count);

        dqt.Tables = quantizationTables;

        QuantizationTables.Add(dqt);
    }

    public override bool Equals(object obj)
     => obj is JpegState jpegState && Equals(jpegState);

    public bool Equals(JpegState other)
        => StartOfFrameFunctionCode == other.StartOfFrameFunctionCode &&
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
