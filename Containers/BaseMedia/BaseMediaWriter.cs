using Media.Common;
using Media.Container;
using System.Collections.Generic;
using System.IO;
using System;
using System.Text;
using System.Linq;
using Media.Common.Interfaces;
using Microsoft.VisualBasic;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
        foreach(var box in boxes) 
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
}

public class DataEntryUrlBox : FullBox
{
    public DataEntryUrlBox(BaseMediaWriter writer, string dataUrl)
        : base(writer, Encoding.UTF8.GetBytes("url "), 0, 1)
    {
        //Get the bytes
        byte[] urlData = Encoding.UTF8.GetBytes(dataUrl);
        // Write the data URL
        Data = Binary.GetBytes(urlData.Length + 1).Concat(urlData).ToArray();
    }
}

public class DrefBox : Mp4Box
{
    private const int EntryCountOffset = 4;

    public int EntryCount
    {
        get => Binary.Read32(Data, EntryCountOffset, Binary.IsBigEndian);
        set => Binary.Write32(Data, EntryCountOffset, Binary.IsBigEndian, value);
    }

    public DrefBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("dref"), 0)
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
    private DrefBox _drefBox;

    public DinfBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("dinf"), 0)
    {
        _drefBox = new DrefBox(writer);

        AddChildBox(_drefBox);
    }
    public void AddDataReference(string dataUrl)
    {
        _drefBox.AddDataReference(dataUrl);
    }
}

public class FtypBox : Mp4Box
{
    public uint MajorBrand
    {
        get => (uint)Binary.Read32(Data, 0, Binary.IsBigEndian);
        set => Binary.Write32(Data, 0, Binary.IsBigEndian, value);
    }

    public uint MinorVersion
    {
        get => (uint)Binary.Read32(Data, 4, Binary.IsBigEndian);
        set => Binary.Write32(Data, 4, Binary.IsBigEndian, value);
    }

    public IEnumerable<uint> CompatibleBrands
    {
        get
        {
            var offset = 8;
            while(offset < DataSize)
            {
                yield return (uint)Binary.Read32(Data, offset, Binary.IsBigEndian);
            }
        }
    }

