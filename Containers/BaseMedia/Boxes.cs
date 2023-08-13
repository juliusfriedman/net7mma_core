using Media.Common;
using Media.Common.Extensions.Linq;
using Media.Container;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;
//Preview
//using TimeToEntrySample = (int SampleCount, int SampleDurtation)

namespace Media.Containers.BaseMedia;

#region Boxes

public class Mp4Box : Node
{
    // Header size for a box (4 bytes for size + 4 bytes for type)
    internal const int HeaderSize = 8;

    public int Length
    {
        get => Binary.Read32(Identifier, 0, Binary.IsBigEndian);
        set => Binary.Write32(Identifier, 0, Binary.IsBigEndian, value);
    }

    //Indicates 8 more bytes follow
    public bool IsExtendedLength => Length == 1;

    //Indicates to end of file
    public bool IndefiniteSize => Length == 0;

    public long ExtendedLength
    {
        get => Binary.Read64(Identifier, 4, Binary.IsBigEndian);
        set => Binary.Write64(Identifier, 4, Binary.IsBigEndian, value);
    }

    public int AtomCode
    {
        get => Binary.Read32(Identifier, IsExtendedLength ? 12 : 4, Binary.IsBigEndian);
        set => Binary.Write32(Identifier, IsExtendedLength ? 12 : 4, Binary.IsBigEndian, value);
    }

    public string BoxType
    {
        get => Encoding.UTF8.GetString(Identifier, IsExtendedLength ? 12 : 4, 4);
        set => Encoding.UTF8.GetBytes(value).CopyTo(Identifier, IsExtendedLength ? 12 : 4);
    }

    public Mp4Box(BaseMediaWriter writer, byte[] boxType, long dataSize)
        : base(writer, new byte[dataSize > int.MaxValue ? HeaderSize * 2 : HeaderSize], dataSize > int.MaxValue ? 12 : 4, -1, dataSize, true)
    {
        if (dataSize > int.MaxValue)
        {
            Length = 1;
            ExtendedLength = dataSize;
        }
        else
        {
            Length = (int)dataSize;
        }

        boxType.CopyTo(Identifier, IsExtendedLength ? 12 : 4);
    }

    public void UpdateSize()
    {
        Master.WriteAt(Offset, Identifier, 0, Binary.BytesPerInteger);
        if (IsExtendedLength)
            Master.WriteAt(Offset, Identifier, Binary.BytesPerInteger, Binary.BytesPerLong);
    }

    public void AddChildBox(Mp4Box box)
    {
        if (box == null)
            throw new ArgumentNullException(nameof(box));

        Data = Data.Concat(box.Identifier).Concat(box.Data).ToArray();

        if (DataSize > int.MaxValue)
        {
            Length = 1;
            ExtendedLength = DataSize;
        }
        else
        {
            Length = (int)DataSize;
        }

        return;
    }

    public void AddChildrenBoxes(params Mp4Box[] boxes) => AddChildrenBoxes((IEnumerable<Mp4Box>)boxes);

    public void AddChildrenBoxes(IEnumerable<Mp4Box> boxes)
    {
        foreach (var box in boxes)
            AddChildBox(box);
    }

    public bool HasChild(Mp4Box box)
    {
        if (box == null)
            throw new ArgumentNullException(nameof(box));

        int offset = 0;

        while (offset + HeaderSize <= Data.Length)
        {
            var size = (ulong)Binary.ReadU32(Data, offset, Binary.IsBigEndian);
            var type = Binary.Read32(Data, offset + 4, Binary.IsBigEndian);

            if (size == 1)
                size = Binary.ReadU64(Data, offset + 8, Binary.IsBigEndian);

            if (type == box.AtomCode)
                return true;

            offset += (int)size;
        }

        return false;
    }

    public IEnumerable<Mp4Box> GetChildren()
    {
        int offset = 0;

        while (offset + HeaderSize <= Data.Length)
        {
            var size = (ulong)Binary.ReadU32(Data, offset, Binary.IsBigEndian);
            var type = Binary.Read32(Data, offset + 4, Binary.IsBigEndian);

            if (size == 1)
                size = Binary.ReadU64(Data, offset + 8, Binary.IsBigEndian);

            yield return new Mp4Box(Master as BaseMediaWriter, Binary.GetBytes(type, Binary.IsBigEndian), (long)size);

            offset += (int)size;
        }
    }
}

