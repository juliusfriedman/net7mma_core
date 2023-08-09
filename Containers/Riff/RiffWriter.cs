using Media.Common;
using Media.Container;
using System;
using System.Collections.Generic;
using System.IO;
using static Media.Containers.Riff.RiffReader;

namespace Media.Containers.Riff;

#region Chunks

public class Chunk : Node
{
    public FourCharacterCode ChunkId
    {
        get => (FourCharacterCode)Binary.Read32(Identifier, 0, Binary.IsBigEndian);
        set => Binary.Write32(Identifier, 0, Binary.IsBigEndian, (int)value);
    }

    public bool HasSubType => Identifier.Length > RiffReader.TWODWORDSSIZE;

    public FourCharacterCode SubType
    {
        get => (FourCharacterCode)Binary.Read32(Identifier, RiffReader.TWODWORDSSIZE, Binary.IsBigEndian);
        set => Binary.Write32(Identifier, RiffReader.TWODWORDSSIZE, Binary.IsBigEndian, (int)value);
    }

    public int Length
    {
        get => Binary.Read32(Identifier, RiffReader.IdentifierSize, Binary.IsBigEndian);
        set => Binary.Write32(Identifier, RiffReader.IdentifierSize, Binary.IsBigEndian, value);
    }

    public Chunk(RiffWriter writer, FourCharacterCode chunkId, int dataSize)
        : base(writer, RiffReader.HasSubType(chunkId) ? new byte[RiffReader.TWODWORDSSIZE + RiffReader.LengthSize] : new byte[RiffReader.TWODWORDSSIZE], RiffReader.LengthSize, -1, dataSize, true)
    {
        ChunkId = chunkId;
        Length = dataSize;
    }

    public Chunk(RiffWriter writer, FourCharacterCode chunkId, byte[] data)
        : base(writer, Binary.GetBytes((long)chunkId, Binary.IsBigEndian), RiffReader.LengthSize, -1, data)
    {
        ChunkId = chunkId;
        Length = data.Length;
    }

    public void UpdateSize()
    {
        Master.WriteAt(DataOffset, Identifier, RiffReader.IdentifierSize, RiffReader.IdentifierSize);
    }
}

public class DataChunk : Chunk
{
    public DataChunk(RiffWriter writer, byte[] data)
        : base(writer, FourCharacterCode.data, data)
    {
    }
}

public class RiffChunk : Chunk
{
    public RiffChunk(RiffWriter writer, FourCharacterCode type, FourCharacterCode subType, int dataSize = 0)
        : base(writer, type, dataSize)
    {        
        SubType = subType;
    }

    public RiffChunk(RiffWriter writer, FourCharacterCode chunkId, FourCharacterCode subType, byte[] data) : base(writer, chunkId, data)
    {
        SubType = subType;
    }
}

public class ListChunk : RiffChunk
{
    public ListChunk(RiffWriter writer, FourCharacterCode chunkId, FourCharacterCode subType, int dataSize) : base(writer, chunkId, subType, dataSize)
    {
    }
    public ListChunk(RiffWriter writer, FourCharacterCode chunkId, FourCharacterCode subType, byte[] data) : base(writer, chunkId, subType, data)
    {
    }
}

public class FmtChunk : Chunk
{
    public ushort AudioFormat
    {
        get => Binary.ReadU16(Data, 0, Binary.IsBigEndian);
        set => Binary.Write16(Data, 0, Binary.IsBigEndian, value);
    }

    public ushort NumChannels
    {
        get => Binary.ReadU16(Data, 2, Binary.IsBigEndian);
        set => Binary.Write16(Data, 2, Binary.IsBigEndian, value);
    }

    public uint SampleRate
    {
        get => Binary.ReadU32(Data, 4, Binary.IsBigEndian);
        set => Binary.Write32(Data, 4, Binary.IsBigEndian, value);
    }

    public uint ByteRate
    {
        get => Binary.ReadU32(Data, 8, Binary.IsBigEndian);
        set => Binary.Write32(Data, 8, Binary.IsBigEndian, value);
    }

    public ushort BlockAlign
    {
        get => Binary.ReadU16(Data, 12, Binary.IsBigEndian);
        set => Binary.Write16(Data, 12, Binary.IsBigEndian, value);
    }

    public ushort BitsPerSample
    {
        get => Binary.ReadU16(Data, 14, Binary.IsBigEndian);
        set => Binary.Write16(Data, 14, Binary.IsBigEndian, value);
    }

