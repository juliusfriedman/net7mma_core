using Media.Common;
using Media.Common.Extensions.Linq;
using Media.Container;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//Preview (still)
//using TimeToEntrySample = (int SampleCount, int SampleDurtation)

namespace Media.Containers.BaseMedia;

#region Boxes

public class Mp4Box : Node
{
    // Header size for a box (4 bytes for size + 4 bytes for type)
    internal const int HeaderSize = 8;

    public int Length
    {
        get => Binary.Read32(Identifier, 0, Binary.IsLittleEndian);
        set => Binary.Write32(Identifier, 0, Binary.IsLittleEndian, value);
    }

    //Indicates 8 more bytes follow
    public bool IsExtendedLength => Length == 1;

    //Indicates to end of file
    public bool IndefiniteSize => Length is 0;

    public long ExtendedLength
    {
        get => Binary.Read64(Identifier, 4, Binary.IsLittleEndian);
        set => Binary.Write64(Identifier, 4, Binary.IsLittleEndian, value);
    }

    public int AtomCode
    {
        get => Binary.Read32(Identifier, IsExtendedLength ? 12 : 4, Binary.IsLittleEndian);
        set => Binary.Write32(Identifier, IsExtendedLength ? 12 : 4, Binary.IsLittleEndian, value);
    }

    public string BoxType
    {
        get => Encoding.UTF8.GetString(Identifier, IsExtendedLength ? 12 : 4, 4);
        set => Encoding.UTF8.GetBytes(value).CopyTo(Identifier, IsExtendedLength ? 12 : 4);
    }

    public Mp4Box(BaseMediaWriter writer, byte[] boxType, long dataSize)
        : base(writer, new byte[dataSize > int.MaxValue ? HeaderSize * 2 : HeaderSize], dataSize > int.MaxValue ? 12 : 4, -1, dataSize, true)
    {
        boxType.CopyTo(Identifier, IsExtendedLength ? 12 : 4);
        SetLength();
    }

    public void UpdateSize()
    {
        Master.WriteAt(Offset, Identifier, 0, Binary.BytesPerInteger);
        if (IsExtendedLength)
            Master.WriteAt(Offset, Identifier, Binary.BytesPerInteger, Binary.BytesPerLong);
    }

    public void AddChildBox(Mp4Box box)
    {
        if (box is null)
            throw new ArgumentNullException(nameof(box));

        AddData(box.Identifier.Concat(box.Data));

        return;
    }

    public void AddData(IEnumerable<byte> data)
    {
        var oldData = Data;

        Data = new(Data.Concat(data).ToArray());

        oldData.Dispose();

        SetLength();
    }

    public void SetLength()
    {
        if (DataSize > int.MaxValue)
        {
            Length = 1;
            ExtendedLength = DataSize;
        }
        else
        {
            Length = (int)(IsExtendedLength ? 12 : 8 + DataSize);
        }
    }

    public void AddChildrenBoxes(params Mp4Box[] boxes) => AddChildrenBoxes((IEnumerable<Mp4Box>)boxes);

    public void AddChildrenBoxes(IEnumerable<Mp4Box> boxes)
    {
        foreach (var box in boxes)
            AddChildBox(box);
    }

    public bool HasChild(Mp4Box box)
    {
        if (box is null)
            throw new ArgumentNullException(nameof(box));

        int offset = 0;

        while (offset + HeaderSize <= Data.Count)
        {
            var size = (ulong)Binary.ReadU32(Data, ref offset, Binary.IsLittleEndian);
            var type = Binary.Read32(Data, ref offset, Binary.IsLittleEndian);

            if (size == 1)
                size = Binary.ReadU64(Data, ref offset, Binary.IsLittleEndian);

            if (type == box.AtomCode)
                return true;

            offset += (int)size;
        }

        return false;
    }

    public IEnumerable<Mp4Box> GetChildren()
    {
        int offset = 0;

        while (offset + HeaderSize <= Data.Count)
        {
            var size = (ulong)Binary.ReadU32(Data, ref offset, Binary.IsLittleEndian);
            var type = Binary.Read32(Data, ref offset, Binary.IsLittleEndian);

            if (size == 1)
                size = Binary.ReadU64(Data, ref offset, Binary.IsLittleEndian);

            yield return new Mp4Box(Master as BaseMediaWriter, Binary.GetBytes(type, Binary.IsLittleEndian), (long)size);

            offset += (int)size;
        }
    }
}

public abstract class FullBox : Mp4Box
{
    // Constructor for FullBox, which takes the writer, box type, version, and flags
    protected FullBox(BaseMediaWriter writer, byte[] type, byte version, uint flags, int dataSize = 4)
        : base(writer, type, dataSize)
    {
        Version = version;
        Flags = flags;
    }

    // Version property to get or set the version byte at the beginning of the data
    public byte Version
    {
        get => Data[0];
        set => Data[0] = value;
    }

    // Flags property to get or set the 3-byte flags after the version byte
    public uint Flags
    {
        get => Binary.ReadU24(Data.Array, 1, Binary.IsLittleEndian);
        set => Binary.Write24(Data.Array, 1, Binary.IsLittleEndian, value);
    }

    public const int OffsetToData = 4;
}

public class DataEntryUrlBox : FullBox
{
    //Todo check if this is correct, something with flags indicates if data is self contained
    public string Url => Encoding.UTF8.GetString(Data.Array, OffsetToData + 4, Data.Count - 4);

    public bool DataIsSelfContained => Flags == 1;

    public DataEntryUrlBox(BaseMediaWriter writer, string dataUrl)
        : base(writer, Encoding.UTF8.GetBytes("url "), 0, 1)
    {
        //Get the bytes
        byte[] urlData = Encoding.UTF8.GetBytes(dataUrl);
        //Add the data prefixed by length
        AddData(urlData.Concat(byte.MinValue));
    }
}

public class DrefBox : FullBox
{
    /// <summary>
    /// The amount of child <see cref="DataEntryUrlBox"/> contained.
    /// </summary>
    public int EntryCount
    {
        get => Binary.Read32(Data, OffsetToData, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, OffsetToData, Binary.IsLittleEndian, value);
    }

    public DrefBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("dref"), 0, 0)
    {
        // Set the entry count to 0 initially
        EntryCount = 0;
    }

    public void AddDataReference(string dataUrl)
    {
        // Increment the entry count
        ++EntryCount;

        // Create a Data Entry Url Box
        DataEntryUrlBox dataEntryUrlBox = new DataEntryUrlBox(Master as BaseMediaWriter, dataUrl);

        AddChildBox(dataEntryUrlBox);
    }
}