public abstract class FullBox : Mp4Box
{
    // Constructor for FullBox, which takes the writer, box type, version, and flags
    protected FullBox(BaseMediaWriter writer, byte[] type, byte version, uint flags, int dataSize = 0)
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
        get => Binary.ReadU24(Data, 1, Binary.IsBigEndian);
        set => Binary.Write24(Data, 1, Binary.IsBigEndian, value);
    }

    public const int OffsetToData = 4;
}

public class DataEntryUrlBox : FullBox
{
    public int UrlLength => Binary.Read32(Data, OffsetToData, Binary.IsBigEndian);

    public string Url => Encoding.UTF8.GetString(Data, OffsetToData + 4, UrlLength);

    public DataEntryUrlBox(BaseMediaWriter writer, string dataUrl)
        : base(writer, Encoding.UTF8.GetBytes("url "), 0, 1)
    {
        //Get the bytes
        byte[] urlData = Encoding.UTF8.GetBytes(dataUrl);
        // Write the data URL with a null terminator
        Data = Binary.GetBytes(urlData.Length).Concat(urlData).Concat(byte.MinValue).ToArray();
    }
}

public class DrefBox : FullBox
{
    /// <summary>
    /// The amount of child <see cref="DataEntryUrlBox"/> contained.
    /// </summary>
    public int EntryCount
    {
        get => Binary.Read32(Data, OffsetToData, Binary.IsBigEndian);
        set => Binary.Write32(Data, OffsetToData, Binary.IsBigEndian, value);
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

public class FtypBox : FullBox
{
    public uint MajorBrand
    {
        get => (uint)Binary.Read32(Data, OffsetToData, Binary.IsBigEndian);
        set => Binary.Write32(Data, OffsetToData, Binary.IsBigEndian, value);
    }

    public uint MinorVersion
    {
        get => (uint)Binary.Read32(Data, OffsetToData + 4, Binary.IsBigEndian);
        set => Binary.Write32(Data, OffsetToData + 4, Binary.IsBigEndian, value);
    }

    public IEnumerable<uint> CompatibleBrands
    {
        get
        {
            var offset = 8;
            while (offset < DataSize)
            {
                yield return (uint)Binary.Read32(Data, offset, Binary.IsBigEndian);
            }
        }
        set
        {
            int offset = 8;
            foreach (var brand in value)
            {
                Binary.Write32(Data, ref offset, Binary.IsBigEndian, brand);
            }
        }
    }

    public FtypBox(BaseMediaWriter writer, uint majorBrand, uint minorVersion, params uint[] compatibleBrands)
        : base(writer, Encoding.UTF8.GetBytes("ftyp"), 0, 0, compatibleBrands.Length * Binary.BytesPerInteger)
    {
        MajorBrand = majorBrand;
        MinorVersion = minorVersion;
        CompatibleBrands = compatibleBrands;
    }
}

public class MvhdBox : FullBox
{
    public uint TimeScale
    {
        get => (uint)Binary.Read32(Data, 4, Binary.IsBigEndian);
        set => Binary.Write32(Data, 4, Binary.IsBigEndian, value);
    }

    public ulong Duration
    {
        get => Binary.ReadU64(Data, 8, Binary.IsBigEndian);
        set => Binary.Write64(Data, 8, Binary.IsBigEndian, value);
    }

    public uint PreferredRate
    {
        get => Binary.ReadU32(Data, 16, Binary.IsBigEndian);
        set => Binary.Write32(Data, 16, Binary.IsBigEndian, value);
    }

    public ushort PreferredVolume
    {
        get => Binary.ReadU16(Data, 20, Binary.IsBigEndian);
        set => Binary.Write16(Data, 20, Binary.IsBigEndian, value);
    }

    public ushort Reserved1
    {
        get => Binary.ReadU16(Data, 22, Binary.IsBigEndian);
        set => Binary.Write16(Data, 22, Binary.IsBigEndian, value);
    }

    public IEnumerable<uint> Matrix
    {
        get
        {
            var offset = 24;
            for (int i = 0; i < 9; i++) //Always 9?
            {
                yield return Binary.ReadU32(Data, ref offset, Binary.IsBigEndian);
            }
        }
        set
        {
            var offset = 24;

            foreach (var identity in value)
            {
                Binary.Write32(Data, ref offset, Binary.IsBigEndian, identity);
            }
        }
    }

    public IEnumerable<byte> PreDefined
    {
        get
        {
            return Data.Skip(72).Take(52);
        }
        set
        {
            var array = value.ToArray();

            if (array.Length != 52)
                throw new ArgumentException("PreDefined must contain 52 elements.");

            Array.Copy(array, 0, Data, 72, 52);
        }
    }

    public uint NextTrackID
    {
        get => Binary.ReadU32(Data, 124, Binary.IsBigEndian);
        set => Binary.Write32(Data, 124, Binary.IsBigEndian, value);
    }

    public MvhdBox(BaseMediaWriter writer, uint timeScale, ulong duration, uint preferredRate, ushort preferredVolume, uint[] matrix, byte[] predefined, uint nextTrackID)
        : base(writer, Encoding.UTF8.GetBytes("mvhd"), 0, 1, 4 + 120)
    {
        TimeScale = timeScale;
        Duration = duration;
        PreferredRate = preferredRate;
        PreferredVolume = preferredVolume;
        Reserved1 = 0;
        Matrix = matrix;
        PreDefined = predefined;
        NextTrackID = nextTrackID;
    }
}

public class MdhdBox : FullBox
{
    public uint TimeScale
    {
        get => Binary.ReadU32(Data, 4, Binary.IsBigEndian);
        set => Binary.Write32(Data, 4, Binary.IsBigEndian, value);
    }

    public ulong Duration
    {
        get => (Version == 1) ? Binary.ReadU64(Data, 8, Binary.IsBigEndian) : Binary.ReadU32(Data, 8, Binary.IsBigEndian);
        set
        {
            if (Version == 1)
                Binary.Write64(Data, 8, Binary.IsBigEndian, value);
            else
                Binary.Write32(Data, 8, Binary.IsBigEndian, (uint)value);
        }
    }

    public string Language
    {
        get => Encoding.UTF8.GetString(Data, 12, 3);
        set
        {
            if (string.IsNullOrEmpty(value) || value.Length != 3)
                throw new ArgumentException("Language must be a 3-character string.");

            Encoding.UTF8.GetBytes(value, 0, 3, Data, 12);
        }
    }

    public ushort PreDefined
    {
        get => Binary.ReadU16(Data, 15, Binary.IsBigEndian);
        set => Binary.Write16(Data, 15, Binary.IsBigEndian, value);
    }

    public MdhdBox(BaseMediaWriter writer, byte version, uint flags, uint timeScale, ulong duration, string language, ushort predefined)
        : base(writer, Encoding.UTF8.GetBytes("mdhd"), version, flags, 4 + (version == 1 ? 20 : 12))
    {
        Version = version;
        Flags = flags;
        TimeScale = timeScale;
        Duration = duration;
        Language = language;
        PreDefined = predefined;
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
        get => Binary.ReadU32(Data, 4, Binary.IsBigEndian);
        set => Binary.Write32(Data, 4, Binary.IsBigEndian, value);
    }

    public uint HandlerType
    {
        get => Binary.ReadU32(Data, 8, Binary.IsBigEndian);
        set => Binary.Write32(Data, 8, Binary.IsBigEndian, value);
    }

    public string Name
    {
        get => Encoding.UTF8.GetString(Data, 24, (int)(DataSize - 24));
        set
        {
            byte[] nameData = Encoding.UTF8.GetBytes(value);
            byte[] newData = new byte[24 + nameData.Length];
            Array.Copy(Data, newData, 24);//Copy 24 bytes
            nameData.CopyTo(newData, 24);//Copy nameData to newData starting @ 24
            Data = newData;
        }
    }
}

public class MinfBox : FullBox
{
    public MinfBox(BaseMediaWriter writer, params Mp4Box[] children)
        : base(writer, Encoding.UTF8.GetBytes("minf"), 0, 0)
    {
        if (children != null)
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
        : base(writer, Encoding.ASCII.GetBytes("mdia"), 0, 0)
    {
        MdhdBox = mdhdBox;
        HdlrBox = hdlrBox;
        MinfBox = minfBox;
    }
}

public class TrakBox : FullBox
{
    public MdiaBox MdiaBox { get; }

    public TrakBox(BaseMediaWriter writer, MdiaBox mdiaBox)
        : base(writer, Encoding.ASCII.GetBytes("trak"), 0, 0)
    {
        MdiaBox = mdiaBox;
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
    public int OffsetCount => Binary.Read32(Data, OffsetToData, Binary.IsBigEndian);

    public IEnumerable<uint> ChunkOffsets
    {
        get
        {
            var offset = OffsetToData + 4;
            while (offset < DataSize)
            {
                yield return Binary.ReadU32(Data, offset, Binary.IsBigEndian);
            }
        }
    }

    public StcoBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("stco"), 0, 0)
    {
    }

    public void AddChunkOffset(uint offset)
    {
        Data = Data.Concat(Binary.GetBytes(offset, Binary.IsBigEndian)).ToArray();
    }
}

public class StsdBox : FullBox
{
    public int EntryCount
    {
        get => Binary.Read32(Data, OffsetToData, Binary.IsBigEndian);
        set => Binary.Write32(Data, OffsetToData, Binary.IsBigEndian, value);
    }

    public StsdBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("stsd"), 0, 0)
    {
        // Set the entry count to 0 initially
        EntryCount = 0;
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
        get => Binary.Read32(Data, 4, Binary.IsBigEndian);
        set => Binary.Write32(Data, 4, Binary.IsBigEndian, value);
    }

    public int SampleCount
    {
        get => Binary.Read32(Data, 8, Binary.IsBigEndian);
        set => Binary.Write32(Data, 8, Binary.IsBigEndian, value);
    }

    public IEnumerable<int> SampleSizes
    {
        get
        {
            var offset = 12;
            while (offset < DataSize)
            {
                yield return Binary.Read32(Data, ref offset, Binary.IsBigEndian);
            }
        }
    }

    public StszBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("stsz"), 0, 0)
    {
    }

    public void AddSampleSize(int size)
    {
        ++SampleCount;
        Data = Data.Concat(Binary.GetBytes(size, Binary.IsBigEndian)).ToArray();
    }
}

public class SttsBox : FullBox
{
    public int EntryCount
    {
        get => Binary.Read32(Data, OffsetToData, Binary.IsBigEndian); 
        set => Binary.Write32(Data, OffsetToData, Binary.IsBigEndian, value);
    }

