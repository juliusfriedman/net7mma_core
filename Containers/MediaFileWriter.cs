using Media.Container;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;

public abstract class MediaFileWriter : MediaFileStream
{
    protected MediaFileWriter(Uri location, System.IO.FileAccess access = System.IO.FileAccess.ReadWrite)
        : base(location, access)
    {
    }

    public IList<Track> Tracks { get; protected set; } = new List<Track>();

    public void Write(Node node)
    {
        Write(node.Identifier);
        Write(node.Data);
    }
    
    #region Big Endian

    public void WriteInt16BigEndian(short value)
    {
        Span<byte> temp = stackalloc byte[Media.Common.Binary.BytesPerShort];
        BinaryPrimitives.WriteInt16BigEndian(temp, value);
        Write(temp);
    }

    public void WriteInt32BigEndian(int value)
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

    public void WriteInt16LittleEndian(short value)
    {
        Span<byte> temp = stackalloc byte[Media.Common.Binary.BytesPerShort];
        BinaryPrimitives.WriteInt16LittleEndian(temp, value);
        Write(temp);
    }

    public void WriteInt32LittleEndian(int value)
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
}