public class DinfBox : Mp4Box
{
    public DrefBox DrefBox { get; protected set; }

    public DinfBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("dinf"), 0)
    {
        DrefBox = new DrefBox(writer);

        AddChildBox(DrefBox);
    }
    public void AddDataReference(string dataUrl)
    {
        DrefBox.AddDataReference(dataUrl);
    }
}

public class FtypBox : Mp4Box
{
    public uint MajorBrand
    {
        get => (uint)Binary.Read32(Data, 0, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, 0, Binary.IsLittleEndian, value);
    }

    public uint MinorVersion
    {
        get => (uint)Binary.Read32(Data, 4, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, 4, Binary.IsLittleEndian, value);
    }

    public IEnumerable<uint> CompatibleBrands
    {
        get
        {
            var offset = 8;
            while (offset < DataSize)
            {
                yield return (uint)Binary.Read32(Data, ref offset, Binary.IsLittleEndian);
            }
        }
        set
        {
            int offset = 8;
            foreach (var brand in value)
            {
                Binary.Write32(Data.Array, ref offset, Binary.IsLittleEndian, brand);
            }
        }
    }

    public FtypBox(BaseMediaWriter writer, uint majorBrand, uint minorVersion, params uint[] compatibleBrands)
        : base(writer, Encoding.UTF8.GetBytes("ftyp"), 8 + compatibleBrands.Length * Binary.BytesPerInteger)
    {
        MajorBrand = majorBrand;
        MinorVersion = minorVersion;
        CompatibleBrands = compatibleBrands;
    }
}

public class MvhdBox : FullBox
{
    public ulong CreationTime
    {
        get => Version == 1 ? Binary.ReadU64(Data, OffsetToData, Binary.IsLittleEndian) : Binary.ReadU32(Data, OffsetToData, Binary.IsLittleEndian);
        set
        {
            if (Version == 1)
                Binary.Write64(Data.Array, OffsetToData, Binary.IsLittleEndian, value);
            else
                Binary.Write32(Data.Array, OffsetToData, Binary.IsLittleEndian, (uint)value);
        }
    }

    public ulong ModificationTime
    {
        get => Version == 1 ? Binary.ReadU64(Data, OffsetToData + (Version == 1 ? 8 : 4), Binary.IsLittleEndian) : Binary.ReadU32(Data, OffsetToData + (Version == 1 ? 8 : 4), Binary.IsLittleEndian);
        set
        {
            if (Version == 1)
                Binary.Write64(Data.Array, OffsetToData + (Version == 1 ? 8 : 4), Binary.IsLittleEndian, value);
            else
                Binary.Write32(Data.Array, OffsetToData + (Version == 1 ? 8 : 4), Binary.IsLittleEndian, (uint)value);
        }
    }

    public uint TimeScale
    {
        get => Binary.ReadU32(Data, OffsetToData + (Version == 1 ? 16 : 8), Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, OffsetToData + (Version == 1 ? 16 : 8), Binary.IsLittleEndian, value);
    }

    public ulong Duration
    {
        get => Version == 1 ? Binary.ReadU64(Data, OffsetToData + (Version == 1 ? 20 : 12), Binary.IsLittleEndian) : Binary.ReadU32(Data, OffsetToData + (Version == 1 ? 20 : 12), Binary.IsLittleEndian);
        set
        {
            if (Version == 1)
                Binary.Write64(Data.Array, OffsetToData + (Version == 1 ? 20 : 12), Binary.IsLittleEndian, value);
            else
                Binary.Write32(Data.Array, OffsetToData + (Version == 1 ? 20 : 12), Binary.IsLittleEndian, (uint)value);
        }
    }

    public float PreferredRate
    {
        get => Binary.ReadU32(Data, OffsetToData + (Version == 1 ? 28 : 16), Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, OffsetToData + (Version == 1 ? 28 : 16), Binary.IsLittleEndian, (uint)value);
    }

    public ushort PreferredVolume
    {
        get => Binary.ReadU16(Data, OffsetToData + (Version == 1 ? 32 : 20), Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, OffsetToData + (Version == 1 ? 32 : 20), Binary.IsLittleEndian, value);
    }

    //10 bytes Reserved

    public IEnumerable<ushort> Matrix //36 bytes
    {
        get
        {
            var offset = OffsetToData + (Version == 1 ? 34 : 22);
            for (int i = 0; i < 9; i++) //Always 9?
            {
                yield return Binary.ReadU16(Data, ref offset, Binary.IsLittleEndian);
            }
        }
        set
        {
            if (value is null) return;//Todo set all 0?

            var offset = OffsetToData + (Version == 1 ? 34 : 22);

            foreach (var identity in value)
            {
                Binary.Write16(Data.Array, ref offset, Binary.IsLittleEndian, identity);
            }
        }
    }

    public IEnumerable<byte> Predefined //24 bytes
    {
        get
        {
            return Data.Skip(OffsetToData + (Version == 1 ? 52 : 40)).Take(24);
        }
        set
        {
            if (value is null) return;//Todo set all 0?

            var array = value.ToArray();

            if (array.Length != 24)
                throw new ArgumentException("PreDefined must contain 24 elements.");

            Array.Copy(array, 0, Data.Array, OffsetToData + (Version == 1 ? 52 : 40), 24);
        }
    }

    public uint NextTrackId
    {
        get => Binary.ReadU32(Data, OffsetToData + (Version == 1 ? 76 : 64), Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, OffsetToData + (Version == 1 ? 76 : 64), Binary.IsLittleEndian, value);
    }

    public MvhdBox(BaseMediaWriter writer, uint timeScale, ulong duration, uint preferredRate, ushort preferredVolume, ushort[] matrix, byte[] predefined, uint nextTrackID)
        : base(writer, Encoding.UTF8.GetBytes("mvhd"), 0, 0, 4 + 120)
    {
        TimeScale = timeScale;
        Duration = duration;
        PreferredRate = preferredRate;
        PreferredVolume = preferredVolume;
        Matrix = matrix;
        Predefined = predefined;
        NextTrackId = nextTrackID;
    }

    public MvhdBox(BaseMediaWriter writer, byte version, ulong creationTime, ulong modificationTime, uint timeScale, ulong duration, ushort preferredVolume)
        : base(writer, Encoding.UTF8.GetBytes("mvhd"), version, 4 + 120)
    {
        CreationTime = creationTime;
        ModificationTime = modificationTime;
        TimeScale = timeScale;
        Duration = duration;
        PreferredRate = 1.0f;
        PreferredVolume = preferredVolume;
        Matrix = new ushort[]
        {
            0x0001, 0x0000, 0x0000,
            0x0000, 0x0001, 0x0000,
            0x0000, 0x0000, 0x4000
        };
        //Predefined = new byte[24];
        NextTrackId = 1;
    }
}

