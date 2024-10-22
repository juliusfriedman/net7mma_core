using Codec.Jpeg.Markers;
using System;

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
