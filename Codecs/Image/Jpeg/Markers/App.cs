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
        get
        {
            using var slice = Data;
            return Encoding.UTF8.GetString(slice.Array, slice.Offset, 5);
        }
        set 
        {
            using var slice = Data;
            Encoding.UTF8.GetBytes(value, 0, 5, slice.Array, slice.Offset);
        }
    }

    public byte MajorVersion
    {
        get 
        {
            using var slice = Data;
            return slice[6];
        }
        set 
        {
            using var slice = Data;
            slice[6] = value;
        }
    }

    public byte MinorVersion
    {
        get
        {
            using var slice = Data;
            return slice[7];
        }
        set
        {
            using var slice = Data;
            slice[7] = value;
        }
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
        get 
        {
            using var slice = Data;
            return slice[8];
        }
        set
        {
            using var slice = Data;
            slice[8] = (byte)value;
        }
    }

    public int XDensity
    {
        get
        {
            using var slice = Data;
            return Binary.Read16(slice.Array, slice.Offset + 9, Binary.IsLittleEndian);
        }
        set
        {
            using var slice = Data;
            Binary.Write16(slice.Array, slice.Offset + 9, Binary.IsLittleEndian, (ushort)value);
        }
    }

    public int YDensity
    {
        get
        {
            using var slice = Data;
            return Binary.Read16(slice.Array, slice.Offset + 11, Binary.IsLittleEndian);
        }
        set
        {
            using var slice = Data;
            Binary.Write16(slice.Array, slice.Offset + 11, Binary.IsLittleEndian, (ushort)value);
        }
    }

    public int XThumbnail
    {
        get
        {
            using var slice = Data;
            return slice[12];
        }
        set
        {
            using var slice = Data;
            slice[12] = (byte)value;
        }
    }

    public int YThumbnail
    {
        get
        {
            using var slice = Data;
            return slice[13];
        }
        set
        {
            using var slice = Data;
            slice[13] = (byte)value;
        }
    }

    public MemorySegment ThumbnailData
    {
        get
        {
            using var slice = Data;
            return slice.Slice(14);
        }
        set
        {
            using var slice = Data;
            value.CopyTo(slice);
        }
    }

    public App(byte functionCode, MemorySegment data)
        : base(functionCode, LengthBytes + Length + data.Count)
    {
        using var slice = Data;
        data.CopyTo(slice);
    }

    public App(Marker marker) : base(marker)
    {
    }
}
