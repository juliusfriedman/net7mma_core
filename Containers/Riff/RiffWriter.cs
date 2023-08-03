using Media.Common;
using Media.Common.Interfaces;
using Media.Container;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using static Media.Containers.Riff.RiffReader;

public class RiffWriter : MediaFileWriter
{
    #region Nested Types

    public abstract class Chunk : Media.Container.Node
    {
        public FourCharacterCode ChunkId => (FourCharacterCode)Binary.Read32(Identifier, 0, false);
        public Chunk(IMediaContainer master, FourCharacterCode chunkId)
            :base(master, BitConverter.GetBytes((long)chunkId), 0, 0, 0, true)
        {
        }

        protected abstract int GetChunkDataSize();
        protected abstract void WriteChunkData(MediaFileWriter writer);
    }

    public class DataChunk : Chunk
    {
        private byte[] data;

        public DataChunk(IMediaContainer master, FourCharacterCode chunkId) : base(master, chunkId)
        {
        }

        public void SetData(byte[] data)
        {
            this.data = data;
        }

        protected override int GetChunkDataSize()
        {
            return data?.Length ?? 0;
        }

        protected override void WriteChunkData(MediaFileWriter writer)
        {
            writer.Write(data);
        }
    }

    public class WaveFormatChunk : DataChunk
    {
        public WaveFormatChunk(IMediaContainer master, int sampleRate, int channels, int bitDepth, int audioFormat = 1)
            : base(master, FourCharacterCode.fmt)
        {
            SampleRate = sampleRate;
            Channels = (short)channels;
            BitDepth = (short)bitDepth;
            AudioFormat = (short)audioFormat; // 1 represents PCM, which is the most common audio format
            BlockAlign = (short)(channels * (bitDepth / 8));
            ByteRate = SampleRate * BlockAlign;
        }

        public int SampleRate { get; private set; }
        public short AudioFormat { get; private set; }
        public short Channels { get; private set; }
        public int ByteRate { get; private set; }
        public short BlockAlign { get; private set; }
        public short BitDepth { get; private set; }

        protected override void WriteChunkData(MediaFileWriter writer)
        {
            // Write the format chunk data in little-endian format
            writer.WriteInt16LittleEndian(AudioFormat);
            writer.WriteInt16LittleEndian(Channels);
            writer.WriteInt32LittleEndian(SampleRate);
            writer.WriteInt32LittleEndian(ByteRate);
            writer.WriteInt16LittleEndian(BlockAlign);
            writer.WriteInt16LittleEndian(BitDepth);
        }
    }

    public class AviStreamHeader : Chunk
    {
        public AviStreamHeader(IMediaContainer master, FourCharacterCode streamType)
            : base(master, FourCharacterCode.strh)
        {
            StreamType = streamType;
        }

        public FourCharacterCode StreamType { get; private set; }

        public ushort Handler { get; set; }

        public ushort Flags { get; set; }

        public ushort Priority { get; set; }

        public ushort Language { get; set; }

        public uint InitialFrames { get; set; }

        public uint Scale { get; set; }

        public uint Rate { get; set; }

        public uint Start { get; set; }

        public uint Length { get; set; }

        public uint SuggestedBufferSize { get; set; }

        public uint Quality { get; set; }

        public uint SampleSize { get; set; }

        public Complex FrameRate
        {
            get { return new Complex((int)Rate, (int)Scale); }
            set
            {
                Rate = (uint)value.Real;
                Scale = (uint)value.Imaginary;
            }
        }

        protected override int GetChunkDataSize()
        {
            throw new NotImplementedException();
        }

        protected override void WriteChunkData(MediaFileWriter writer)
        {
            writer.WriteInt32LittleEndian((int)StreamType);
            writer.WriteInt16LittleEndian((short)Handler);
            writer.WriteInt16LittleEndian((short)Flags);
            writer.WriteInt16LittleEndian((short)Priority);
            writer.WriteInt16LittleEndian((short)Language);
            writer.WriteInt32LittleEndian((int)InitialFrames);
            writer.WriteInt32LittleEndian((int)Scale);
            writer.WriteInt32LittleEndian((int)Rate);
            writer.WriteInt32LittleEndian((int)Start);
            writer.WriteInt32LittleEndian((int)Length);
            writer.WriteInt32LittleEndian((int)SuggestedBufferSize);
            writer.WriteInt32LittleEndian((int)Quality);
            writer.WriteInt32LittleEndian((int)SampleSize);
        }
    }

    #endregion

    private List<Chunk> chunks;

    private FourCharacterCode riffType;
    private FourCharacterCode fileType;

    public override Node Root => chunks[0];

    public override Node TableOfContents => chunks.FirstOrDefault(c => c.ChunkId == FourCharacterCode.avih);

    public RiffWriter(Uri filename, FourCharacterCode riffType, FourCharacterCode fileType)
        : base(filename)
    {
        if (!Enum.IsDefined(typeof(FourCharacterCode), fileType))
            throw new ArgumentException("Invalid file type.", nameof(fileType));

        this.riffType = riffType;
        this.fileType = fileType;
    }

    //public void WriteSineWave(int frequency, int sampleRate, int bitDepth, int numChannels, int numSamples)
    //{
    //    double amplitude = (1 << (bitDepth - 1)) - 1;
    //    double angularFrequency = 2.0 * Math.PI * frequency;
    //    double timeStep = 1.0 / sampleRate;

    //    for (int sample = 0; sample < numSamples; sample++)
    //    {
    //        double time = sample * timeStep;
    //        double sampleValue = amplitude * Math.Sin(angularFrequency * time);

    //        // Write the sample value to the WAV file for each channel
    //        for (int channel = 0; channel < numChannels; channel++)
    //        {
    //            WriteSample(BitConverter.GetBytes(sampleValue));
    //        }
    //    }
    //}

    public void WriteSample(byte[] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        Write(data, 0, data.Length);
    }

    protected void WriteFourCC(FourCharacterCode fourCC) => WriteInt32LittleEndian((int)fourCC);

    public override void WriteHeader()
    {
        // Write RIFF chunk
        WriteFourCC(riffType);
        WriteInt32LittleEndian(0); // Placeholder for chunk size
        WriteFourCC(fileType);

        // Write data sub-chunk header (placeholder for data size)
        WriteFourCC(FourCharacterCode.data);
        WriteInt32LittleEndian(0); // Placeholder for data size
        WriteFourCC(fileType); // Write the fileType (e.g., AVI, WAV, etc.)
    }

    public override void Close()
    {
        // Update data chunk size
        long dataChunkSize = Position - 4;
        Seek(4, SeekOrigin.Begin);
        WriteInt32LittleEndian((int)dataChunkSize);
    }
    public void AddChunk(Chunk chunk)
    {
        if (chunk == null)
            throw new ArgumentNullException(nameof(chunk));

        if (chunks == null)
            chunks = new List<Chunk>();

        chunks.Add(chunk);
    }

    public override void WriteVideoFrame(byte[] frameData)
    {
        throw new NotImplementedException();
    }

    public override void WriteAudioSamples(byte[] audioData)
    {
        throw new NotImplementedException();
    }

    public override IEnumerator<Node> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    public override IEnumerable<Track> GetTracks() => Tracks;

    public override SegmentStream GetSample(Track track, out TimeSpan duration)
    {
        throw new NotImplementedException();
    }
}