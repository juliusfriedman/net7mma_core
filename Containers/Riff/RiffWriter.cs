using Media.Common;
using Media.Container;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using static Media.Containers.Riff.RiffReader;

namespace Media.Containers.Riff;

#region Nested Types

public abstract class Chunk : Node
{
    public FourCharacterCode ChunkId => (FourCharacterCode)BitConverter.ToInt32(Identifier, 0);

    public Chunk(RiffWriter writer, FourCharacterCode chunkId, long dataSize)
        : base(writer, BitConverter.GetBytes((long)chunkId), RiffReader.LengthSize, 0, dataSize, false)
    {
    }
}

public class RiffChunk : Chunk
{
    public RiffChunk(RiffWriter writer, FourCharacterCode chunkId, long dataSize)
        : base(writer, chunkId, dataSize)
    {
    }
}

public class DataChunk : Chunk
{
    public DataChunk(RiffWriter writer, long dataSize)
        : base(writer, FourCharacterCode.data, dataSize)
    {
    }
}

public class FormatChunk : Chunk
{
    private readonly WaveFormat _waveFormat;

    public FormatChunk(RiffWriter writer, WaveFormat waveFormat)
        : base(writer, FourCharacterCode.fmt, waveFormat.GetDataSize())
    {
        _waveFormat = waveFormat;
    }

    protected void WriteChunkData(MediaFileWriter writer)
    {
        // Write the WaveFormat data to the file
        _waveFormat.WriteToStream(writer);
    }
}

public class WaveFormat
{
    public int AudioFormat { get; set; } = 1;
    public int NumChannels { get; set; }
    public int SampleRate { get; set; }
    public int BitsPerSample { get; set; }
    public int BlockAlign => NumChannels * BitsPerSample / 8;
    public int AverageBytesPerSecond => SampleRate * BlockAlign;

    public WaveFormat(int numChannels, int sampleRate, int bitsPerSample)
    {
        NumChannels = numChannels;
        SampleRate = sampleRate;
        BitsPerSample = bitsPerSample;
    }

    public int GetDataSize()
    {
        // Calculate the size of the WaveFormat data (16 bytes for PCM)
        return 16;
    }

    public void WriteToStream(MediaFileWriter writer)
    {
        // Write the WaveFormat data to the stream
        writer.WriteInt16LittleEndian((short)AudioFormat);
        writer.WriteInt16LittleEndian((short)NumChannels);
        writer.WriteInt32LittleEndian(SampleRate);
        writer.WriteInt32LittleEndian(AverageBytesPerSecond);
        writer.WriteInt16LittleEndian((short)BlockAlign);
        writer.WriteInt16LittleEndian((short)BitsPerSample);
    }
}

public enum AudioEncoding : ushort
{
    PCM = 1, // Pulse Code Modulation (Linear PCM)
    IEEE_FLOAT = 3, // IEEE Float
    ALAW = 6, // 8-bit ITU-T G.711 A-law
    MULAW = 7, // 8-bit ITU-T G.711 µ-law
    EXTENSIBLE = 0xFFFE // Determined by SubFormat
                        // Add more encodings as needed
}

public class AviStreamHeader : Chunk
{
    public int StreamType { get; set; }
    public int HandlerType { get; set; }
    public int Flags { get; set; }
    public short Priority { get; set; }
    public short Language { get; set; }
    public int InitialFrames { get; set; }
    public int Scale { get; set; }
    public int Rate { get; set; }
    public int Start { get; set; }
    public int Length { get; set; }
    public int SuggestedBufferSize { get; set; }
    public int Quality { get; set; }
    public int SampleSize { get; set; }
    public Complex FrameRate { get; set; }
    public MemorySegment Name { get; set; }

    public AviStreamHeader(RiffWriter master)
        : base(master, FourCharacterCode.strh, 56)
    {
        StreamType = (int)FourCharacterCode.vids; // Video stream
        HandlerType = 0;
        Flags = 0;
        Priority = 0;
        Language = 0;
        InitialFrames = 0;
        Scale = 1; // Time scale is in seconds
        Rate = 30; // Frame rate (frames per second)
        Start = 0;
        Length = 0; // 0 means the stream is continuous
        SuggestedBufferSize = 0;
        Quality = -1; // Use default quality
        SampleSize = 0; // 0 means the sample size is variable
        FrameRate = new Complex(1, Rate);
        Name = new MemorySegment(new byte[64]); // Empty name
    }
}

#endregion

