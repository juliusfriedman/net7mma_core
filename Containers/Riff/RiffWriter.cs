using Media.Common;
using Media.Container;
using System;
using System.Collections.Generic;
using System.IO;

namespace Media.Containers.Riff;

public class RiffWriter : MediaFileWriter
{
    private readonly List<Chunk> chunks = [];

    public override Node Root => chunks[0];

    public override Node TableOfContents => chunks[1];

    public RiffWriter(Uri filename, FourCharacterCode type, FourCharacterCode subType)
        : base(filename, FileAccess.ReadWrite)
    {

        AddChunk(new HeaderChunk(this, type, subType, 0));
    }

    protected internal void WriteFourCC(FourCharacterCode fourCC) => WriteInt32LittleEndian((int)fourCC);

    //TODO, should not write when added, only when flushed etc
    public void AddChunk(Chunk chunk)
    {
        if (chunk is null)
            throw new ArgumentNullException(nameof(chunk));

        chunks.Add(chunk);
        chunk.DataOffset = Position;

        if (chunk.Length is 0)
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
        Seek(RiffReader.IdentifierSize, SeekOrigin.Begin);
        WriteInt32LittleEndian((int)Length - RiffReader.IdentifierSize);

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
        track.Header.Data = new(new byte[track.DataStream.Length]);

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
        using (RiffWriter writer = new(new Uri("file://" + outputFilePath), FourCharacterCode.RIFF, FourCharacterCode.WAVE))
        {
            // Create the necessary chunks for the Wave file.
            // Note: We will use default values for FmtChunk since they are not important for this example.
            FmtChunk fmtChunk = new(writer, 1, 1, (uint)sampleRate, 16); // 1 channel, 16 bits per sample

            // Add the audio data (samples) to the DataChunk.
            using (DataChunk dataChunk = new(writer, ConvertAudioDataToBytes(audioData)))
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
            FmtChunk fmtChunk = new(writer, 1, 1, 44100, 16); // 1 channel, 16 bits per sample
            DataChunk dataChunk = new(writer, ConvertAudioDataToBytes(audioData));

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
        int durationMs = 5000;
        int numSamples = durationMs * sampleRate / 1000;

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