    public IEnumerable<(int SampleCount, int SampleDurtation)> TimeToSampleEntries
    {
        get
        {
            var offset = 8;
            //var index = 0;
            while (offset + 8 <= DataSize /*&& index < EntryCount*/)
            {
                var sampleCount = Binary.Read32(Data, ref offset, Binary.IsBigEndian);
                var sampleDuration = Binary.Read32(Data, ref offset, Binary.IsBigEndian);
                yield return (sampleCount, sampleDuration);
                //++index;
            }
        }
    }

    public SttsBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("stts"), 0, 0)
    {
    }

    public void AddTimeToSampleEntry(int sampleCount, int sampleDuration)
    {
        ++EntryCount;
        Data = Data.Concat(Binary.GetBytes(sampleCount, Binary.IsBigEndian))
                   .Concat(Binary.GetBytes(sampleDuration, Binary.IsBigEndian))
                   .ToArray();
    }
}

#region Fragmenting Boxes

public class TfhdBox : FullBox
{
    public uint TrackId
    {
        get => Binary.ReadU32(Data, OffsetToData, Binary.IsBigEndian);
        set => Binary.Write32(Data, OffsetToData, Binary.IsBigEndian, value);
    }

    public uint SampleDescriptionIndex
    {
        get => Binary.ReadU32(Data, 8, Binary.IsBigEndian);
        set => Binary.Write32(Data, 8, Binary.IsBigEndian, value);
    }