public class MdhdBox : FullBox
{
    public ulong CreationTime
    {
        get => Version == 1 ? Binary.ReadU64(Data, OffsetToData + 4, Binary.IsLittleEndian) : Binary.ReadU32(Data, OffsetToData + 4, Binary.IsLittleEndian);
        set
        {
            if (Version == 1)
                Binary.Write64(Data.Array, OffsetToData + 4, Binary.IsLittleEndian, value);
            else
                Binary.Write32(Data.Array, OffsetToData + 4, Binary.IsLittleEndian, (uint)value);
        }
    }

    public ulong ModificationTime
    {
        get => Version == 1 ? Binary.ReadU64(Data, OffsetToData + 12, Binary.IsLittleEndian) : Binary.ReadU32(Data, OffsetToData + 8, Binary.IsLittleEndian);
        set
        {
            if (Version == 1)
                Binary.Write64(Data.Array, OffsetToData + 12, Binary.IsLittleEndian, value);
            else
                Binary.Write32(Data.Array, OffsetToData + 8, Binary.IsLittleEndian, (uint)value);
        }
    }

    public uint TimeScale
    {
        get => Binary.ReadU32(Data, OffsetToData + (Version == 1 ? 20 : 12), Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, OffsetToData + (Version == 1 ? 20 : 12), Binary.IsLittleEndian, value);
    }

    public ulong Duration
    {
        get => Version == 1 ? Binary.ReadU64(Data, OffsetToData + 28, Binary.IsLittleEndian) : Binary.ReadU32(Data, OffsetToData + 16, Binary.IsLittleEndian);
        set
        {
            if (Version == 1)
                Binary.Write64(Data.Array, OffsetToData + 28, Binary.IsLittleEndian, value);
            else
                Binary.Write32(Data.Array, OffsetToData + 16, Binary.IsLittleEndian, (uint)value);
        }
    }

    public ushort Language
    {
        get => Binary.ReadU16(Data, OffsetToData + (Version == 1 ? 36 : 20), Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, OffsetToData + (Version == 1 ? 36 : 20), Binary.IsLittleEndian, value);
    }

    public ushort PreDefined
    {
        get => Binary.ReadU16(Data, 15, Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, 15, Binary.IsLittleEndian, value);
    }

    public MdhdBox(BaseMediaWriter writer, byte version, uint creationTime, uint modificationTime, uint timeScale, ulong duration, ushort language)
        : base(writer, Encoding.UTF8.GetBytes("mdhd"), version, 0, 4 + version == 1 ? 44 : 32)
    {
        CreationTime = creationTime;
        ModificationTime = modificationTime;
        TimeScale = timeScale;
        Duration = duration;
        Language = language;
    }
}

public class HdlrBox : FullBox
{
    public HdlrBox(BaseMediaWriter writer, uint handlerType)
        : base(writer, Encoding.UTF8.GetBytes("hdlr"), 0, 0, 24)
    {
        PreDefined = 0;
        HandlerType = handlerType;
        Name = string.Empty;
    }

    public uint PreDefined
    {
        get => Binary.ReadU32(Data, 4, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, 4, Binary.IsLittleEndian, value);
    }

    public uint HandlerType
    {
        get => Binary.ReadU32(Data, 8, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, 8, Binary.IsLittleEndian, value);
    }

    public string Name
    {
        get => Encoding.UTF8.GetString(Data.Array, 24, (int)(DataSize - 24));
        set
        {
            byte[] nameData = Encoding.UTF8.GetBytes(value);
            byte[] newData = new byte[24 + nameData.Length];
            Array.Copy(Data.Array, newData, 24);//Copy 24 bytes
            nameData.CopyTo(newData, 24);//Copy nameData to newData starting @ 24
            var data = Data;
            Data = new(newData);
            data.Dispose();
        }
    }
}

public class MinfBox : FullBox
{
    public MinfBox(BaseMediaWriter writer, params Mp4Box[] children)
        : base(writer, Encoding.UTF8.GetBytes("minf"), 0, 0)
    {
        if (children is not null)
        {
            foreach (var child in children)
            {
                AddChildBox(child);
            }
        }
    }
}

public class MdiaBox : FullBox
{
    public MdhdBox MdhdBox { get; }
    public HdlrBox HdlrBox { get; }
    public MinfBox MinfBox { get; }

    public MdiaBox(BaseMediaWriter writer, MdhdBox mdhdBox, HdlrBox hdlrBox, MinfBox minfBox)
        : base(writer, Encoding.UTF8.GetBytes("mdia"), 0, 0)
    {
        MdhdBox = mdhdBox;
        HdlrBox = hdlrBox;
        MinfBox = minfBox;

        AddChildBox(MdhdBox);
        AddChildBox(HdlrBox);
        AddChildBox(MinfBox);
    }
}

public class TrakBox : FullBox
{
    public MdiaBox MdiaBox { get; }

    public TrakBox(BaseMediaWriter writer, MdiaBox mdiaBox)
        : base(writer, Encoding.ASCII.GetBytes("trak"), 0, 0)
    {
        MdiaBox = mdiaBox;
        AddChildBox(MdiaBox);
    }
}

public class StblBox : FullBox
{
    public StsdBox StsdBox { get; protected set; }
    public SttsBox SttsBox { get; protected set; }
    public StszBox StszBox { get; protected set; }
    public StcoBox StcoBox { get; protected set; }

    public StblBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("stbl"), 0, 0)
    {
        StsdBox = new StsdBox(writer);
        SttsBox = new SttsBox(writer);
        StszBox = new StszBox(writer);
        StcoBox = new StcoBox(writer);

        AddChildBox(StsdBox);
        AddChildBox(SttsBox);
        AddChildBox(StszBox);
        AddChildBox(StcoBox);
    }

    public void AddSampleDescriptionBox(Mp4Box sampleEntry) => StsdBox.AddSampleEntry(sampleEntry);

    public void AddTimeToSampleEntry(int sampleCount, int sampleDuration) => SttsBox.AddTimeToSampleEntry(sampleCount, sampleDuration);

    public void AddSampleSize(int size) => StszBox.AddSampleSize(size);

    public void AddChunkOffset(uint offset) => StcoBox.AddChunkOffset(offset);
}

public class StcoBox : FullBox
{
    public uint EntryCount
    {
        get => Binary.ReadU32(Data, OffsetToData, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, OffsetToData, Binary.IsLittleEndian, value);
    }

