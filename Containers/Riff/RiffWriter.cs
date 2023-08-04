using Media.Common;
using Media.Container;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection.PortableExecutable;
using static Media.Containers.Riff.RiffReader;

namespace Media.Containers.Riff;

#region Nested Types

public class Chunk : Node
{
    public FourCharacterCode ChunkId => (FourCharacterCode)Binary.Read32(Identifier, 0, Binary.IsBigEndian);

    public Chunk(RiffWriter writer, FourCharacterCode chunkId, long dataSize)
        : base(writer, Binary.GetBytes((long)chunkId, Binary.IsBigEndian), RiffReader.LengthSize, 0, dataSize, true)
    {
    }

    public Chunk(RiffWriter writer, FourCharacterCode chunkId, byte[] data)
        : base(writer, Binary.GetBytes((long)chunkId, Binary.IsBigEndian), RiffReader.LengthSize, 0, data)
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

public class RiffChunk : Chunk
{
    public RiffChunk(RiffWriter writer, FourCharacterCode type, long dataSize)
        : base(writer, type, dataSize)
    {
    }
}

public class FormatChunk : Chunk
{
    public FormatChunk(RiffWriter writer, byte[] data)
        : base(writer, FourCharacterCode.fmt, data.Length)
    {
        data.CopyTo(Data, 0);
    }
}

public class WaveFormat : MemorySegment
{
    const int Size = 16;

    // Fields specific to WaveFormat
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

    public WaveFormat(AudioEncoding audioFormat, int numChannels, int sampleRate, int bitsPerSample)
        : base(new byte[Size])
    {
        AudioFormat = (short)audioFormat;
        NumChannels = (short)numChannels;
        SampleRate = sampleRate;
        BitsPerSample = (short)bitsPerSample;

        // Calculate and set the other fields based on the given values
        BlockAlign = (short)(NumChannels * (BitsPerSample / 8));
        ByteRate = SampleRate * BlockAlign;
    }

    public WaveFormat(byte[] data, int offset)
        : base(data, offset)
    {
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
    private readonly FourCharacterCode Type;
    private readonly List<Chunk> chunks = new List<Chunk>();

    public override Node Root => chunks[0];

    public override Node TableOfContents => chunks[1];

    public RiffWriter(Uri filename, FourCharacterCode type)
        : base(filename, FileAccess.ReadWrite)
    {
        Type = type;
        AddChunk(new RiffChunk(this, Type, 0));
    }

    internal protected void WriteFourCC(FourCharacterCode fourCC) => WriteInt32LittleEndian((int)fourCC);

    public void AddChunk(Chunk chunk)
    {
        if (chunk == null)
            throw new ArgumentNullException(nameof(chunk));

        chunks.Add(chunk);

        Write(chunk);
    }

    public override IEnumerator<Node> GetEnumerator() => chunks.GetEnumerator();

    public override IEnumerable<Track> GetTracks() => Tracks;

    public override SegmentStream GetSample(Track track, out TimeSpan duration)
    {
        throw new NotImplementedException();
    }
}

public class UnitTests
{
    public static void WriteManaged()
    {
        var audioData = GenerateTwinkleTwinkleLittleStar();

        // Put in Media/Audio/wav so we can read it.
        string localPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/Media/Audio/wav/";

        // Replace with your desired output file path
        string outputFilePath = Path.GetFullPath(localPath + "twinkle_twinkle_little_star.wav");

        System.IO.File.WriteAllBytes(outputFilePath, Common.MemorySegment.Empty.Array);


        // Create the RiffWriter with the appropriate type and subtype for Wave files.
        using (RiffWriter writer = new RiffWriter(new Uri("file://" + outputFilePath), FourCharacterCode.RIFF))
        {
            // Create the necessary chunks for the Wave file.
            // Note: We will use default values for AviStreamHeader since they are not important for this example.
            AviStreamHeader streamHeader = new AviStreamHeader(writer)
            {
                StreamType = FourCharacterCode.WAVE,
                HandlerType = FourCharacterCode.data,
                SampleRate = 44100,
                Start = 0,
                Length = audioData.Length * sizeof(short),
                SuggestedBufferSize = 0,
                Quality = -1,
                SampleSize = 16,
                FrameRate = 0,
                Scale = 1,
                Rate = 0,
                StartInitialFrames = 0,
                ExtraDataSize = 0
            };

            // Add the audio data (samples) to the DataChunk.
            using (DataChunk dataChunk = new DataChunk(writer, audioData.Length * sizeof(short)))
            {
                using (BinaryWriter dataWriter = new BinaryWriter(dataChunk.DataStream))
                {
                    foreach (short sample in audioData)
                    {
                        dataWriter.Write(sample);
                    }
                }

                // Add the chunks to the RiffWriter.
                writer.AddChunk(streamHeader);
                writer.AddChunk(dataChunk);
            }
        }

        Console.WriteLine("Wave file 'output.wav' written successfully!");
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

    public static void WriteRaw()
    {
        // Put in Media/Audio/wav so we can read it.
        string localPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "/Media/Audio/wav/";

        // Replace with your desired output file path
        string outputFilePath = Path.GetFullPath(localPath + "row_row_row_your_boat.wav");

        System.IO.File.WriteAllBytes(outputFilePath, Common.MemorySegment.Empty.Array);

        // Create the RiffFileWriter and WaveFileHeader
        using (var riffFileWriter = new RiffWriter(new Uri("file://" + outputFilePath), FourCharacterCode.RIFF))
        {
            WaveFormat waveFormat = new WaveFormat(AudioEncoding.PCM, numChannels: 1, sampleRate: 44100, bitsPerSample: 16);
            FormatChunk waveFormatChunk = new FormatChunk(riffFileWriter, waveFormat.Array);
            riffFileWriter.AddChunk(waveFormatChunk);

            // Audio data for "Row, Row, Row Your Boat"
            short[] audioData = GenerateRowYourBoat();

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