    public uint DefaultSampleDuration
    {
        get => Binary.ReadU32(Data, 12, Binary.IsBigEndian);
        set => Binary.Write32(Data, 12, Binary.IsBigEndian, value);
    }

    public uint DefaultSampleSize
    {
        get => Binary.ReadU32(Data, 16, Binary.IsBigEndian);
        set => Binary.Write32(Data, 16, Binary.IsBigEndian, value);
    }

    public uint DefaultSampleFlags
    {
        get => Binary.ReadU32(Data, 20, Binary.IsBigEndian);
        set => Binary.Write32(Data, 20, Binary.IsBigEndian, value);
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
        get => (Version == 1) ? Binary.ReadU64(Data, OffsetToData, Binary.IsBigEndian) : Binary.ReadU32(Data, OffsetToData, Binary.IsBigEndian);
        set
        {
            if (Version == 1)
                Binary.Write64(Data, OffsetToData, Binary.IsBigEndian, value);
            else
                Binary.Write32(Data, OffsetToData, Binary.IsBigEndian, (uint)value);
        }
    }

    public TfdtBox(BaseMediaWriter writer, ulong baseMediaDecodeTime)
        : base(writer, Encoding.ASCII.GetBytes("tfdt"), 0, 0)
    {
        BaseMediaDecodeTime = baseMediaDecodeTime;
    }
}