    public IEnumerable<uint> ChunkOffsets
    {
        get
        {
            var offset = 8;
            while (offset < DataSize)
            {
                yield return Binary.ReadU32(Data, ref offset, Binary.IsLittleEndian);
            }
        }
    }

    public StcoBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("stco"), 0, 0)
    {
    }

    public void AddChunkOffset(uint offset)
    {
        AddData(Binary.GetBytes(offset, Binary.IsLittleEndian));
        ++EntryCount;
    }
}

public class St64Box : FullBox
{
    public uint EntryCount
    {
        get => Binary.ReadU32(Data, OffsetToData, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, OffsetToData, Binary.IsLittleEndian, value);
    }

    public IEnumerable<ulong> ChunkOffsets
    {
        get
        {
            var offset = 8;
            while (offset < DataSize)
            {
                yield return Binary.ReadU64(Data, ref offset, Binary.IsLittleEndian);
            }
        }
    }

    public St64Box(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("stco"), 0, 0)
    {
    }

    public void AddChunkOffset(ulong offset)
    {
        AddData(Binary.GetBytes(offset, Binary.IsLittleEndian));
        ++EntryCount;
    }
}

public class StsdBox : FullBox
{
    public int EntryCount
    {
        get => Binary.Read32(Data, OffsetToData, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, OffsetToData, Binary.IsLittleEndian, value);
    }

    public StsdBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("stsd"), 0, 0)
    {
        // Set the entry count to 0 initially
        //EntryCount = 0;
    }

    public void AddSampleEntry(Mp4Box sampleEntry)
    {
        // Increment the entry count
        ++EntryCount;

        AddChildBox(sampleEntry);
    }
}

public class StszBox : FullBox
{
    public int SampleSize
    {
        get => Binary.Read32(Data, 4, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, 4, Binary.IsLittleEndian, value);
    }

    public int SampleCount
    {
        get => Binary.Read32(Data, 8, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, 8, Binary.IsLittleEndian, value);
    }

    public IEnumerable<int> SampleSizes
    {
        get
        {
            var offset = 12;
            while (offset < DataSize)
            {
                yield return Binary.Read32(Data, ref offset, Binary.IsLittleEndian);
            }
        }
    }

    public StszBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("stsz"), 0, 0, 4 + 8)
    {
    }

    public void AddSampleSize(int size)
    {
        ++SampleCount;
        AddData(Binary.GetBytes(size, Binary.IsLittleEndian));
    }
}

/// <summary>
/// Sync Sample Box
/// </summary>
public class SttsBox : FullBox
{
    public int EntryCount
    {
        get => Binary.Read32(Data, OffsetToData, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, OffsetToData, Binary.IsLittleEndian, value);
    }

    public IEnumerable<(int SampleCount, int SampleDurtation)> TimeToSampleEntries
    {
        get
        {
            var offset = 8;
            //var index = 0;
            while (offset + 8 <= DataSize /*&& index < EntryCount*/)
            {
                var sampleCount = Binary.Read32(Data, ref offset, Binary.IsLittleEndian);
                var sampleDuration = Binary.Read32(Data, ref offset, Binary.IsLittleEndian);
                yield return (sampleCount, sampleDuration);
                //++index;
            }
        }
    }

    public SttsBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("stts"), 0, 4 + 4)
    {
    }

    public void AddTimeToSampleEntry(int sampleCount, int sampleDuration)
    {
        ++EntryCount;
        AddData(Binary.GetBytes(sampleCount, Binary.IsLittleEndian)
                   .Concat(Binary.GetBytes(sampleDuration, Binary.IsLittleEndian)));
    }
}

#region Fragmenting Boxes

public class TfhdBox : FullBox
{
    public uint TrackId
    {
        get => Binary.ReadU32(Data, OffsetToData, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, OffsetToData, Binary.IsLittleEndian, value);
    }

    public uint SampleDescriptionIndex
    {
        get => Binary.ReadU32(Data, 8, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, 8, Binary.IsLittleEndian, value);
    }

    public uint DefaultSampleDuration
    {
        get => Binary.ReadU32(Data, 12, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, 12, Binary.IsLittleEndian, value);
    }

    public uint DefaultSampleSize
    {
        get => Binary.ReadU32(Data, 16, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, 16, Binary.IsLittleEndian, value);
    }

    public uint DefaultSampleFlags
    {
        get => Binary.ReadU32(Data, 20, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, 20, Binary.IsLittleEndian, value);
    }

    public TfhdBox(BaseMediaWriter writer, uint trackId, uint defaultSampleDuration, uint defaultSampleSize, uint defaultSampleFlags)
        : base(writer, Encoding.UTF8.GetBytes("tfhd"), 0, 0)
    {
        TrackId = trackId;
        DefaultSampleDuration = defaultSampleDuration;
        DefaultSampleSize = defaultSampleSize;
        DefaultSampleFlags = defaultSampleFlags;
    }
}

public class TfdtBox : FullBox
{
    public ulong BaseMediaDecodeTime
    {
        get => (Version == 1) ? Binary.ReadU64(Data, OffsetToData, Binary.IsLittleEndian) : Binary.ReadU32(Data, OffsetToData, Binary.IsLittleEndian);
        set
        {
            if (Version == 1)
                Binary.Write64(Data.Array, OffsetToData, Binary.IsLittleEndian, value);
            else
                Binary.Write32(Data.Array, OffsetToData, Binary.IsLittleEndian, (uint)value);
        }
    }

    public TfdtBox(BaseMediaWriter writer, ulong baseMediaDecodeTime)
        : base(writer, Encoding.ASCII.GetBytes("tfdt"), 0, 0)
    {
        BaseMediaDecodeTime = baseMediaDecodeTime;
    }
}

public class TfraBox : FullBox
{
    public uint TrackId
    {
        get => Binary.ReadU32(Data, OffsetToData, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, OffsetToData, Binary.IsLittleEndian, value);
    }

    public ushort LengthSizeOfTrafNum
    {
        get => (ushort)(Binary.ReadU8(Data, OffsetToData + 4, Binary.IsLittleEndian) >> 4);
        set => Binary.WriteBits(Data.Array, Binary.BytesToBits(OffsetToData + 4), 4, (byte)(value << 4), Binary.IsLittleEndian);
    }

    public ushort LengthSizeOfTrunNum
    {
        get => (ushort)((Binary.ReadU8(Data, OffsetToData + 4, Binary.IsLittleEndian) >> 2) & 0x03);
        set => Binary.WriteBits(Data.Array, Binary.BytesToBits(OffsetToData + 4), 2, (byte)(value << 2), Binary.IsLittleEndian);
    }

