using Media.Codec.Jpeg;
using Media.Common;
using System;
using System.Text;

namespace Codec.Jpeg.Markers;

public class App : Marker
{
    public new const int Length = 14;

    public string Identifier
    {
        get => Encoding.UTF8.GetString(Data.Array, Data.Offset, 5);
        set => Encoding.UTF8.GetBytes(value, 0, 5, Data.Array, Data.Offset);
    }

    public byte MajorVersion
    {
        get => Data[6];
        set => Data[6] = value;
    }

    public byte MinorVersion
    {
        get => Data[7];
        set => Data[7] = value;
    }

    public Version Version
    {
        get => new Version(MajorVersion, MinorVersion);
        set
        {
            MajorVersion = (byte)value.Major;
            MinorVersion = (byte)value.Minor;
        }
    }

    public int DensityUnits
    {
        get => Data[8];
        set => Data[8] = (byte)value;
    }

    public int XDensity
    {
        get => Binary.Read16(Data.Array, Data.Offset + 9, Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, Data.Offset + 9, Binary.IsLittleEndian, (ushort)value);
    }

    public int YDensity
    {
        get => Binary.Read16(Data.Array, Data.Offset + 11, Binary.IsLittleEndian);
        set => Binary.Write16(Data.Array, Data.Offset + 11, Binary.IsLittleEndian, (ushort)value);
    }

    public int XThumbnail
    {
        get => Data[12];
        set => Data[12] = (byte)value;
    }

    public int YThumbnail
    {
        get => Data[13];
        set => Data[13] = (byte)value;
    }

    public MemorySegment ThumbnailData
    {
        get => Data.Slice(14);
        set => value.CopyTo(Data.Array, Data.Offset + 14);
    }

    public App(byte functionCode, MemorySegment data)
        : base(functionCode, Length + data.Count)
    {
        data.CopyTo(Data.Array, Data.Offset + 14);
    }

    public App(Marker marker) : base(marker)
    {
    }
}
