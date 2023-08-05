using Media.Common;
using Media.Container;
using System.Collections.Generic;
using System.IO;
using System;
using System.Text;
using System.Linq;

namespace Container.BaseMedia;

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
        : base(writer, boxType, 4, HeaderSize, dataSize, false)
    {
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
            //Todo ref overload...
            Binary.Write32(Data, offset, Binary.IsBigEndian, brand);
            offset += 4;
        }
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
            Data = Data.Concat(box.Identifier).Concat(Binary.GetBytes(box.DataSize, Binary.IsBigEndian)).Concat(box.Data).ToArray();
            DataSize += box.IdentifierSize + box.DataSize;
        }
    }

    public bool HasChild(Mp4Box box)
    {
        if (box == null)
            throw new ArgumentNullException(nameof(box));

        int offset = HeaderSize;

        while (offset + 8 <= Data.Length)
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

public class BaseMediaWriter : MediaFileWriter
{
    private readonly List<Mp4Box> boxes = new List<Mp4Box>();

    public override Node Root => boxes[0];

    public override Node TableOfContents => boxes[1];

    public BaseMediaWriter(Uri filename)
        : base(filename, FileAccess.ReadWrite)
    {
        AddBox(new FtypBox(this, 1, 1, 1, 2, 3, 4));
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
        throw new NotImplementedException();
    }
}