    public FmtChunk(RiffWriter writer, ushort audioFormat, ushort numChannels, uint sampleRate, ushort bitsPerSample)
        : base(writer, FourCharacterCode.fmt, new byte[16])
    {
        // Set the audio format
        AudioFormat = audioFormat;

        // Set the number of channels
        NumChannels = numChannels;

        // Set the sample rate
        SampleRate = sampleRate;

        // Calculate and set the block align
        ushort blockAlign = (ushort)(numChannels * (bitsPerSample / 8));
        BlockAlign = blockAlign;

        // Calculate and set the byte rate
        uint byteRate = sampleRate * BlockAlign;
        ByteRate = byteRate;

        // Set the bits per sample
        BitsPerSample = bitsPerSample;
    }
}

public class AviMainHeader : Chunk
{
    public int MicroSecPerFrame
    {
        get => Binary.Read32(Identifier, IdentifierSize, Binary.IsBigEndian);
        set => Binary.Write32(Identifier, IdentifierSize, Binary.IsBigEndian, value);
    }

    public int MaxBytesPerSec
    {
        get => Binary.Read32(Identifier, IdentifierSize + 4, Binary.IsBigEndian);
        set => Binary.Write32(Identifier, IdentifierSize + 4, Binary.IsBigEndian, value);
    }

    public int PaddingGranularity
    {
        get => Binary.Read32(Identifier, IdentifierSize + 8, Binary.IsBigEndian);
        set => Binary.Write32(Identifier, IdentifierSize + 8, Binary.IsBigEndian, value);
    }

    public AviMainHeaderFlags Flags
    {
        get => (AviMainHeaderFlags)Binary.Read32(Identifier, IdentifierSize + 12, Binary.IsBigEndian);
        set => Binary.Write32(Identifier, IdentifierSize + 12, Binary.IsBigEndian, (int)value);
    }

    public int TotalFrames
    {
        get => Binary.Read32(Identifier, IdentifierSize + 16, Binary.IsBigEndian);
        set => Binary.Write32(Identifier, IdentifierSize + 16, Binary.IsBigEndian, value);
    }

    public int InitialFrames
    {
        get => Binary.Read32(Identifier, IdentifierSize + 20, Binary.IsBigEndian);
        set => Binary.Write32(Identifier, IdentifierSize + 20, Binary.IsBigEndian, value);
    }

    public int Streams
    {
        get => Binary.Read32(Identifier, IdentifierSize + 24, Binary.IsBigEndian);
        set => Binary.Write32(Identifier, IdentifierSize + 24, Binary.IsBigEndian, value);
    }

    public int SuggestedBufferSize
    {
        get => Binary.Read32(Identifier, IdentifierSize + 28, Binary.IsBigEndian);
        set => Binary.Write32(Identifier, IdentifierSize + 28, Binary.IsBigEndian, value);
    }

    public int Width
    {
        get => Binary.Read32(Identifier, IdentifierSize + 32, Binary.IsBigEndian);
        set => Binary.Write32(Identifier, IdentifierSize + 32, Binary.IsBigEndian, value);
    }

    public int Height
    {
        get => Binary.Read32(Identifier, IdentifierSize + 36, Binary.IsBigEndian);
        set => Binary.Write32(Identifier, IdentifierSize + 36, Binary.IsBigEndian, value);
    }

    public AviMainHeader(RiffWriter writer)
        : base(writer, FourCharacterCode.avih, 56)
    {
        SubType = FourCharacterCode.avih;
    }
}

[Flags]
public enum AviMainHeaderFlags
{
    None = 0,
    HasIndex = 0x00000010,
    MustUseIndex = 0x00000020,
    IsInterleaved = 0x00000100,
    TrustCkType = 0x00000800,
    WasCaptureFile = 0x00010000,
    CopyRighted = 0x00020000
}

public class AviStreamHeader : Chunk
{
    public FourCharacterCode StreamType
    {
        get => (FourCharacterCode)Binary.Read32(Data, 0, Binary.IsBigEndian);
        set => Binary.Write32(Data, 0, Binary.IsBigEndian, (int)value);
    }

    public FourCharacterCode HandlerType
    {
        get => (FourCharacterCode)Binary.Read32(Data, 4, Binary.IsBigEndian);
        set => Binary.Write32(Data, 4, Binary.IsBigEndian, (int)value);
    }

    public int SampleRate
    {
        get => Binary.Read32(Data, 8, Binary.IsBigEndian);
        set => Binary.Write32(Data, 8, Binary.IsBigEndian, value);
    }

    public int Start
    {
        get => Binary.Read32(Data, 12, Binary.IsBigEndian);
        set => Binary.Write32(Data, 12, Binary.IsBigEndian, value);
    }