    public ushort LengthSizeOfSampleNum
    {
        get => (ushort)(Binary.ReadU8(Data, OffsetToData + 4, Binary.IsLittleEndian) & 0x03);
        set => Binary.WriteBits(Data.Array, Binary.BytesToBits(OffsetToData + 4), 2, (byte)value, Binary.IsLittleEndian);
    }

    public uint EntryCount
    {
        get => Binary.ReadU32(Data, OffsetToData + 6, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, OffsetToData + 6, Binary.IsLittleEndian, value);
    }

    public IEnumerable<TrackFragmentRandomAccessEntryBox> Entries
    {
        get
        {
            var offset = OffsetToData + 10;
            while (offset < DataSize)
            {
                //This allocated for the data already
                var tfra = new TrackFragmentRandomAccessEntryBox(Master as BaseMediaWriter, Version);

                //Reassign rather than copy
                tfra.Data = new MemorySegment(Data.Array, offset, tfra.Length);

                yield return tfra;

                offset += tfra.Length;
            }
        }
    }

    public TfraBox(BaseMediaWriter writer, uint trackId, ushort lengthSizeOfTrafNum, ushort lengthSizeOfTrunNum, ushort lengthSizeOfSampleNum, byte version = 1, uint flags = 0)
        : base(writer, Encoding.UTF8.GetBytes("tfra"), version, flags)
    {
        TrackId = trackId;
        LengthSizeOfTrafNum = lengthSizeOfTrafNum;
        LengthSizeOfTrunNum = lengthSizeOfTrunNum;
        LengthSizeOfSampleNum = lengthSizeOfSampleNum;
    }

    public void AddEntry(TrackFragmentRandomAccessEntryBox entry)
    {
        EntryCount++;
        AddChildBox(entry);
    }
}

public class TrackFragmentRandomAccessEntryBox : FullBox
{
    public ulong Time
    {
        get => Version is 0 ? Binary.ReadU32(Data, OffsetToData, Binary.IsLittleEndian) : Binary.ReadU64(Data, OffsetToData, Binary.IsLittleEndian);
        set
        {
            if (Version is 0)
                Binary.Write32(Data.Array, OffsetToData, Binary.IsLittleEndian, (uint)value);
            else
                Binary.Write64(Data.Array, OffsetToData, Binary.IsLittleEndian, value);
        }
    }

    public uint MoofOffset
    {
        get => Binary.ReadU32(Data, OffsetToData + (Version is 0 ? 4 : 8), Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, OffsetToData + (Version is 0 ? 4 : 8), Binary.IsLittleEndian, value);
    }

    public uint TrafNumber
    {
        get => Binary.ReadU32(Data, OffsetToData + (Version is 0 ? 8 : 12), Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, OffsetToData + (Version is 0 ? 8 : 12), Binary.IsLittleEndian, value);
    }

    public uint TrunNumber
    {
        get => Binary.ReadU32(Data, OffsetToData + (Version is 0 ? 12 : 16), Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, OffsetToData + (Version is 0 ? 12 : 16), Binary.IsLittleEndian, value);
    }

    public uint SampleNumber
    {
        get => Binary.ReadU32(Data, OffsetToData + (Version is 0 ? 16 : 20), Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, OffsetToData + (Version is 0 ? 16 : 20), Binary.IsLittleEndian, value);
    }

    public TrackFragmentRandomAccessEntryBox(BaseMediaWriter writer, byte version)
        : base(writer, Encoding.UTF8.GetBytes("tfra"), version, 0, version is 0 ? 20 : 24)
    {
    }
}

public class TrexBox : FullBox
{
    public uint TrackID
    {
        get => Binary.ReadU32(Data, OffsetToData, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, OffsetToData, Binary.IsLittleEndian, value);
    }

    public uint DefaultSampleDescriptionIndex
    {
        get => Binary.ReadU32(Data, 8, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, 8, Binary.IsLittleEndian, value);
    }

    public uint DefaultSampleDuration
    {
        get => Binary.ReadU32(Data, 12, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, 12, Binary.IsLittleEndian, value);
    }

    public uint DefaultSampleSize
    {
        get => Binary.ReadU32(Data, 16, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, 16, Binary.IsLittleEndian, value);
    }

    public uint DefaultSampleFlags
    {
        get => Binary.ReadU32(Data, 20, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, 20, Binary.IsLittleEndian, value);
    }

    public TrexBox(BaseMediaWriter writer, uint trackID, uint defaultSampleDescriptionIndex, uint defaultSampleDuration, uint defaultSampleSize, uint defaultSampleFlags)
        : base(writer, Encoding.UTF8.GetBytes("trex"), 0, 0, 24)
    {
        TrackID = trackID;
        DefaultSampleDescriptionIndex = defaultSampleDescriptionIndex;
        DefaultSampleDuration = defaultSampleDuration;
        DefaultSampleSize = defaultSampleSize;
        DefaultSampleFlags = defaultSampleFlags;
    }
}

public class TrunBox : FullBox
{
    public uint SampleCount
    {
        get => Binary.ReadU32(Data, OffsetToData, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, OffsetToData, Binary.IsLittleEndian, value);
    }

    public ulong TrackDataOffset
    {
        get => (Version is 0) ? Binary.ReadU32(Data, 8, Binary.IsLittleEndian) : Binary.ReadU64(Data, 8, Binary.IsLittleEndian);
        set
        {
            if (Version is 0)
                Binary.Write32(Data.Array, 8, Binary.IsLittleEndian, (uint)value);
            else
                Binary.Write64(Data.Array, 8, Binary.IsLittleEndian, value);
        }
    }

    public uint FirstSampleFlags
    {
        get => (Version is 0) ? 0 : Binary.ReadU32(Data, 16, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, 16, Binary.IsLittleEndian, value);
    }

    public IEnumerable<uint> SampleFlags
    {
        get
        {
            var offset = (Version is 0) ? 12 : 20;
            for (int i = 0; i < SampleCount; i++)
            {
                yield return Binary.ReadU32(Data, ref offset, Binary.IsLittleEndian);
            }
        }
        set
        {
            var offset = (Version is 0) ? 12 : 20;

            foreach (var flag in value)
            {
                Binary.Write32(Data.Array, ref offset, Binary.IsLittleEndian, flag);
            }
        }
    }

    public TrunBox(BaseMediaWriter writer, uint sampleCount, uint dataOffset, uint firstSampleFlags)
        : base(writer, Encoding.UTF8.GetBytes("trun"), 0, 0)
    {
        SampleCount = sampleCount;
        DataOffset = dataOffset;
        FirstSampleFlags = firstSampleFlags;
    }
}