    public FtypBox(BaseMediaWriter writer, uint majorBrand, uint minorVersion, params uint[] compatibleBrands)
        : base(writer, Encoding.UTF8.GetBytes("ftyp"), compatibleBrands.Length * Binary.BytesPerInteger)
    {
        MajorBrand = majorBrand;
        MinorVersion = minorVersion;
        int offset = 8;
        foreach (var brand in compatibleBrands)
        {
            Binary.Write32(Data, ref offset, Binary.IsBigEndian, brand);
        }
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

            foreach(var identity in value)
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

public class MdhdBox : Mp4Box
{
    public byte Version
    {
        get => Data[0];
        set => Data[0] = value;
    }

    public uint Flags
    {
        get => Binary.ReadU24(Data, 1, Binary.IsBigEndian);
        set => Binary.Write24(Data, 1, Binary.IsBigEndian, value);
    }

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
        : base(writer, Encoding.UTF8.GetBytes("mdhd"), 4 + (version == 1 ? 20 : 12))
    {
        Version = version;
        Flags = flags;
        TimeScale = timeScale;
        Duration = duration;
        Language = language;
        PreDefined = predefined;
    }
}

public class HdlrBox : Mp4Box
{
    public HdlrBox(BaseMediaWriter writer, uint handlerType)
        : base(writer, Encoding.UTF8.GetBytes("hdlr"), 24)
    {
        Version = 0;
        Flags = 0;
        PreDefined = 0;
        HandlerType = handlerType;
        Name = string.Empty;
    }

    public byte Version
    {
        get => Data[0];
        set => Data[0] = value;
    }

    public uint Flags
    {
        get => Binary.ReadU24(Data, 1, Binary.IsBigEndian);
        set => Binary.Write24(Data, 1, Binary.IsBigEndian, value);
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

public class MinfBox : Mp4Box
{
    public MinfBox(BaseMediaWriter writer, params Mp4Box[] children)
        : base(writer, Encoding.UTF8.GetBytes("minf"), 0)
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

public class MdiaBox : Mp4Box
{
    public MdhdBox MdhdBox { get; }
    public HdlrBox HdlrBox { get; }
    public MinfBox MinfBox { get; }

    public MdiaBox(BaseMediaWriter writer, MdhdBox mdhdBox, HdlrBox hdlrBox, MinfBox minfBox)
        : base(writer, Encoding.ASCII.GetBytes("mdia"), 0)
    {
        MdhdBox = mdhdBox;
        HdlrBox = hdlrBox;
        MinfBox = minfBox;
    }
}

public class TrakBox : Mp4Box
{
    public MdiaBox MdiaBox { get; }

    public TrakBox(BaseMediaWriter writer, MdiaBox mdiaBox)
        : base(writer, Encoding.ASCII.GetBytes("trak"), 0)
    {
        MdiaBox = mdiaBox;
    }
}

public class StblBox : Mp4Box
{
    private StsdBox _stsdBox;
    private SttsBox _sttsBox;
    private StszBox _stszBox;
    private StcoBox _stcoBox;

    public StblBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("stbl"), 0)
    {
        _stsdBox = new StsdBox(writer);
        _sttsBox = new SttsBox(writer);
        _stszBox = new StszBox(writer);
        _stcoBox = new StcoBox(writer);

        AddChildBox(_stsdBox);
        AddChildBox(_sttsBox);
        AddChildBox(_stszBox);
        AddChildBox(_stcoBox);
    }

    public void AddSampleDescriptionBox(Mp4Box sampleEntry)
    {
        _stsdBox.AddSampleEntry(sampleEntry);
    }

    public void AddTimeToSampleEntry(int sampleCount, int sampleDuration)
    {
        _sttsBox.AddTimeToSampleEntry(sampleCount, sampleDuration);
    }

    public void AddSampleSize(int size)
    {
        _stszBox.AddSampleSize(size);
    }

    public void AddChunkOffset(uint offset)
    {
        _stcoBox.AddChunkOffset(offset);
    }
}

public class StcoBox : Mp4Box
{
    public int OffsetCount => Binary.Read32(Data, 0, Binary.IsBigEndian);

    public IEnumerable<uint> ChunkOffsets
    {
        get
        {
            var offset = 4;
            while (offset < DataSize)
            {
                yield return (uint)Binary.Read32(Data, offset, Binary.IsBigEndian);
            }
        }
    }

    public StcoBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("stco"), 0)
    {
    }

    public void AddChunkOffset(uint offset)
    {
        Data = Data.Concat(Binary.GetBytes(offset, Binary.IsBigEndian)).ToArray();
    }
}

public class StsdBox : Mp4Box
{
    private const int EntryCountOffset = 4;

    public int EntryCount
    {
        get => Binary.Read32(Data, EntryCountOffset, Binary.IsBigEndian);
        set => Binary.Write32(Data, EntryCountOffset, Binary.IsBigEndian, value);
    }

    public StsdBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("stsd"), 0)
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

public class StszBox : Mp4Box
{
    public int SampleSize
    {
        get => Binary.Read32(Data, 0, Binary.IsBigEndian);
        set => Binary.Write32(Data, 4, Binary.IsBigEndian, value);
    }

    public int SampleCount
    {
        get => Binary.Read32(Data, 4, Binary.IsBigEndian); 
        set => Binary.Write32(Data, 4, Binary.IsBigEndian, value);
    }

    public IEnumerable<int> SampleSizes
    {
        get
        {
            var offset = 8;
            while (offset < DataSize)
            {
                yield return Binary.Read32(Data, ref offset, Binary.IsBigEndian);
            }
        }
    }

    public StszBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("stsz"), 0)
    {
    }

    public void AddSampleSize(int size)
    {
        ++SampleCount;
        Data = Data.Concat(Binary.GetBytes(size, Binary.IsBigEndian)).ToArray();
    }
}

public class SttsBox : Mp4Box
{
    public int EntryCount { get => Binary.Read32(Data, 0, Binary.IsBigEndian); set => Binary.Write32(Data, 0, Binary.IsBigEndian, value); }

    public IEnumerable<(int SampleCount, int SampleDurtation)> TimeToSampleEntries
    {
        get
        {
            var offset = 4;
            while (offset + 8 <= DataSize)
            {
                var sampleCount = Binary.Read32(Data, ref offset, Binary.IsBigEndian);
                var sampleDuration = Binary.Read32(Data, ref offset , Binary.IsBigEndian);
                yield return (sampleCount, sampleDuration);
            }
        }
    }

    public SttsBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("stts"), 0)
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
        get => Binary.ReadU32(Data, 4, Binary.IsBigEndian);
        set => Binary.Write32(Data, 4, Binary.IsBigEndian, value);
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
        : base(writer, Encoding.ASCII.GetBytes("tfhd"), 0, 0)
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
        get => Binary.ReadU64(Data, 4, Binary.IsBigEndian);
        set => Binary.Write64(Data, 4, Binary.IsBigEndian, value);
    }

