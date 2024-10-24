using Codec.Jpeg.Markers;
using System;
using static Media.Codec.Jpeg.JpegCodec;
using System.Collections.Generic;

namespace Codec.Jpeg.Classes;

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

    public HuffmanTable DcTable = new HuffmanTable
    {
        Id = 0,
        MinCode = [0, 1, 5, 6, 14, 30, 62, 126, 254, 510, 1022, 2046, 4094, 8190, 16382, 32766],
        MaxCode = [0, 1, 5, 6, 14, 30, 62, 126, 254, 510, 1022, 2046, 4094, 8190, 16382, 32766],
        ValPtr = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15],
        Values = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15],
        CodeTable = new Dictionary<int, (int code, int length)>
                    {
                        { 0, (0b00, 2) },
                        { 1, (0b01, 2) },
                        { 2, (0b100, 3) },
                        { 3, (0b101, 3) },
                        { 4, (0b1100, 4) },
                        { 5, (0b1101, 4) },
                        { 6, (0b11100, 5) },
                        { 7, (0b11101, 5) },
                        { 8, (0b111100, 6) },
                        { 9, (0b111101, 6) },
                        { 10, (0b1111100, 7) },
                        { 11, (0b1111101, 7) },
                        { 12, (0b11111100, 8) },
                        { 13, (0b11111101, 8) },
                        { 14, (0b111111100, 9) },
                        { 15, (0b111111101, 9) }
                    }
    };

    public HuffmanTable AcTable = new HuffmanTable
    {
        Id = 1,
        MinCode = [0, 1, 5, 6, 14, 30, 62, 126, 254, 510, 1022, 2046, 4094, 8190, 16382, 32766],
        MaxCode = [0, 1, 5, 6, 14, 30, 62, 126, 254, 510, 1022, 2046, 4094, 8190, 16382, 32766],
        ValPtr = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15],
        Values = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15],
        CodeTable = new Dictionary<int, (int code, int length)>
                    {
                        { 0, (0b00, 2) },
                        { 1, (0b01, 2) },
                        { 2, (0b100, 3) },
                        { 3, (0b101, 3) },
                        { 4, (0b1100, 4) },
                        { 5, (0b1101, 4) },
                        { 6, (0b11100, 5) },
                        { 7, (0b11101, 5) },
                        { 8, (0b111100, 6) },
                        { 9, (0b111101, 6) },
                        { 10, (0b1111100, 7) },
                        { 11, (0b1111101, 7) },
                        { 12, (0b11111100, 8) },
                        { 13, (0b11111101, 8) },
                        { 14, (0b111111100, 9) },
                        { 15, (0b111111101, 9) }
                    }
    };

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

    /// <summary>
    /// Constructs a <see cref="JpegState"/>
    /// </summary>
    /// <param name="data"></param>
    public JpegState(StartOfScan sos)
    {
        StartOfFrameFunctionCode = sos.FunctionCode;
        Ss = (byte)sos.Ss;
        Se = (byte)sos.Se;
        Ah = (byte)sos.Ah;
        Al = (byte)sos.Al;
    }


    /// <summary>
    /// Copy constructor
    /// </summary>
    /// <param name="other"></param>
    public JpegState (JpegState other)
    {
        StartOfFrameFunctionCode = other.StartOfFrameFunctionCode;
        Ss = other.Ss;
        Se = other.Se;
        Ah = other.Ah;
        Al = other.Al;
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
