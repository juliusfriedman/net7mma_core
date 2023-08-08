using Media.Common;
using Media.Container;
using System.Collections.Generic;
using System.IO;
using System;
using System.Text;
using System.Linq;
using Media.Containers.BaseMedia;

namespace Container.BaseMedia;

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

    public int AtomCode
    {
        get => Binary.Read32(Identifier, 4, Binary.IsBigEndian);
        set => Binary.Write32(Identifier, 4, Binary.IsBigEndian, value);
    }

    public string BoxType
    {
        get => Encoding.UTF8.GetString(Identifier, 4, 4);
    }

    public Mp4Box(BaseMediaWriter writer, byte[] boxType, long dataSize)
        : base(writer, new byte[HeaderSize], 4, HeaderSize, dataSize, true)
    {
        boxType.CopyTo(Identifier, 4);
    }
}

public abstract class FullBox : Mp4Box
{
    // Constructor for FullBox, which takes the writer, box type, version, and flags
    protected FullBox(BaseMediaWriter writer, byte[] type, byte version, uint flags)
        : base(writer, type, 4)
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
        : base(writer, Encoding.UTF8.GetBytes("ftyp"), 8 + compatibleBrands.Length * 4)
    {
        MajorBrand = majorBrand;
        MinorVersion = minorVersion;
        int offset = 8;
        foreach (var brand in compatibleBrands)
        {
            Binary.Write32(Data, ref offset, Binary.IsBigEndian, brand);
        }
    }

    //Not useful?
    public void AddCompatibleBrand(uint brand)
    {
        Array.Resize(ref m_Data, (int)(DataSize + 4));
        int offset = 8 + (int)DataSize;
        Binary.Write32(Data, ref offset, Binary.IsBigEndian, brand);

        // Update the box size
        DataSize += 4;
        Length = (int)DataSize + 8;
    }
}