    public TfdtBox(BaseMediaWriter writer, ulong baseMediaDecodeTime)
        : base(writer, Encoding.ASCII.GetBytes("tfdt"), 0, 0)
    {
        BaseMediaDecodeTime = baseMediaDecodeTime;
    }
}

public class TrunBox : FullBox
{
    public uint SampleCount
    {
        get => Binary.ReadU32(Data, 4, Binary.IsBigEndian);
        set => Binary.Write32(Data, 4, Binary.IsBigEndian, value);
    }

    public new uint DataOffset
    {
        get => Binary.ReadU32(Data, 8, Binary.IsBigEndian);
        set => Binary.Write32(Data, 8, Binary.IsBigEndian, value);
    }

    public uint FirstSampleFlags
    {
        get => Binary.ReadU32(Data, 12, Binary.IsBigEndian);
        set => Binary.Write32(Data, 12, Binary.IsBigEndian, value);
    }

    public uint[] SampleDuration { get; set; }
    public uint[] SampleSize { get; set; }
    public uint[] SampleFlags { get; set; }
    public uint[] SampleCompositionTimeOffset { get; set; }

    public TrunBox(BaseMediaWriter writer, uint sampleCount, uint dataOffset, uint firstSampleFlags)
        : base(writer, Encoding.ASCII.GetBytes("trun"), 0, 0)
    {
        SampleCount = sampleCount;
        DataOffset = dataOffset;
        FirstSampleFlags = firstSampleFlags;
    }
}

public class TrafBox : Mp4Box
{
    public TrafBox(BaseMediaWriter writer)
        : base(writer, Encoding.ASCII.GetBytes("traf"), 0)
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

    public void AddChildBox(Mp4Box box)
    {
        if (box == null)
            throw new ArgumentNullException(nameof(box));

        Data = Data.Concat(Binary.GetBytes((int)box.DataSize, Binary.IsBigEndian)).Concat(box.Identifier).Concat(box.Data).ToArray();
        DataSize += box.IdentifierSize + box.DataSize;
    }
}

