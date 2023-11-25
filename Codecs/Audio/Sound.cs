using Media.Codec;
using Media.Codecs.Audio;
using System.IO;

namespace Codecs.Audio;

public class Sound : AudioBuffer
{
    public static Sound FromStream(Stream stream)
    {
        //Load WaveHeader without RiffReader
        using var waveFormat = new WaveFormat();

        if (20 != stream.Read(waveFormat.Array, waveFormat.Offset, 20))
            throw new System.InvalidOperationException();

        //Check Riff, Wave fmt?
        int fileSize = Media.Common.Binary.Read32(waveFormat.Array, 4, Media.Common.Binary.IsBigEndian);

        //Read in WaveFormat
        if (WaveFormat.Size != stream.Read(waveFormat.Array, waveFormat.Offset, WaveFormat.Size))
            throw new System.InvalidOperationException();

        if (!waveFormat.IsValid)
            throw new System.InvalidOperationException();

        var componentCount = waveFormat.NumChannels;
        var componentSize = waveFormat.BitsPerSample / componentCount;

        var components = new MediaComponent[componentCount];
        for (var c = 0; c < componentCount; c++)
            components[c] = new MediaComponent((byte)c, componentSize);

        var audioFormat = new AudioFormat(waveFormat.SampleRate, true, Media.Common.Binary.ByteOrder.Little, DataLayout.Packed, components);

        //Read data chunk with size
        stream.Read(waveFormat.Array, waveFormat.Offset, 8);

        int dataSize = Media.Common.Binary.Read32(waveFormat.Array, 4, Media.Common.Binary.IsBigEndian);

        var sound = new Sound(audioFormat, dataSize / audioFormat.SampleRate);

        //Read data into the sound.
        stream.Read(sound.Data.Array);

        return sound;
    }


    public Sound(AudioFormat audioFormat, int numberOfSamples = 1, bool shouldDispose = true)
        : base(audioFormat, numberOfSamples, shouldDispose)
    {
    }

    public WaveFormat WaveFormat => new(WaveFormatId.Pcm, AudioFormat.Channels, AudioFormat.SampleRate, AudioFormat.BitsPerSample);

    public void SaveWave(Stream stream)
    {
        //Write WaveHeader without RiffWriter
        var fileHeader = new byte[]
        {
            (byte)'R', (byte)'I', (byte) 'F', (byte)'F',
            0, 0, 0, 0, //FileSize
            (byte)'W', (byte)'A', (byte) 'V', (byte)'E',
            (byte)'f', (byte)'m', (byte) 't', (byte)' ',
            0x00, 0x00, 0x00, 0x16, //Length
            ///Could put data chunk here to save on allocations and gc pressure.
        };

        int fileSize = fileHeader.Length + WaveFormat.Size + Data.Count;

        Media.Common.Binary.Write32(fileHeader, 4, Media.Common.Binary.IsBigEndian, fileSize - 8);

        stream.Write(fileHeader);

        using var waveFormat = WaveFormat;
        stream.Write(waveFormat.Array, waveFormat.Offset, waveFormat.Count);

        var data = new byte[]
        {
            (byte)'d', (byte)'a', (byte) 't', (byte)'a',
            0, 0, 0, 0
        };

        Media.Common.Binary.Write32(data, 4, Media.Common.Binary.IsBigEndian, Data.Count);

        stream.Write(Data.Array, Data.Offset, Data.Count);
    }
}