public class TrafBox : FullBox
{
    public TrafBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("traf"), 0, 0)
    {
    }

    public void AddTrackFragment(Track track, int trackId)
    {
        var trunEntryCount = track.SampleCount;
        var tfhdFlags = 0x00020000; // Default-base-is-moof flag

        // Create a tfhd box
        var tfhd = new TfhdBox(Master as BaseMediaWriter, (uint)trackId, (uint)tfhdFlags, 0, 0);
        AddChildBox(tfhd);

        // Create a tfdt box
        var tfdt = new TfdtBox(Master as BaseMediaWriter, (ulong)(track.Duration.Ticks / 10));
        AddChildBox(tfdt);

        // Create a trun box
        var trun = new TrunBox(Master as BaseMediaWriter, (uint)trunEntryCount, 0, 0);

        //trun.SampleSizes = track.SampleSizes;
        //trun.SampleFlags = track.SampleFlags;

        AddChildBox(trun);
    }
}

public class MoofBox : FullBox
{
    public MoofBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("moof"), 0, 0)
    {
    }

    public void AddTrackFragment(Track track, int trackId)
    {
        var traf = new TrafBox(Master as BaseMediaWriter);
        traf.AddTrackFragment(track, trackId);
        AddChildBox(traf);
    }
}

public class MfhdBox : FullBox
{
    public uint SequenceNumber
    {
        get => Binary.ReadU32(Data, 4, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, 4, Binary.IsLittleEndian, value);
    }

    public MfhdBox(BaseMediaWriter writer, uint sequenceNumber)
        : base(writer, Encoding.UTF8.GetBytes("mfhd"), 0, 0)
    {
        SequenceNumber = sequenceNumber;
    }
}

public class MvexBox : Mp4Box
{
    public MvexBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("mvex"), 0)
    {
    }

    public void AddTrexBox(uint trackId, uint defaultSampleDescriptionIndex, uint defaultSampleDuration, uint defaultSampleSize, uint defaultSampleFlags)
    {
        TrexBox trexBox = new TrexBox(Master as BaseMediaWriter, trackId, defaultSampleDescriptionIndex, defaultSampleDuration, defaultSampleSize, defaultSampleFlags);
        AddChildBox(trexBox);
    }
}

#endregion

public class TkhdBox : FullBox
{
    public ulong CreationTime
    {
        get => Version == 1 ? Binary.ReadU64(Data, 4, Binary.IsLittleEndian) : Binary.ReadU32(Data, 4, Binary.IsLittleEndian);
        set
        {
            if (Version == 1)
                Binary.Write64(Data.Array, 4, Binary.IsLittleEndian, value);
            else
                Binary.Write32(Data.Array, 4, Binary.IsLittleEndian, (uint)value);
        }
    }

    public ulong ModificationTime
    {
        get => Version == 1 ? Binary.ReadU64(Data, 12, Binary.IsLittleEndian) : Binary.ReadU32(Data, 8, Binary.IsLittleEndian);
        set
        {
            if (Version == 1)
                Binary.Write64(Data.Array, 12, Binary.IsLittleEndian, value);
            else
                Binary.Write32(Data.Array, 8, Binary.IsLittleEndian, (uint)value);
        }
    }

    public uint TrackId
    {
        get => Version == 1 ? Binary.ReadU32(Data, 20, Binary.IsLittleEndian) : Binary.ReadU32(Data, 12, Binary.IsLittleEndian);
        set
        {
            if (Version == 1)
                Binary.Write32(Data.Array, 20, Binary.IsLittleEndian, value);
            else
                Binary.Write32(Data.Array, 12, Binary.IsLittleEndian, value);
        }
    }

    public uint Reserved1
    {
        get => Version == 1 ? Binary.ReadU32(Data, 24, Binary.IsLittleEndian) : Binary.ReadU32(Data, 16, Binary.IsLittleEndian);
        set
        {
            if (Version == 1)
                Binary.Write32(Data.Array, 24, Binary.IsLittleEndian, value);
            else
                Binary.Write32(Data.Array, 16, Binary.IsLittleEndian, value);
        }
    }

    public ulong Duration
    {
        get => Version == 1 ? Binary.ReadU64(Data, 28, Binary.IsLittleEndian) : Binary.ReadU32(Data, 20, Binary.IsLittleEndian);
        set
        {
            if (Version == 1)
                Binary.Write64(Data.Array, 28, Binary.IsLittleEndian, value);
            else
                Binary.Write32(Data.Array, 20, Binary.IsLittleEndian, (uint)value);
        }
    }

    public uint Reserved2
    {
        get => Version == 1 ? Binary.ReadU32(Data, 36, Binary.IsLittleEndian) : Binary.ReadU32(Data, 24, Binary.IsLittleEndian);
        set
        {
            if (Version == 1)
                Binary.Write32(Data.Array, 36, Binary.IsLittleEndian, value);
            else
                Binary.Write32(Data.Array, 24, Binary.IsLittleEndian, value);
        }
    }

    public ushort Layer
    {
        get => Version == 1 ? Binary.ReadU16(Data, 40, Binary.IsLittleEndian) : Binary.ReadU16(Data, 26, Binary.IsLittleEndian);
        set
        {
            if (Version == 1)
                Binary.Write16(Data.Array, 40, Binary.IsLittleEndian, value);
            else
                Binary.Write16(Data.Array, 26, Binary.IsLittleEndian, value);
        }
    }

    public ushort AlternateGroup
    {
        get => Version == 1 ? Binary.ReadU16(Data, 42, Binary.IsLittleEndian) : Binary.ReadU16(Data, 28, Binary.IsLittleEndian);
        set
        {
            if (Version == 1)
                Binary.Write16(Data.Array, 42, Binary.IsLittleEndian, value);
            else
                Binary.Write16(Data.Array, 28, Binary.IsLittleEndian, value);
        }
    }

    public ushort Volume
    {
        get => Version == 1 ? Binary.ReadU16(Data, 44, Binary.IsLittleEndian) : Binary.ReadU16(Data, 30, Binary.IsLittleEndian);
        set
        {
            if (Version == 1)
                Binary.Write16(Data.Array, 44, Binary.IsLittleEndian, value);
            else
                Binary.Write16(Data.Array, 30, Binary.IsLittleEndian, value);
        }
    }

    public ushort Reserved3
    {
        get => Version == 1 ? Binary.ReadU16(Data, 46, Binary.IsLittleEndian) : Binary.ReadU16(Data, 32, Binary.IsLittleEndian);
        set
        {
            if (Version == 1)
                Binary.Write16(Data.Array, 46, Binary.IsLittleEndian, value);
            else
                Binary.Write16(Data.Array, 32, Binary.IsLittleEndian, value);
        }
    }

