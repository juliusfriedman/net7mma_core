using Media.Container;
using System;
using System.Collections.Generic;

public abstract class MediaFileWriter : MediaFileStream
{
    protected MediaFileWriter(Uri location, System.IO.FileAccess access = System.IO.FileAccess.ReadWrite)
        : base(location, access)
    {
    }

    public IList<Track> Tracks { get; protected set; }

    public void Write(Node node)
    {
        WriteAt(Position, node.Identifier, 0, node.Identifier.Length);
        WriteAt(Position, node.Data, 0, (int)node.DataSize);
    }

    public void WriteInt16BigEndian(short value)
    {
        WriteByte((byte)((value >> 8) & 0xFF));
        WriteByte((byte)(value & 0xFF));
    }

    public void WriteInt32BigEndian(int value)
    {
        WriteByte((byte)((value >> 24) & 0xFF));
        WriteByte((byte)((value >> 16) & 0xFF));
        WriteByte((byte)((value >> 8) & 0xFF));
        WriteByte((byte)(value & 0xFF));
    }

    public void WriteInt64BigEndian(long value)
    {
        WriteByte((byte)((value >> 56) & 0xFF));
        WriteByte((byte)((value >> 48) & 0xFF));
        WriteByte((byte)((value >> 40) & 0xFF));
        WriteByte((byte)((value >> 32) & 0xFF));
        WriteByte((byte)((value >> 24) & 0xFF));
        WriteByte((byte)((value >> 16) & 0xFF));
        WriteByte((byte)((value >> 8) & 0xFF));
        WriteByte((byte)(value & 0xFF));
    }

    public void WriteInt16LittleEndian(short value)
    {
        WriteByte((byte)(value & 0xFF));
        WriteByte((byte)((value >> 8) & 0xFF));
    }

    public void WriteInt32LittleEndian(int value)
    {
        WriteByte((byte)(value & 0xFF));
        WriteByte((byte)((value >> 8) & 0xFF));
        WriteByte((byte)((value >> 16) & 0xFF));
        WriteByte((byte)((value >> 24) & 0xFF));
    }

    public void WriteInt64LittleEndian(long value)
    {
        WriteByte((byte)(value & 0xFF));
        WriteByte((byte)((value >> 8) & 0xFF));
        WriteByte((byte)((value >> 16) & 0xFF));
        WriteByte((byte)((value >> 24) & 0xFF));
        WriteByte((byte)((value >> 32) & 0xFF));
        WriteByte((byte)((value >> 40) & 0xFF));
        WriteByte((byte)((value >> 48) & 0xFF));
        WriteByte((byte)((value >> 56) & 0xFF));
    }
}