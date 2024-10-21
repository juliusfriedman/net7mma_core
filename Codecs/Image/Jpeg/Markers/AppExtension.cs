using Codec.Jpeg.Classes;
using Media.Codec.Jpeg;
using Media.Common;
using System.Text;

namespace Codec.Jpeg.Markers;

internal class AppExtension : Marker
{
    public new const int Length = 6;

    public AppExtension(byte functionCode, MemorySegment data)
        : base(functionCode, Length + data.Count)
    {
        data.CopyTo(ThumbnailData);
    }

    public AppExtension(MemorySegment data)
        : base(data)
    {
    }

    public string Identifier
    {
        get => Encoding.UTF8.GetString(Data.Array, Data.Offset, 5);
        set => Encoding.UTF8.GetBytes(value, 0, 5, Data.Array, Data.Offset);
    }

    public int ThumbnailFormat
    {
        get => Data[6];
        set => Data[6] = (byte)value;
    }

    public ThumbnailFormatType ThumbnailFormatType
    {
        get => (ThumbnailFormatType)ThumbnailFormat;
        set => ThumbnailFormat = (int)value;
    }

    public MemorySegment ThumbnailData
    {
        get => Data.Slice(DataSize > 0 ? 6 : 0);
        set
        {
            using var slice = ThumbnailData;
            value.CopyTo(slice);
        }
    }
}
