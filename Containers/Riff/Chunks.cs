using Media.Common;
using Media.Common.Extensions.Linq;
using Media.Container;
using System;
using System.Collections.Generic;
using System.Linq;

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
        Master.WriteAt(Offset, Identifier, RiffReader.IdentifierSize, RiffReader.IdentifierSize);
    }

    public IEnumerable<byte> Prepare()
    {
        var result = Identifier.Concat(Data);

        if ((DataSize & 1) == 1)
            result = result.Concat((byte)0);

        return result;
    }
}

public class DataChunk : Chunk
{
    public DataChunk(RiffWriter writer, byte[] data)
        : base(writer, FourCharacterCode.data, data)
    {
    }
}

public class HeaderChunk : Chunk
{
    public HeaderChunk(RiffWriter writer, FourCharacterCode type, FourCharacterCode subType, int dataSize = 0)
        : base(writer, type, dataSize)
    {
        SubType = subType;
    }

    public HeaderChunk(RiffWriter writer, FourCharacterCode chunkId, FourCharacterCode subType, byte[] data) : base(writer, chunkId, data)
    {
        SubType = subType;
    }
}

public class ListChunk : HeaderChunk
{
    public ListChunk(RiffWriter writer, FourCharacterCode subType, int dataSize)
        : base(writer, FourCharacterCode.LIST, subType, dataSize)
    {
    }

    public ListChunk(RiffWriter writer, FourCharacterCode subType, byte[] data)
        : base(writer, FourCharacterCode.LIST, subType, data)
    {
    }

    public void AddChunk(Chunk chunk)
    {
        if (chunk is null)
            return;
        var data = Data;
        Data = new(Data.Concat(chunk.Prepare()).ToArray());
        data.Dispose();
    }
}

public class FmtChunk : Chunk
{
    public ushort AudioFormat
    {
        get => Binary.ReadU16(Data, 0, Binary.IsBigEndian);
        set => Binary.Write16(Data.Array, 0, Binary.IsBigEndian, value);
    }

    public ushort NumChannels
    {
        get => Binary.ReadU16(Data, 2, Binary.IsBigEndian);
        set => Binary.Write16(Data.Array, 2, Binary.IsBigEndian, value);
    }

    public uint SampleRate
    {
        get => Binary.ReadU32(Data, 4, Binary.IsBigEndian);
        set => Binary.Write32(Data.Array, 4, Binary.IsBigEndian, value);
    }

    public uint ByteRate
    {
        get => Binary.ReadU32(Data, 8, Binary.IsBigEndian);
        set => Binary.Write32(Data.Array, 8, Binary.IsBigEndian, value);
    }

    public ushort BlockAlign
    {
        get => Binary.ReadU16(Data, 12, Binary.IsBigEndian);
        set => Binary.Write16(Data.Array, 12, Binary.IsBigEndian, value);
    }

    public ushort BitsPerSample
    {
        get => Binary.ReadU16(Data, 14, Binary.IsBigEndian);
        set => Binary.Write16(Data.Array, 14, Binary.IsBigEndian, value);
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
    /// <summary>
    /// In bytes
    /// </summary>
    public const int Size = 56;

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
        : base(writer, FourCharacterCode.avih, Size)
    {
        SubType = FourCharacterCode.avih;
    }
}

[Flags]
public enum AviMainHeaderFlags : uint
{
    HasIndex = 0x00000010, // Indicates that the AVI file has an idx1 chunk containing the index at the end of the file
    MustUseIndex = 0x00000020, // Indicates that application should use the index, rather than the physical ordering of the chunks in the file, to determine the order of presentation of the data
    IsInterleaved = 0x00000100, // Indicates that the AVI file is interleaved
    TrustChunkType = 0x00000800, // Indicates that the chunk type should be trusted
    WasCaptureFile = 0x00010000, // Indicates that the AVI file is a specially allocated file used for capturing real-time video
    CopyRighted = 0x00020000  // Indicates that the AVI file is copyrighted
}

public class AviStreamHeader : Chunk
{
    public const int Size = 56;
    public FourCharacterCode StreamType
    {
        get => (FourCharacterCode)Binary.Read32(Data, 0, Binary.IsBigEndian);
        set => Binary.Write32(Data.Array, 0, Binary.IsBigEndian, (int)value);
    }

    public FourCharacterCode HandlerType
    {
        get => (FourCharacterCode)Binary.Read32(Data, 4, Binary.IsBigEndian);
        set => Binary.Write32(Data.Array, 4, Binary.IsBigEndian, (int)value);
    }

    public int SampleRate
    {
        get => Binary.Read32(Data, 8, Binary.IsBigEndian);
        set => Binary.Write32(Data.Array, 8, Binary.IsBigEndian, value);
    }

    public int Start
    {
        get => Binary.Read32(Data, 12, Binary.IsBigEndian);
        set => Binary.Write32(Data.Array, 12, Binary.IsBigEndian, value);
    }

    public int SampleLength
    {
        get => Binary.Read32(Data, 16, Binary.IsBigEndian);
        set => Binary.Write32(Data.Array, 16, Binary.IsBigEndian, value);
    }

    public int SuggestedBufferSize
    {
        get => Binary.Read32(Data, 20, Binary.IsBigEndian);
        set => Binary.Write32(Data.Array, 20, Binary.IsBigEndian, value);
    }

    public int Quality
    {
        get => Binary.Read32(Data, 24, Binary.IsBigEndian);
        set => Binary.Write32(Data.Array, 24, Binary.IsBigEndian, value);
    }

    public int SampleSize
    {
        get => Binary.Read32(Data, 28, Binary.IsBigEndian);
        set => Binary.Write32(Data.Array, 28, Binary.IsBigEndian, value);
    }

    public int FrameRate
    {
        get => Binary.Read32(Data, 32, Binary.IsBigEndian);
        set => Binary.Write32(Data.Array, 32, Binary.IsBigEndian, value);
    }

    public int Scale
    {
        get => Binary.Read32(Data, 36, Binary.IsBigEndian);
        set => Binary.Write32(Data.Array, 36, Binary.IsBigEndian, value);
    }

    public int Rate
    {
        get => Binary.Read32(Data, 40, Binary.IsBigEndian);
        set => Binary.Write32(Data.Array, 40, Binary.IsBigEndian, value);
    }

    public int StartInitialFrames
    {
        get => Binary.Read32(Data, 44, Binary.IsBigEndian);
        set => Binary.Write32(Data.Array, 44, Binary.IsBigEndian, value);
    }

    public int ExtraDataSize
    {
        get => Binary.Read32(Data, 48, Binary.IsBigEndian);
        set => Binary.Write32(Data.Array, 48, Binary.IsBigEndian, value);
    }

    public AviStreamHeader(RiffWriter writer)
        : base(writer, FourCharacterCode.avih, Size)
    {
    }
}

#endregion