public class RiffWriter : MediaFileWriter
{
    private readonly List<Chunk> chunks = new List<Chunk>();
    private FourCharacterCode riffType;
    private FourCharacterCode fileType;
    private long dataChunkSizeOffset;
    private long riffChunkSizeOffset;
    private long fmtChunkSizeOffset;

    public override Node Root => chunks[0];

    public override Node TableOfContents => chunks.FirstOrDefault(c => c.ChunkId == FourCharacterCode.avih);

    public RiffWriter(Uri filename, FourCharacterCode riffType, FourCharacterCode fileType)
        : base(filename, FileAccess.ReadWrite)
    {
        if (!Enum.IsDefined(typeof(FourCharacterCode), fileType))
            throw new ArgumentException("Invalid file type.", nameof(fileType));

        this.riffType = riffType;
        this.fileType = fileType;

        AddChunk(new RiffChunk(this, riffType, 0));
    }

    internal protected void WriteFourCC(FourCharacterCode fourCC) => WriteInt32LittleEndian((int)fourCC);

    public override void Close()
    {
        foreach (var chunk in chunks)
            Write(chunk);

        // Update data chunk size
        long dataSize = Position - dataChunkSizeOffset - 4;
        Seek(dataChunkSizeOffset, SeekOrigin.Begin);
        WriteInt32LittleEndian((int)dataSize);

        // Update fmt chunk size
        long fmtChunkSize = Position - fmtChunkSizeOffset - 4;
        Seek(fmtChunkSizeOffset, SeekOrigin.Begin);
        WriteInt32LittleEndian((int)fmtChunkSize);

        // Update RIFF chunk size
        long riffChunkSize = Position - riffChunkSizeOffset - 4;
        Seek(riffChunkSizeOffset, SeekOrigin.Begin);
        WriteInt32LittleEndian((int)riffChunkSize);

        base.Close();
    }

    public void AddChunk(Chunk chunk)
    {
        if (chunk == null)
            throw new ArgumentNullException(nameof(chunk));

        chunks.Add(chunk);
    }

    public override IEnumerator<Node> GetEnumerator() => chunks.GetEnumerator();

    public override IEnumerable<Track> GetTracks() => Tracks;

    public override SegmentStream GetSample(Track track, out TimeSpan duration)
    {
        throw new NotImplementedException();
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        //Put in Media/Audio/wav so we can read it..
        string localPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/Media/Audio/wav/";

        // Replace with your desired output file path
        string outputFilePath = Path.GetFullPath(localPath + "twinkle_twinkle_little_star.wav");

        System.IO.File.WriteAllBytes(outputFilePath, Common.MemorySegment.Empty.Array);

        // Sample audio data for "Twinkle Twinkle Little Star"
        var audioData = GenerateTwinkleTwinkleLittleStar();

        // Audio format properties
        int sampleRate = 44100;
        int channels = 1;
        int bitDepth = 16;

        // Create the RiffFileWriter and WaveFileHeader
        using (var riffFileWriter = new RiffWriter(new Uri("file://" + outputFilePath), FourCharacterCode.RIFF, FourCharacterCode.WAVE))
        {
            WaveFormat waveFormat = new WaveFormat(channels, sampleRate, bitDepth);
            FormatChunk waveFormatChunk = new FormatChunk(riffFileWriter, new WaveFormat(channels, sampleRate, bitDepth));
            riffFileWriter.AddChunk(waveFormatChunk);

            // Calculate the data size for the audio samples
            int dataChunkDataSize = audioData.Length * sizeof(short);

            // Write the DataChunk identifier
            riffFileWriter.WriteFourCC(FourCharacterCode.data);

            // Write the data size
            riffFileWriter.WriteInt32LittleEndian(dataChunkDataSize);

            // Write the audio samples
            foreach (var sampleValue in audioData)
            {
                // Write the 16-bit PCM value to the RiffFileWriter
                riffFileWriter.WriteInt16LittleEndian(sampleValue);
            }
        }

        Console.WriteLine("Wave file generated successfully!");
    }

    // Sample audio data for "Twinkle Twinkle Little Star"
    public static short[] GenerateTwinkleTwinkleLittleStar()
    {
        double amplitude = 0.3; // Adjust the amplitude to control the volume
        int sampleRate = 44100;
        int durationMs = 500;
        int numSamples = (durationMs * sampleRate) / 1000;
        double frequency = 261.63; // Frequency of C4 note (middle C)

        short[] audioData = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double time = i / (double)sampleRate;
            double sineWave = amplitude * Math.Sin(2 * Math.PI * frequency * time);

            // Convert the double sample value to a 16-bit PCM value (-32768 to 32767)
            audioData[i] = (short)(sineWave * short.MaxValue);
        }

        return audioData;
    }
}