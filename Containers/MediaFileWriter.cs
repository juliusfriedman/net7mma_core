using Media.Container;
using System;
using System.Collections.Generic;
using System.IO;

public abstract class MediaFileWriter : MediaFileStream
{
    protected MediaFileWriter(Uri location, System.IO.FileAccess access = System.IO.FileAccess.ReadWrite)
        : base(location, access)
    {
    }

    public IList<Track> Tracks { get; protected set; }

    // Implement methods for writing video frames and audio samples

    //public abstract Node CreateHeader();
    //public abstract void WriteNode(Node node);

    public void Write(Node node)
    {
        node.DataOffset = Position;
        WriteAt(node.DataOffset, node.Data, 0, (int)node.DataSize);
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