    public IEnumerable<ushort> Matrix
    {
        get
        {
            int offset = Version == 1 ? 48 : 34;
            for (int i = 0; i < 9; i++) //Always 9?
            {
                yield return Binary.ReadU16(Data, ref offset, Binary.IsLittleEndian);
            }
        }
        set
        {
            int offset = Version == 1 ? 48 : 34;

            foreach (var identity in value)
            {
                Binary.Write16(Data.Array, ref offset, Binary.IsLittleEndian, identity);
            }
        }
    }

    public uint Width
    {
        get => Version == 1 ? Binary.ReadU32(Data, 84, Binary.IsLittleEndian) : Binary.ReadU32(Data, 56, Binary.IsLittleEndian);
        set
        {
            if (Version == 1)
                Binary.Write32(Data.Array, 84, Binary.IsLittleEndian, value);
            else
                Binary.Write32(Data.Array, 56, Binary.IsLittleEndian, value);
        }
    }

    public uint Height
    {
        get => Version == 1 ? Binary.ReadU32(Data, 88, Binary.IsLittleEndian) : Binary.ReadU32(Data, 60, Binary.IsLittleEndian);
        set
        {
            if (Version == 1)
                Binary.Write32(Data.Array, 88, Binary.IsLittleEndian, value);
            else
                Binary.Write32(Data.Array, 60, Binary.IsLittleEndian, value);
        }
    }

    public TkhdBox(BaseMediaWriter writer, ushort version, uint flags)
        : base(writer, Encoding.ASCII.GetBytes("tkhd"), (byte)version, flags)
    {
        if (version is 0)
            Data = new(new byte[84]);
        else if (version == 1)
            Data = new(new byte[92]);
        else
            throw new ArgumentException("Invalid version. Version must be 0 or 1.");

        Version = (byte)version;
        Flags = flags;
        CreationTime = 0;
        ModificationTime = 0;
        TrackId = 0;
        Reserved1 = 0;
        Duration = 0;
        Reserved2 = 0;
        Layer = 0;
        AlternateGroup = 0;
        Volume = 0;
        Reserved3 = 0;
        Matrix = new ushort[9] { 0x0100, 0, 0, 0, 0x0100, 0, 0, 0, 0x4000 };
        Width = 0;
        Height = 0;
    }
}

public class MdatBox : Mp4Box
{
    public MdatBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("mdat"), 0)
    {
    }

    public void AddSampleData(byte[] data)
    {
        AddData(data);
        SetLength();
    }
}

public class UdtaBox : Mp4Box
{
    public UdtaBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("udta"), 0)
    {
        // Initialize and add any user data related boxes here
    }
}

public class UuidBox : Mp4Box
{
    public UuidBox(BaseMediaWriter writer, Guid uuid, byte[] userData)
        : base(writer, Encoding.UTF8.GetBytes("uuid"), userData.Length + 16)
    {
        // Write the UUID
        var data = Data;
        Data = new(Data.Concat(uuid.ToByteArray()).Concat(userData).ToArray());
        data.Dispose();
    }

    public void AddUserMetadata(string key, string value)
    {
        var userMetadataBox = new UserMetadataBox(Master as BaseMediaWriter, key, value);
        AddChildBox(userMetadataBox);
    }

    public void AddUserdata(string key, string value)
    {
        var userdataBox = new UserDataBox(Master as BaseMediaWriter, key, value);
        AddChildBox(userdataBox);
    }
}

public abstract class KeyValueBox : FullBox
{
    public string Key
    {
        get => Encoding.UTF8.GetString(Data.Array, OffsetToData, Length);
        //Todo when setting value may be larger than allocated
        protected set => Encoding.UTF8.GetBytes(value).CopyTo(Data.Array, OffsetToData);
    }

    public string Value
    {
        get => Encoding.UTF8.GetString(Data.Array, OffsetToData + Key.Length + 1, Length - Key.Length - 1);
        //Todo when setting value may be larger than allocated
        protected set => Encoding.UTF8.GetBytes(value).CopyTo(Data.Array, OffsetToData + Key.Length + 1);
    }

    protected KeyValueBox(BaseMediaWriter writer, string type, string key, string value)
        : base(writer, Encoding.UTF8.GetBytes(type), 0, 0, key.Length + value.Length + 2)
    {
        Key = key;
        Value = value;
    }
}

public class UserDataBox : KeyValueBox
{
    public UserDataBox(BaseMediaWriter writer, string key, string value)
        : base(writer, "udta", key, value)
    {
    }
}

public class UserMetadataBox : KeyValueBox
{
    public UserMetadataBox(BaseMediaWriter writer, string key, string value)
        : base(writer, "meta", key, value)
    {
    }
}

public class MoovBox : Mp4Box
{
    public MvhdBox MovieHeaderBox { get; }
    public List<TrakBox> Tracks { get; } = new List<TrakBox>();
    public UdtaBox UserDataBox { get; }

    public MoovBox(BaseMediaWriter writer, uint timeScale, uint duration, uint preferredRate, ushort preferredVolume, ushort[] matrix, byte[] predefined, uint nextTrackId)
        : base(writer, Encoding.UTF8.GetBytes("moov"), 0)
    {
        MovieHeaderBox = new MvhdBox(writer, timeScale, duration, preferredRate, preferredVolume, matrix, predefined, nextTrackId);
        UserDataBox = new UdtaBox(writer);

        AddChildBox(MovieHeaderBox);
        AddChildrenBoxes(Tracks);
        AddChildBox(UserDataBox);
    }

    public void AddTrack(TrakBox trackBox)
    {
        Tracks.Add(trackBox);
        AddChildBox(trackBox);
    }
}

public class AvcCBox : Mp4Box
{
    public AvcCBox(BaseMediaWriter writer, byte[] avcCData)
        : base(writer, Encoding.UTF8.GetBytes("avcC"), avcCData.Length)
    {
        Data = new(avcCData);
    }
}

public class Avc1Box : Mp4Box
{
    public Avc1Box(BaseMediaWriter writer, byte[] avcCData)
        : base(writer, Encoding.UTF8.GetBytes("avc1"), 0)
    {
        // Create the AVC Configuration Box (avcC) using the provided data
        AvcCBox avcCBox = new AvcCBox(writer, avcCData);

        AddChildBox(avcCBox);
    }
}

public class BtrtBox : FullBox
{
    public uint BufferSizeDB
    {
        get => Binary.ReadU32(Data, OffsetToData, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, OffsetToData, Binary.IsLittleEndian, value);
    }