    public int Length
    {
        get => Binary.Read32(Data, 16, Binary.IsBigEndian);
        set => Binary.Write32(Data, 16, Binary.IsBigEndian, value);
    }

    public int SuggestedBufferSize
    {
        get => Binary.Read32(Data, 20, Binary.IsBigEndian);
        set => Binary.Write32(Data, 20, Binary.IsBigEndian, value);
    }

    public int Quality
    {
        get => Binary.Read32(Data, 24, Binary.IsBigEndian);
        set => Binary.Write32(Data, 24, Binary.IsBigEndian, value);
    }

    public int SampleSize
    {
        get => Binary.Read32(Data, 28, Binary.IsBigEndian);
        set => Binary.Write32(Data, 28, Binary.IsBigEndian, value);
    }

    public int FrameRate
    {
        get => Binary.Read32(Data, 32, Binary.IsBigEndian);
        set => Binary.Write32(Data, 32, Binary.IsBigEndian, value);
    }

    public int Scale
    {
        get => Binary.Read32(Data, 36, Binary.IsBigEndian);
        set => Binary.Write32(Data, 36, Binary.IsBigEndian, value);
    }

    public int Rate
    {
        get => Binary.Read32(Data, 40, Binary.IsBigEndian);
        set => Binary.Write32(Data, 40, Binary.IsBigEndian, value);
    }

    public int StartInitialFrames
    {
        get => Binary.Read32(Data, 44, Binary.IsBigEndian);
        set => Binary.Write32(Data, 44, Binary.IsBigEndian, value);
    }

    public int ExtraDataSize
    {
        get => Binary.Read32(Data, 48, Binary.IsBigEndian);
        set => Binary.Write32(Data, 48, Binary.IsBigEndian, value);
    }

    public AviStreamHeader(RiffWriter writer)
        : base(writer, FourCharacterCode.avih, 56)
    {
    }
}

#endregion

public class RiffWriter : MediaFileWriter
{
    private readonly List<Chunk> chunks = new List<Chunk>();

    public override Node Root => chunks[0];

    public override Node TableOfContents => chunks[1];

    public RiffWriter(Uri filename, FourCharacterCode type, FourCharacterCode subType)
        : base(filename, FileAccess.ReadWrite)
    {

        AddChunk(new RiffChunk(this, type, subType, 0));
    }

    internal protected void WriteFourCC(FourCharacterCode fourCC) => WriteInt32LittleEndian((int)fourCC);

    //TODO, should not write when added, only when flushed etc
    public void AddChunk(Chunk chunk)
    {
        if (chunk == null)
            throw new ArgumentNullException(nameof(chunk));

        chunks.Add(chunk);
        chunk.DataOffset = Position;
        
        if(chunk.Length == 0)
            chunk.Length = (int)chunk.DataSize;
        else if (Binary.IsOdd(chunk.Length))
            chunk.Length++;

        Write(chunk);

        //Write any padding 
        var paddingBytes = chunk.Length - chunk.DataSize;

        for (int i = 0; i < paddingBytes; ++i) WriteByte(0);
    }

    public override void Close()
    {
        Seek(IdentifierSize, SeekOrigin.Begin);
        WriteInt32LittleEndian((int)Length - IdentifierSize);

        //Foreach Chunk ensure Length was set and write it?

        base.Close();
    }

    public override IEnumerator<Node> GetEnumerator() => chunks.GetEnumerator();

    public override IEnumerable<Track> GetTracks() => Tracks;

    public override SegmentStream GetSample(Track track, out TimeSpan duration)
    {
        throw new NotImplementedException();
    }

    public override string ToTextualConvention(Node node) => RiffReader.ToFourCharacterCode(node.Identifier);

    public override Track CreateTrack(Sdp.MediaType mediaType)
    {
        return new Track(new Chunk(this, mediaType == Sdp.MediaType.audio ? FourCharacterCode.auds : mediaType == Sdp.MediaType.text ? FourCharacterCode.txts : mediaType == Sdp.MediaType.video ? FourCharacterCode.vids : FourCharacterCode.JUNK, null), string.Empty, 0, DateTime.UtcNow, DateTime.UtcNow, 0, 0, 0, TimeSpan.Zero, TimeSpan.Zero, 60, mediaType, new byte[4]);
    }

    public override bool TryAddTrack(Track track)
    {
        if (track.Header.Master != this) return false;
        if (Tracks.Contains(track)) return false;

        Tracks.Add(track);

        //Some data in the track... needs to be written
        track.Header.Data = new byte[track.DataStream.Length];

        //Copy any dataStream in the track to the dataStream in the header.
        track.DataStream.CopyTo(track.Header.DataStream);

        //Write the header
        Write(track.Header);

        return true;
    }
}

