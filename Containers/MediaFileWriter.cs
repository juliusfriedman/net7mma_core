using Media.Common;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace Media.Container;

public abstract class MediaFileWriter : MediaFileStream
{
    protected MediaFileWriter(Uri location, System.IO.FileAccess access = System.IO.FileAccess.ReadWrite)
        : base(location, access)
    {
    }

    public IList<Track> Tracks { get; protected set; } = new List<Track>();

    #region Abstraction

    public abstract Track CreateTrack(Sdp.MediaType mediaType);

    public abstract bool TryAddTrack(Track track);

    #endregion

    public void Write(Node node)
    {
        Write(node.Identifier);
        Write(node.Data.Array, node.Data.Offset, node.Data.Count);
    }

    #region Big Endian

    public void WriteInt16BigEndian(short value) //=> Write(Binary.GetBytes(value, Binary.IsLittleEndian));
    {
        Span<byte> temp = stackalloc byte[Media.Common.Binary.BytesPerShort];
        BinaryPrimitives.WriteInt16BigEndian(temp, value);
        Write(temp);
    }

    public void WriteInt32BigEndian(int value) //=> Write(Binary.GetBytes(value, Binary.IsLittleEndian));
    {
        Span<byte> temp = stackalloc byte[Media.Common.Binary.BytesPerInteger];
        BinaryPrimitives.WriteInt32BigEndian(temp, value);
        Write(temp);
    }

    public void WriteInt64BigEndian(long value)
    {
        Span<byte> temp = stackalloc byte[Media.Common.Binary.BytesPerLong];
        BinaryPrimitives.WriteInt64BigEndian(temp, value);
        Write(temp);
    }

    #endregion

    #region Little Endian

    public void WriteInt16LittleEndian(short value) //=> Write(Binary.GetBytes(value, Binary.IsBigEndian));
    {
        Span<byte> temp = stackalloc byte[Media.Common.Binary.BytesPerShort];
        BinaryPrimitives.WriteInt16LittleEndian(temp, value);
        Write(temp);
    }

    public void WriteInt32LittleEndian(int value) //=> Write(Binary.GetBytes(value, Binary.IsBigEndian));
    {
        Span<byte> temp = stackalloc byte[Media.Common.Binary.BytesPerInteger];
        BinaryPrimitives.WriteInt32LittleEndian(temp, value);
        Write(temp);
    }

    public void WriteInt64LittleEndian(long value)
    {
        Span<byte> temp = stackalloc byte[Media.Common.Binary.BytesPerLong];
        BinaryPrimitives.WriteInt64LittleEndian(temp, value);
        Write(temp);
    }

    #endregion
}