public class MoofBox : Mp4Box
{
    public MoofBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("moof"), 0)
    {
    }

    public void AddTrackFragment(Track track, int trackId)
    {
        var traf = new TrafBox(Master as BaseMediaWriter);
        traf.AddTrackFragment(track, trackId);
        AddChildBox(traf);
    }

    public void AddChildBox(Mp4Box box)
    {
        if (box == null)
            throw new ArgumentNullException(nameof(box));

        Data = Data.Concat(Binary.GetBytes((int)box.DataSize, Binary.IsBigEndian)).Concat(box.Identifier).Concat(box.Data).ToArray();
        DataSize += box.IdentifierSize + box.DataSize;
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

    public void WriteData(byte[] data)
    {
        Data = data;
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

public class VmhdBox : FullBox
{
    public ushort GraphicsMode
    {
        get => Binary.ReadU16(Data, 0, Binary.IsBigEndian);
        set => Binary.Write16(Data, 0, Binary.IsBigEndian, value);
    }

    public IEnumerable<ushort> OpColor
    {
        get
        {
            int offset = 2;
            for (int i = 0; i < 3; i++) 
            {
                yield return Binary.ReadU16(Data, ref offset, Binary.IsBigEndian);
            }
        }
        set
        {
            int offset = 2;

            foreach (var identity in value)
            {
                Binary.Write16(Data, ref offset, Binary.IsBigEndian, identity);
            }
        }
    }

    public VmhdBox(BaseMediaWriter writer)
        : base(writer, Encoding.UTF8.GetBytes("vmhd"), 0, 1)
    {
        // Initialize graphics mode and opcolor
        GraphicsMode = 0;
        OpColor = new ushort[] { 0, 0, 0 };
    }
}

#endregion

public class BaseMediaWriter : MediaFileWriter
{
    private readonly List<Mp4Box> boxes = new List<Mp4Box>();

    public override Node Root => boxes[0];

    public override Node TableOfContents => boxes[1];

    public BaseMediaWriter(Uri filename)
        : base(filename, FileAccess.ReadWrite)
    {
        //AddBox(new FtypBox(this, 7, 0, 1, 2, 3, 4, 5, 6));
        //AddBox(new MoovBox(this));
    }

    public void AddBox(Mp4Box box)
    {
        if (box == null)
            throw new ArgumentNullException(nameof(box));

        boxes.Add(box);

        Write(box);
    }

    public override IEnumerator<Node> GetEnumerator() => boxes.GetEnumerator();

    public override IEnumerable<Track> GetTracks() => Tracks;

    public override SegmentStream GetSample(Track track, out TimeSpan duration)
    {
        //Needs sampleOffsets box.
        throw new NotImplementedException();
    }

    public override string ToTextualConvention(Node node) => BaseMediaReader.ToUTF8FourCharacterCode(node.Identifier, node.IdentifierSize);

    //Need overloads with type e.g. CreateFragmentedTrack etc
    public override Track CreateTrack(Media.Sdp.MediaType mediaType)
    {
        var trakBox = new TrakBox(this, new MdiaBox(this, new MdhdBox(this, 0, 0, 0, 0, "english", 0), new HdlrBox(this, 0), new MinfBox(this, null)));
        var track = new Track(trakBox, "track", 1, DateTime.UtcNow, DateTime.UtcNow, 1, 0, 0, TimeSpan.Zero, TimeSpan.Zero, 0, mediaType, new byte[4]);

        track.UserData = new Dictionary<string, object>();

        return track;
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

/// <summary>
/// Useful to support (fragmented) writing.
/// </summary>
public class Mp4Writer : BaseMediaWriter
{
    public Mp4Writer(Uri fileName)
        : base(fileName)
    {
        // Write the "ftyp" box
        WriteFtypBox();
    }

    public void WriteBox(string boxType, byte[] data)
    {
        // Write the box header
        WriteInt32LittleEndian(data.Length + Mp4Box.HeaderSize);
        Write(Encoding.UTF8.GetBytes(boxType));

        // Write the box data
        Write(data);
    }

    public void WriteFtypBox()
    {
        uint majorBrand = 0x69736F6D; // "isom"
        uint minorVersion = 0;
        uint[] compatibleBrands = new uint[] { 0x69736F6D, 0x61766331 }; // "isom", "avc1"
        FtypBox ftypBox = new FtypBox(this, majorBrand, minorVersion, compatibleBrands);
        Write(ftypBox);
    }

    public void WriteMoovBox(TimeSpan duration, uint timeScale, int trackId, int sampleCount, int[] sampleSizes, TimeSpan[] sampleTimestamps)
    {
        // Create the moov box
        MoovBox moovBox = new MoovBox(this);

        // Create the mvhd box
        MvhdBox mvhdBox = new MvhdBox(this, (uint)duration.Ticks, timeScale, 3, 4, null, null, 5);
        moovBox.AddChildBox(mvhdBox);

        var hdlrBox = new HdlrBox(this, 0);

        var minfBox = new MinfBox(this);

        MdhdBox mdhdBox = new MdhdBox(this, 0, 1, 2, 3, "", 5);

        // Create the trak box
        TrakBox trakBox = new TrakBox(this, new MdiaBox(this, mdhdBox, hdlrBox, minfBox));
        moovBox.AddChildBox(trakBox);

        // Create the tkhd box
        TkhdBox tkhdBox = new TkhdBox(this, 1, 0);
        trakBox.AddChildBox(tkhdBox);

        // Create the mdia box        
        MdiaBox mdiaBox = new MdiaBox(this, mdhdBox, new HdlrBox(this, 0), minfBox);
        trakBox.AddChildBox(mdiaBox);

        // Create the vmhd box
        VmhdBox vmhdBox = new VmhdBox(this);
        mdiaBox.MinfBox.AddChildBox(vmhdBox);

        // Create the dinf box
        DinfBox dinfBox = new DinfBox(this);
        mdiaBox.MinfBox.AddChildBox(dinfBox);

        // Create the dref box
        DrefBox drefBox = new DrefBox(this);
        dinfBox.AddChildBox(drefBox);

        // Create the url box
        DataEntryUrlBox urlBox = new DataEntryUrlBox(this, "");
        drefBox.AddChildBox(urlBox);

        // Create the stbl box
        StblBox stblBox = new StblBox(this);
        mdiaBox.MinfBox.AddChildBox(stblBox);

        // Create the stsd box
        StsdBox stsdBox = new StsdBox(this);
        stblBox.AddChildBox(stsdBox);

        // Create the avc1 box
        Avc1Box avc1Box = new Avc1Box(this, new byte[0]); // Replace with actual avcC data
        stsdBox.AddSampleEntry(avc1Box);

        // Create the stts box
        SttsBox sttsBox = new SttsBox(this);
        foreach (TimeSpan timestamp in sampleTimestamps)
        {
            sttsBox.AddTimeToSampleEntry(1, (int)timestamp.TotalMilliseconds * (int)timeScale / 1000);
        }
        stblBox.AddChildBox(sttsBox);

        // Create the stsz box
        StszBox stszBox = new StszBox(this);
        foreach (int size in sampleSizes)
        {
            stszBox.AddSampleSize(size);
        }
        stblBox.AddChildBox(stszBox);

        // Create the stco box
        StcoBox stcoBox = new StcoBox(this);
        uint offset = 0;
        foreach (int size in sampleSizes)
        {
            stcoBox.AddChunkOffset(offset);
            offset += (uint)size;
        }
        stblBox.AddChildBox(stcoBox);

        // Write the moov box to the file
        AddBox(moovBox);
    }

    public void WriteMoofBox(uint sequenceNumber, int trackId, TimeSpan baseMediaDecodeTime, int[] sampleSizes, uint[] sampleFlags)
    {
        // Create "moof" box
        MoofBox moofBox = new MoofBox(this);

        // Create "mfhd" box
        MfhdBox mfhdBox = new MfhdBox(this, sequenceNumber);

        // Create "traf" box
        TrafBox trafBox = new TrafBox(this);

        // Create "tfhd" box
        TfhdBox tfhdBox = new TfhdBox(this, (uint)trackId, (uint)baseMediaDecodeTime.Ticks / 10, 0, 0);

        // Create "tfdt" box
        TfdtBox tfdtBox = new TfdtBox(this, (uint)baseMediaDecodeTime.Ticks / 10);

        // Create "trun" box
        TrunBox trunBox = new TrunBox(this, (uint)sampleSizes.Length, 0, 0);
        //trunBox.SampleSizes = sampleSizes;
        trunBox.SampleFlags = sampleFlags;

        // Add boxes to their parent boxes
        trafBox.AddChildBox(tfhdBox);
        trafBox.AddChildBox(tfdtBox);
        trafBox.AddChildBox(trunBox);
        moofBox.AddChildBox(mfhdBox);
        moofBox.AddChildBox(trafBox);

        // Write "moof" box
        WriteBox("moof", moofBox.Data);
    }
}