public class UnitTests
{
    // Function to generate a simple sine wave sound
    internal static short[] GenerateSineWave(int durationInSeconds, int sampleRate, double frequency)
    {
        int numSamples = durationInSeconds * sampleRate;
        double amplitude = 32760.0; // Max amplitude for 16-bit signed PCM
        double twoPiF = 2.0 * Math.PI * frequency;
        short[] samples = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double t = (double)i / sampleRate;
            samples[i] = (short)(amplitude * Math.Sin(twoPiF * t));
        }

        return samples;
    }

    // Convert the short[] audio data to a byte[] for the DataChunk
    internal static byte[] ConvertAudioDataToBytes(short[] audioData)
    {
        byte[] bytes = new byte[audioData.Length * sizeof(short)];
        Buffer.BlockCopy(audioData, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    public static void WriteManaged()
    {
        int durationInSeconds = 5;
        int sampleRate = 44100;
        double frequency = 440.0; // A4 note frequency (440 Hz)

        // Generate the audio data (sine wave)
        short[] audioData = GenerateSineWave(durationInSeconds, sampleRate, frequency);

        // Put in Media/Audio/wav so we can read it.
        string localPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/Media/Audio/wav/";

        // Replace with your desired output file path
        string outputFilePath = Path.GetFullPath(localPath + "twinkle_twinkle_little_star_managed.wav");

        System.IO.File.WriteAllBytes(outputFilePath, Common.MemorySegment.Empty.Array);

        // Create the RiffWriter with the appropriate type and subtype for Wave files.
        using (RiffWriter writer = new RiffWriter(new Uri("file://" + outputFilePath), FourCharacterCode.RIFF, FourCharacterCode.WAVE))
        {
            // Create the necessary chunks for the Wave file.
            // Note: We will use default values for FmtChunk since they are not important for this example.
            FmtChunk fmtChunk = new FmtChunk(writer, 1, 1, (uint)sampleRate, 16); // 1 channel, 16 bits per sample

            // Add the audio data (samples) to the DataChunk.
            using (DataChunk dataChunk = new DataChunk(writer, ConvertAudioDataToBytes(audioData)))
            {
                // Add the chunks to the RiffWriter.
                writer.AddChunk(fmtChunk);
                writer.AddChunk(dataChunk);
            }
        }

        Console.WriteLine("Wave file written successfully!");
    }

    public static void WriteRaw()
    {
        var audioData = GenerateRowYourBoat();

        // Put in Media/Audio/wav so we can read it.
        string localPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/Media/Audio/wav/";

        // Replace with your desired output file path
        string outputFilePath = Path.GetFullPath(localPath + "twinkle_twinkle_little_star_raw.wav");

        System.IO.File.WriteAllBytes(outputFilePath, Common.MemorySegment.Empty.Array);

        // Create the RiffFileWriter and WaveFileHeader
        using (var writer = new RiffWriter(new Uri("file://" + outputFilePath), FourCharacterCode.RIFF, FourCharacterCode.WAVE))
        {
            // Create the necessary chunks for the Wave file
            FmtChunk fmtChunk = new FmtChunk(writer, 1, 1, 44100, 16); // 1 channel, 16 bits per sample
            DataChunk dataChunk = new DataChunk(writer, ConvertAudioDataToBytes(audioData));

            writer.AddChunk(fmtChunk);
            writer.AddChunk(dataChunk);
        }

        Console.WriteLine("Wave file written successfully!");
    }

    // Sample audio data for "Row, Row, Row Your Boat"
    public static short[] GenerateRowYourBoat()
    {
        double amplitude = 0.3; // Adjust the amplitude to control the volume
        int sampleRate = 44100;
        int durationMs = 500;
        int numSamples = (durationMs * sampleRate) / 1000;

        // The musical notes of the song (D, D, E, D, F, E)
        double[] frequencies = { 293.66, 293.66, 329.63, 293.66, 349.23, 329.63 };

        short[] audioData = new short[numSamples];

        for (int i = 0; i < numSamples; i++)
        {
            double time = i / (double)sampleRate;
            int noteIndex = (int)((time / durationMs) * frequencies.Length);
            double frequency = frequencies[noteIndex];

            double sineWave = amplitude * Math.Sin(2 * Math.PI * frequency * time);

            // Convert the double sample value to a 16-bit PCM value (-32768 to 32767)
            audioData[i] = (short)(sineWave * short.MaxValue);
        }

        return audioData;
    }
}