public class TrexBox : FullBox
{
    public uint TrackID
    {
        get => Binary.ReadU32(Data, OffsetToData, Binary.IsBigEndian);
        set => Binary.Write32(Data, OffsetToData, Binary.IsBigEndian, value);
    }

    public uint DefaultSampleDescriptionIndex
    {
        get => Binary.ReadU32(Data, 8, Binary.IsBigEndian);
        set => Binary.Write32(Data, 8, Binary.IsBigEndian, value);
    }

    public uint DefaultSampleDuration
    {
        get => Binary.ReadU32(Data, 12, Binary.IsBigEndian);
        set => Binary.Write32(Data, 12, Binary.IsBigEndian, value);
    }

    public uint DefaultSampleSize
    {
        get => Binary.ReadU32(Data, 16, Binary.IsBigEndian);
        set => Binary.Write32(Data, 16, Binary.IsBigEndian, value);
    }

    public uint DefaultSampleFlags
    {
        get => Binary.ReadU32(Data, 20, Binary.IsBigEndian);
        set => Binary.Write32(Data, 20, Binary.IsBigEndian, value);
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
    private const int SampleCountOffset = 4;

    public uint SampleCount
    {
        get => Binary.ReadU32(Data, SampleCountOffset, Binary.IsBigEndian);
        set => Binary.Write32(Data, SampleCountOffset, Binary.IsBigEndian, value);
    }

    public ulong TrackDataOffset
    {
        get => (Version == 0) ? Binary.ReadU32(Data, 8, Binary.IsBigEndian) : Binary.ReadU64(Data, 8, Binary.IsBigEndian);
        set
        {
            if (Version == 0)
                Binary.Write32(Data, 8, Binary.IsBigEndian, (uint)value);
            else
                Binary.Write64(Data, 8, Binary.IsBigEndian, value);
        }
    }

    public uint FirstSampleFlags
    {
        get => (Version == 0) ? 0 : Binary.ReadU32(Data, 16, Binary.IsBigEndian);
        set => Binary.Write32(Data, 16, Binary.IsBigEndian, value);
    }

    public IEnumerable<uint> SampleFlags
    {
        get
        {
            var offset = (Version == 0) ? 12 : 20;
            for (int i = 0; i < SampleCount; i++)
            {
                yield return Binary.ReadU32(Data, ref offset, Binary.IsBigEndian);
            }
        }
        set
        {
            var offset = (Version == 0) ? 12 : 20;

            foreach (var flag in value)
            {
                Binary.Write32(Data, ref offset, Binary.IsBigEndian, flag);
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
        get => Binary.ReadU32(Data, 4, Binary.IsBigEndian);
        set => Binary.Write32(Data, 4, Binary.IsBigEndian, value);
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
        get => Version == 1 ? Binary.ReadU64(Data, 4, Binary.IsBigEndian) : Binary.ReadU32(Data, 4, Binary.IsBigEndian);
        set
        {
            if (Version == 1)
                Binary.Write64(Data, 4, Binary.IsBigEndian, value);
            else
                Binary.Write32(Data, 4, Binary.IsBigEndian, (uint)value);
        }
    }

    public ulong ModificationTime
    {
        get => Version == 1 ? Binary.ReadU64(Data, 12, Binary.IsBigEndian) : Binary.ReadU32(Data, 8, Binary.IsBigEndian);
        set
        {
            if (Version == 1)
                Binary.Write64(Data, 12, Binary.IsBigEndian, value);
            else
                Binary.Write32(Data, 8, Binary.IsBigEndian, (uint)value);
        }
    }

    public uint TrackId
    {
        get => Version == 1 ? Binary.ReadU32(Data, 20, Binary.IsBigEndian) : Binary.ReadU32(Data, 12, Binary.IsBigEndian);
        set
        {
            if (Version == 1)
                Binary.Write32(Data, 20, Binary.IsBigEndian, value);
            else
                Binary.Write32(Data, 12, Binary.IsBigEndian, value);
        }
    }

    public uint Reserved1
    {
        get => Version == 1 ? Binary.ReadU32(Data, 24, Binary.IsBigEndian) : Binary.ReadU32(Data, 16, Binary.IsBigEndian);
        set
        {
            if (Version == 1)
                Binary.Write32(Data, 24, Binary.IsBigEndian, value);
            else
                Binary.Write32(Data, 16, Binary.IsBigEndian, value);
        }
    }

    public ulong Duration
    {
        get => Version == 1 ? Binary.ReadU64(Data, 28, Binary.IsBigEndian) : Binary.ReadU32(Data, 20, Binary.IsBigEndian);
        set
        {
            if (Version == 1)
                Binary.Write64(Data, 28, Binary.IsBigEndian, value);
            else
                Binary.Write32(Data, 20, Binary.IsBigEndian, (uint)value);
        }
    }

    public uint Reserved2
    {
        get => Version == 1 ? Binary.ReadU32(Data, 36, Binary.IsBigEndian) : Binary.ReadU32(Data, 24, Binary.IsBigEndian);
        set
        {
            if (Version == 1)
                Binary.Write32(Data, 36, Binary.IsBigEndian, value);
            else
                Binary.Write32(Data, 24, Binary.IsBigEndian, value);
        }
    }

    public ushort Layer
    {
        get => Version == 1 ? Binary.ReadU16(Data, 40, Binary.IsBigEndian) : Binary.ReadU16(Data, 26, Binary.IsBigEndian);
        set
        {
            if (Version == 1)
                Binary.Write16(Data, 40, Binary.IsBigEndian, value);
            else
                Binary.Write16(Data, 26, Binary.IsBigEndian, value);
        }
    }

    public ushort AlternateGroup
    {
        get => Version == 1 ? Binary.ReadU16(Data, 42, Binary.IsBigEndian) : Binary.ReadU16(Data, 28, Binary.IsBigEndian);
        set
        {
            if (Version == 1)
                Binary.Write16(Data, 42, Binary.IsBigEndian, value);
            else
                Binary.Write16(Data, 28, Binary.IsBigEndian, value);
        }
    }

    public ushort Volume
    {
        get => Version == 1 ? Binary.ReadU16(Data, 44, Binary.IsBigEndian) : Binary.ReadU16(Data, 30, Binary.IsBigEndian);
        set
        {
            if (Version == 1)
                Binary.Write16(Data, 44, Binary.IsBigEndian, value);
            else
                Binary.Write16(Data, 30, Binary.IsBigEndian, value);
        }
    }

    public ushort Reserved3
    {
        get => Version == 1 ? Binary.ReadU16(Data, 46, Binary.IsBigEndian) : Binary.ReadU16(Data, 32, Binary.IsBigEndian);
        set
        {
            if (Version == 1)
                Binary.Write16(Data, 46, Binary.IsBigEndian, value);
            else
                Binary.Write16(Data, 32, Binary.IsBigEndian, value);
        }
    }

    public IEnumerable<ushort> Matrix
    {
        get
        {
            int offset = Version == 1 ? 48 : 34;
            for (int i = 0; i < 9; i++) //Always 9?
            {
                yield return Binary.ReadU16(Data, ref offset, Binary.IsBigEndian);
            }
        }
        set
        {
            int offset = Version == 1 ? 48 : 34;

            foreach (var identity in value)
            {
                Binary.Write16(Data, ref offset, Binary.IsBigEndian, identity);
            }
        }
    }

    public uint Width
    {
        get => Version == 1 ? Binary.ReadU32(Data, 84, Binary.IsBigEndian) : Binary.ReadU32(Data, 56, Binary.IsBigEndian);
        set
        {
            if (Version == 1)
                Binary.Write32(Data, 84, Binary.IsBigEndian, value);
            else
                Binary.Write32(Data, 56, Binary.IsBigEndian, value);
        }
    }

    public uint Height
    {
        get => Version == 1 ? Binary.ReadU32(Data, 88, Binary.IsBigEndian) : Binary.ReadU32(Data, 60, Binary.IsBigEndian);
        set
        {
            if (Version == 1)
                Binary.Write32(Data, 88, Binary.IsBigEndian, value);
            else
                Binary.Write32(Data, 60, Binary.IsBigEndian, value);
        }
    }

    public TkhdBox(BaseMediaWriter writer, ushort version, uint flags)
        : base(writer, Encoding.ASCII.GetBytes("tkhd"), (byte)version, flags)
    {
        if (version == 0)
            Data = new byte[84];
        else if (version == 1)
            Data = new byte[92];
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
        Data = Data.Concat(uuid.ToByteArray()).Concat(userData).ToArray();
    }
}

public class MoovBox : Mp4Box
{
    public MvhdBox MovieHeaderBox { get; }
    public List<TrakBox> Tracks { get; }
    public UdtaBox UserDataBox { get; }

    public MoovBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("moov"), 0)
    {
        MovieHeaderBox = new MvhdBox(writer, 1, 1, 1, 1, null, null, 0);
        Tracks = new List<TrakBox>();
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
        Data = avcCData;
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
        get => Binary.ReadU32(Data, OffsetToData, Binary.IsBigEndian);
        set => Binary.Write32(Data, OffsetToData, Binary.IsBigEndian, value);
    }

    public uint MaxBitrate
    {
        get => Binary.ReadU32(Data, 8, Binary.IsBigEndian);
        set => Binary.Write32(Data, 8, Binary.IsBigEndian, value);
    }

    public uint AvgBitrate
    {
        get => Binary.ReadU32(Data, 12, Binary.IsBigEndian);
        set => Binary.Write32(Data, 12, Binary.IsBigEndian, value);
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
        get => Binary.ReadU16(Data, OffsetToData, Binary.IsBigEndian);
        set => Binary.Write16(Data, OffsetToData, Binary.IsBigEndian, value);
    }

    public IEnumerable<ushort> OpColor
    {
        get
        {
            int offset = OffsetToData + 2;
            for (int i = 0; i < 3; i++) //Todo, if more than 3 should return based on offset...
                yield return Binary.ReadU16(Data, ref offset, Binary.IsBigEndian);
        }
        set
        {
            //Todo , verify only 3 in value.Count()
            int offset = OffsetToData + 2;
            foreach (var identity in value)
                Binary.Write16(Data, ref offset, Binary.IsBigEndian, identity);
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
    // Common properties for sample entry boxes
    public ushort DataReferenceIndex
    {
        get => Binary.ReadU16(Data, OffsetToData, Binary.IsBigEndian);
        set => Binary.Write16(Data, OffsetToData, Binary.IsBigEndian, value);
    }

    public SampleEntryBox(BaseMediaWriter writer, string type)
        : base(writer, Encoding.UTF8.GetBytes(type), 0, 0, HeaderSize + 6)
    {
        DataReferenceIndex = 1; // Default data reference index
    }
}

public class Mp4aBox : SampleEntryBox
{
    public ushort EntryVersion
    {
        get => Binary.ReadU16(Data, OffsetToData + 6, Binary.IsBigEndian);
        set => Binary.Write16(Data, OffsetToData + 6, Binary.IsBigEndian, value);
    }

    public ushort ChannelCount
    {
        get => Binary.ReadU16(Data, OffsetToData + 8, Binary.IsBigEndian);
        set => Binary.Write16(Data, OffsetToData + 8, Binary.IsBigEndian, value);
    }

    public ushort SampleSize
    {
        get => Binary.ReadU16(Data, OffsetToData + 10, Binary.IsBigEndian);
        set => Binary.Write16(Data, OffsetToData + 10, Binary.IsBigEndian, value);
    }

    public ushort CompressionId
    {
        get => Binary.ReadU16(Data, OffsetToData + 12, Binary.IsBigEndian);
        set => Binary.Write16(Data, OffsetToData + 12, Binary.IsBigEndian, value);
    }

    public ushort PacketSize
    {
        get => Binary.ReadU16(Data, OffsetToData + 14, Binary.IsBigEndian);
        set => Binary.Write16(Data, OffsetToData + 14, Binary.IsBigEndian, value);
    }

    public uint SampleRate
    {
        get => Binary.ReadU32(Data, OffsetToData + 16, Binary.IsBigEndian);
        set => Binary.Write32(Data, OffsetToData + 16, Binary.IsBigEndian, value);
    }

    public Mp4aBox(BaseMediaWriter writer)
        : base(writer, "mp4a")
    {
        // Initialize any specific properties for mp4a sample entries
    }
}

public class VisualSampleEntryBox : SampleEntryBox
{
    public ushort PreDefined1
    {
        get => Binary.ReadU16(Data, OffsetToData + 6, Binary.IsBigEndian);
        set => Binary.Write16(Data, OffsetToData + 6, Binary.IsBigEndian, value);
    }

    public ushort Reserved1
    {
        get => Binary.ReadU16(Data, OffsetToData + 8, Binary.IsBigEndian);
        set => Binary.Write16(Data, OffsetToData + 8, Binary.IsBigEndian, value);
    }

    public uint[] PreDefined2
    {
        get
        {
            uint[] predefined = new uint[3];
            for (int i = 0; i < 3; i++)
            {
                predefined[i] = Binary.ReadU32(Data, OffsetToData + 10 + i * 4, Binary.IsBigEndian);
            }
            return predefined;
        }
        set
        {
            if (value.Length != 3)
                throw new ArgumentException("PreDefined2 must contain 3 elements.");

            for (int i = 0; i < 3; i++)
            {
                Binary.Write32(Data, OffsetToData + 10 + i * 4, Binary.IsBigEndian, value[i]);
            }
        }
    }

    public ushort Width
    {
        get => Binary.ReadU16(Data, OffsetToData + 22, Binary.IsBigEndian);
        set => Binary.Write16(Data, OffsetToData + 22, Binary.IsBigEndian, value);
    }

    public ushort Height
    {
        get => Binary.ReadU16(Data, OffsetToData + 24, Binary.IsBigEndian);
        set => Binary.Write16(Data, OffsetToData + 24, Binary.IsBigEndian, value);
    }

    public ushort HorizResolution
    {
        get => Binary.ReadU16(Data, OffsetToData + 26, Binary.IsBigEndian);
        set => Binary.Write16(Data, OffsetToData + 26, Binary.IsBigEndian, value);
    }

    public ushort VertResolution
    {
        get => Binary.ReadU16(Data, OffsetToData + 28, Binary.IsBigEndian);
        set => Binary.Write16(Data, OffsetToData + 28, Binary.IsBigEndian, value);
    }

    public uint Reserved2
    {
        get => Binary.ReadU32(Data, OffsetToData + 30, Binary.IsBigEndian);
        set => Binary.Write32(Data, OffsetToData + 30, Binary.IsBigEndian, value);
    }

    public ushort FrameCount
    {
        get => Binary.ReadU16(Data, OffsetToData + 34, Binary.IsBigEndian);
        set => Binary.Write16(Data, OffsetToData + 34, Binary.IsBigEndian, value);
    }

    // Add more properties specific to visual sample entries if needed

    public VisualSampleEntryBox(BaseMediaWriter writer, string type)
        : base(writer, type)
    {
        // Set default values or initialize properties as needed
    }
}

public class AudioSampleEntryBox : SampleEntryBox
{
    public ushort EntryVersion
    {
        get => Binary.ReadU16(Data, OffsetToData + 6, Binary.IsBigEndian);
        set => Binary.Write16(Data, OffsetToData + 6, Binary.IsBigEndian, value);
    }

    public ushort ChannelCount
    {
        get => Binary.ReadU16(Data, OffsetToData + 8, Binary.IsBigEndian);
        set => Binary.Write16(Data, OffsetToData + 8, Binary.IsBigEndian, value);
    }

    public ushort SampleSize
    {
        get => Binary.ReadU16(Data, OffsetToData + 10, Binary.IsBigEndian);
        set => Binary.Write16(Data, OffsetToData + 10, Binary.IsBigEndian, value);
    }

    public ushort CompressionId
    {
        get => Binary.ReadU16(Data, OffsetToData + 12, Binary.IsBigEndian);
        set => Binary.Write16(Data, OffsetToData + 12, Binary.IsBigEndian, value);
    }

    public ushort PacketSize
    {
        get => Binary.ReadU16(Data, OffsetToData + 14, Binary.IsBigEndian);
        set => Binary.Write16(Data, OffsetToData + 14, Binary.IsBigEndian, value);
    }

    public uint SampleRate
    {
        get => Binary.ReadU32(Data, OffsetToData + 16, Binary.IsBigEndian);
        set => Binary.Write32(Data, OffsetToData + 16, Binary.IsBigEndian, value);
    }

    public AudioSampleEntryBox(BaseMediaWriter writer, string format)
        : base(writer, format)
    {
        // Initialize any specific properties for audio sample entries
    }
}

#endregion