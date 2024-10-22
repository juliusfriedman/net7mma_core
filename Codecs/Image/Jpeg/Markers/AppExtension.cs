using Codec.Jpeg.Classes;
using Media.Codec.Jpeg;
using Media.Common;
using System.Text;

namespace Codec.Jpeg.Markers;

internal class AppExtension : Marker
{
    public new const int Length = 6;

    public AppExtension(byte functionCode, MemorySegment data)
        : base(functionCode, LengthBytes + Length + data.Count)
    {
        data.CopyTo(ThumbnailData);
    }

    public AppExtension(MemorySegment data)
        : base(data)
    {
    }

    public string Identifier
    {
        get => Encoding.UTF8.GetString(Array, DataOffset, 5);
        set => Encoding.UTF8.GetBytes(value, 0, 5, Array, DataOffset);
    }

    public int ThumbnailFormat
    {
        get => Array[DataOffset + 6];
        set => Array[DataOffset + 6] = (byte)value;
    }

    public ThumbnailFormatType ThumbnailFormatType
    {
        get => (ThumbnailFormatType)ThumbnailFormat;
        set => ThumbnailFormat = (int)value;
    }

    public MemorySegment ThumbnailData
    {
        get => this.Slice(DataOffset + DataLength > 0 ? Length : 0);
        set
        {
            using var slice = ThumbnailData;
            value.CopyTo(slice);
        }
    }
}