public class MvhdBox : Mp4Box
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

            //for (int i = 0; i < 9; i++)
            foreach(var identity in value)
            {
                Binary.Write32(Data, offset, Binary.IsBigEndian, identity);
                offset += 4;
            }
        }
    }

    public byte[] PreDefined
    {
        get
        {
            byte[] predefined = new byte[52];
            Array.Copy(Data, 72, predefined, 0, 52);
            return predefined;
        }
        set
        {
            if (value.Length != 52)
                throw new ArgumentException("PreDefined must contain 52 elements.");

            Array.Copy(value, 0, Data, 72, 52);
        }
    }

    public uint NextTrackID
    {
        get => Binary.ReadU32(Data, 124, Binary.IsBigEndian);
        set => Binary.Write32(Data, 124, Binary.IsBigEndian, value);
    }

    public MvhdBox(BaseMediaWriter writer, uint timeScale, ulong duration, uint preferredRate, ushort preferredVolume, uint[] matrix, byte[] predefined, uint nextTrackID)
        : base(writer, Encoding.UTF8.GetBytes("mvhd"), 4 + 120)
    {
        Version = 0;
        Flags = 0;
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

    public void AddChildBox(Mp4Box box)
    {
        if (box == null)
            throw new ArgumentNullException(nameof(box));

        if (!HasChild(box))
        {
            Data = Data.Concat(box.Identifier).Concat(Binary.GetBytes((int)box.DataSize, Binary.IsBigEndian)).Concat(box.Data).ToArray();
            DataSize += box.IdentifierSize + box.DataSize;
        }
    }

    public bool HasChild(Mp4Box box)
    {
        if (box == null)
            throw new ArgumentNullException(nameof(box));

        int offset = HeaderSize;

        while (offset + HeaderSize <= Data.Length)
        {
            var type = Binary.Read32(Data, offset + 4, Binary.IsBigEndian);
            var size = Binary.ReadU32(Data, offset, Binary.IsBigEndian);

            if (type == box.AtomCode)
                return true;

            offset += (int)size;
        }

        return false;
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

    public ushort[] Matrix
    {
        get
        {
            ushort[] matrix = new ushort[9];
            int offset = Version == 1 ? 48 : 34;

            for (int i = 0; i < 9; i++)
            {
                matrix[i] = Binary.ReadU16(Data, ref offset, Binary.IsBigEndian);
            }

            return matrix;
        }
        set
        {
            if (value == null || value.Length != 9)
                throw new ArgumentException("Matrix should contain 9 elements.");

            int offset = Version == 1 ? 48 : 34;

            for (int i = 0; i < 9; i++)
            {
                Binary.Write16(Data, ref offset, Binary.IsBigEndian, value[i]);
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

#endregion

public class BaseMediaWriter : MediaFileWriter
{
    private readonly List<Mp4Box> boxes = new List<Mp4Box>();

    public override Node Root => boxes[0];

    public override Node TableOfContents => boxes[1];

    public BaseMediaWriter(Uri filename)
        : base(filename, FileAccess.ReadWrite)
    {
        AddBox(new FtypBox(this, 7, 0, 1, 2, 3, 4, 5, 6));
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
    public override Track CreateTrack()
    {
        throw new NotImplementedException();
    }

    public override bool TryAddTrack(Track track)
    {
        throw new NotImplementedException();
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
        WriteInt32LittleEndian(data.Length + 8);
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
        WriteBox("ftyp", ftypBox.Data);
    }

    public void WriteMoovBox(TimeSpan duration, uint timeScale, int trackId, int sampleCount, int[] sampleSizes, TimeSpan[] sampleTimestamps)
    {
        //// Create "moov" box
        //MoovBox moovBox = new MoovBox(this);
        //WriteBox("moov", moovBox.Data);

        //// Create "mvhd" box
        //MvhdBox mvhdBox = new MvhdBox(this, duration, timeScale);
        //WriteBox("mvhd", mvhdBox.Data);

        //// Create "trak" box
        //TrakBox trakBox = new TrakBox(this);
        //WriteBox("trak", trakBox.Data);

        //// Create "tkhd" box
        //TkhdBox tkhdBox = new TkhdBox(this, trackId, duration, timeScale);
        //WriteBox("tkhd", tkhdBox.Data);

        //// Create "mdia" box
        //MdiaBox mdiaBox = new MdiaBox(this);
        //WriteBox("mdia", mdiaBox.Data);

        //// Create "mdhd" box
        //MdhdBox mdhdBox = new MdhdBox(this, duration, timeScale);
        //WriteBox("mdhd", mdhdBox.Data);

        //// Create "hdlr" box
        //HdlrBox hdlrBox = new HdlrBox(this, "vide");
        //WriteBox("hdlr", hdlrBox.Data);

        //// Create "minf" box
        //MinfBox minfBox = new MinfBox(this);
        //WriteBox("minf", minfBox.Data);

        //// Create "vmhd" box
        //VmhdBox vmhdBox = new VmhdBox(this);
        //WriteBox("vmhd", vmhdBox.Data);

        //// Create "dinf" box
        //DinfBox dinfBox = new DinfBox(this);
        //WriteBox("dinf", dinfBox.Data);

        //// Create "stbl" box
        //StblBox stblBox = new StblBox(this);
        //WriteBox("stbl", stblBox.Data);

        //// Create "stsd" box
        //StsdBox stsdBox = new StsdBox(this);
        //WriteBox("stsd", stsdBox.Data);

        //// Create "avc1" box
        //Avc1Box avc1Box = new Avc1Box(this);
        //WriteBox("avc1", avc1Box.Data);

        //// Create "avcC" box
        //AvcCBox avcCBox = new AvcCBox(this);
        //WriteBox("avcC", avcCBox.Data);

        //// Create "stts" box
        //SttsBox sttsBox = new SttsBox(this, sampleCount, sampleTimestamps);
        //WriteBox("stts", sttsBox.Data);

        //// Create "stsz" box
        //StszBox stszBox = new StszBox(this, sampleSizes);
        //WriteBox("stsz", stszBox.Data);

        //// Create "stco" box
        //StcoBox stcoBox = new StcoBox(this, sampleCount, sampleSizes);
        //WriteBox("stco", stcoBox.Data);

        //// Update "mdhd" duration with total duration
        //mdhdBox.Duration = duration;

        //// Update "minf" and "mdia" boxes' size with total size
        //minfBox.UpdateSize();
        //mdiaBox.UpdateSize();

        //// Update "trak" box's size with total size
        //trakBox.UpdateSize();

        //// Update "moov" box's size with total size
        //moovBox.UpdateSize();
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