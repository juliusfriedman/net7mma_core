using Media.Common;

namespace Media.Codecs.Audio;

public class WaveFormat : MemorySegment
{
    const int Size = 18;

    public WaveFormatId WaveFormatId => (WaveFormatId)AudioFormat;

    public short AudioFormat
    {
        get => Binary.Read16(Array, Offset, Binary.IsBigEndian);
        set => Binary.Write16(Array, Offset, Binary.IsBigEndian, value);
    }

    public short NumChannels
    {
        get => Binary.Read16(Array, Offset + 2, Binary.IsBigEndian);
        set => Binary.Write16(Array, Offset + 2, Binary.IsBigEndian, value);
    }

    public int SampleRate
    {
        get => Binary.Read32(Array, Offset + 4, Binary.IsBigEndian);
        set => Binary.Write32(Array, Offset + 4, Binary.IsBigEndian, value);
    }

    public int ByteRate
    {
        get => Binary.Read32(Array, Offset + 8, Binary.IsBigEndian);
        set => Binary.Write32(Array, Offset + 8, Binary.IsBigEndian, value);
    }

    public short BlockAlign
    {
        get => Binary.Read16(Array, Offset + 12, Binary.IsBigEndian);
        set => Binary.Write16(Array, Offset + 12, Binary.IsBigEndian, value);
    }

    public short BitsPerSample
    {
        get => Binary.Read16(Array, Offset + 14, Binary.IsBigEndian);
        set => Binary.Write16(Array, Offset + 14, Binary.IsBigEndian, value);
    }

    public short ExtraSize
    {
        get => Binary.Read16(Array, Offset + 16, Binary.IsBigEndian);
        set => Binary.Write16(Array, Offset + 16, Binary.IsBigEndian, value);
    }

    public WaveFormat(WaveFormatId audioFormat, int numChannels, int sampleRate, int bitsPerSample)
        : base(new byte[Size])
    {
        AudioFormat = (short)audioFormat;
        NumChannels = (short)numChannels;
        SampleRate = sampleRate;
        BitsPerSample = (short)bitsPerSample;

        // Calculate and set the other fields based on the given values
        BlockAlign = (short)(NumChannels * (BitsPerSample / Binary.BitsPerByte));
        ByteRate = SampleRate * BlockAlign;
    }

    public WaveFormat(byte[] data, int offset)
        : base(data, offset)
    {
    }

    /// <summary>
    ///     Converts a duration in milliseconds to a duration in bytes.
    /// </summary>
    /// <param name="milliseconds">Duration in millisecond to convert to a duration in bytes.</param>
    /// <returns>Duration in bytes.</returns>
    public long MillisecondsToBytes(double milliseconds)
    {
        var result = (long)(ByteRate / 1000.0 * milliseconds);
        result -= result % BlockAlign;
        return result;
    }

    /// <summary>
    ///     Converts a duration in bytes to a duration in milliseconds.
    /// </summary>
    /// <param name="bytes">Duration in bytes to convert to a duration in milliseconds.</param>
    /// <returns>Duration in milliseconds.</returns>
    public double BytesToMilliseconds(long bytes)
    {
        bytes -= bytes % BlockAlign;
        var result = bytes / (double)ByteRate * 1000.0;
        return result;
    }
}