    public uint MaxBitrate
    {
        get => Binary.ReadU32(Data, 8, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, 8, Binary.IsLittleEndian, value);
    }

    public uint AvgBitrate
    {
        get => Binary.ReadU32(Data, 12, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, 12, Binary.IsLittleEndian, value);
    }

    public BtrtBox(BaseMediaWriter writer, uint bufferSizeDB, uint maxBitrate, uint avgBitrate)
        : base(writer, Encoding.UTF8.GetBytes("btrt"), 0, 0, 12)
    {
        BufferSizeDB = bufferSizeDB;
        MaxBitrate = maxBitrate;
        AvgBitrate = avgBitrate;
    }
}

public class VmhdBox : FullBox
{
    public ushort GraphicsMode
    {
        get => Binary.ReadU16(Data, OffsetToData, Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, OffsetToData, Binary.IsLittleEndian, value);
    }

    public IEnumerable<ushort> OpColor
    {
        get
        {
            int offset = OffsetToData + 2;
            for (int i = 0; i < 3; i++) //Todo, if more than 3 should return based on offset...
                yield return Binary.ReadU16(Data, ref offset, Binary.IsLittleEndian);
        }
        set
        {
            //Todo , verify only 3 in value.Count()
            int offset = OffsetToData + 2;
            foreach (var identity in value)
                Binary.Write16(Data.Array, ref offset, Binary.IsLittleEndian, identity);
        }
    }

    public VmhdBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("vmhd"), 0, 1, 18)
    {
        // Initialize graphics mode and opcolor
        //GraphicsMode = 0;
        //OpColor = new ushort[] { 0, 0, 0 };
    }
}

public abstract class SampleEntryBox : FullBox
{
    public ushort DataReferenceIndex
    {
        get => Binary.ReadU16(Data, OffsetToData, Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, OffsetToData, Binary.IsLittleEndian, value);
    }

    public SampleEntryBox(BaseMediaWriter writer, string type, int dataSize)
        : base(writer, Encoding.UTF8.GetBytes(type), 0, 0, dataSize)
    {
        DataReferenceIndex = 1; // Default data reference index
    }
}

public class VisualSampleEntryBox : SampleEntryBox
{
    public ushort PreDefined1
    {
        get => Binary.ReadU16(Data, OffsetToData + 6, Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, OffsetToData + 6, Binary.IsLittleEndian, value);
    }

    public ushort Reserved1
    {
        get => Binary.ReadU16(Data, OffsetToData + 8, Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, OffsetToData + 8, Binary.IsLittleEndian, value);
    }

    public IEnumerable<uint> PreDefined2
    {
        get
        {
            for (int i = 0; i < 3; i++)
            {
                yield return Binary.ReadU32(Data, OffsetToData + 10 + i * 4, Binary.IsLittleEndian);
            }
        }
        set
        {
            //if (value.Count() != 3)
            //throw new ArgumentException("PreDefined2 must contain 3 elements.");

            int i = 0;
            foreach (var val in value)
            {
                Binary.Write32(Data.Array, OffsetToData + 10 + i++ * 4, Binary.IsLittleEndian, val);
            }
        }
    }

    public ushort Width
    {
        get => Binary.ReadU16(Data, OffsetToData + 22, Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, OffsetToData + 22, Binary.IsLittleEndian, value);
    }

    public ushort Height
    {
        get => Binary.ReadU16(Data, OffsetToData + 24, Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, OffsetToData + 24, Binary.IsLittleEndian, value);
    }

    public ushort HorizResolution
    {
        get => Binary.ReadU16(Data, OffsetToData + 26, Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, OffsetToData + 26, Binary.IsLittleEndian, value);
    }

    public ushort VertResolution
    {
        get => Binary.ReadU16(Data, OffsetToData + 28, Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, OffsetToData + 28, Binary.IsLittleEndian, value);
    }

    public uint Reserved2
    {
        get => Binary.ReadU32(Data, OffsetToData + 30, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, OffsetToData + 30, Binary.IsLittleEndian, value);
    }

    public ushort FrameCount
    {
        get => Binary.ReadU16(Data, OffsetToData + 34, Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, OffsetToData + 34, Binary.IsLittleEndian, value);
    }

    // Add more properties specific to visual sample entries if needed

    public VisualSampleEntryBox(BaseMediaWriter writer, string type)
        : base(writer, type, 38)
    {
        // Set default values or initialize properties as needed
    }
}

public class AudioSampleEntryBox : SampleEntryBox
{
    public ushort EntryVersion
    {
        get => Binary.ReadU16(Data, OffsetToData + 6, Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, OffsetToData + 6, Binary.IsLittleEndian, value);
    }

    public ushort ChannelCount
    {
        get => Binary.ReadU16(Data, OffsetToData + 8, Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, OffsetToData + 8, Binary.IsLittleEndian, value);
    }

    public ushort SampleSize
    {
        get => Binary.ReadU16(Data, OffsetToData + 10, Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, OffsetToData + 10, Binary.IsLittleEndian, value);
    }

    public ushort CompressionId
    {
        get => Binary.ReadU16(Data, OffsetToData + 12, Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, OffsetToData + 12, Binary.IsLittleEndian, value);
    }

    public ushort PacketSize
    {
        get => Binary.ReadU16(Data, OffsetToData + 14, Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, OffsetToData + 14, Binary.IsLittleEndian, value);
    }

    public uint SampleRate
    {
        get => Binary.ReadU32(Data, OffsetToData + 16, Binary.IsLittleEndian);
        set => Binary.Write32(Data.Array, OffsetToData + 16, Binary.IsLittleEndian, value);
    }

    public AudioSampleEntryBox(BaseMediaWriter writer, string format, int dataSize)
        : base(writer, format, dataSize)
    {
        // Initialize any specific properties for audio sample entries
    }
}

public class Mp4aBox : AudioSampleEntryBox
{
    public ushort AudioCodec
    {
        get => Binary.ReadU16(Data, OffsetToData + 28, Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, OffsetToData + 28, Binary.IsLittleEndian, value);
    }

    public ushort AudioChannels
    {
        get => Binary.ReadU16(Data, OffsetToData + 30, Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, OffsetToData + 30, Binary.IsLittleEndian, value);
    }

    public ushort AudioSampleSize
    {
        get => Binary.ReadU16(Data, OffsetToData + 32, Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, OffsetToData + 32, Binary.IsLittleEndian, value);
    }
    public Mp4aBox(BaseMediaWriter writer)
        : base(writer, "mp4a", 36)
    {
        // Initialize any specific properties for mp4a sample entries
    }
}

#endregion