using Media.Common;
using Media.Container;
using System.Collections.Generic;
using System.IO;
using System;
using Media.Containers.BaseMedia;
using System.Text;

namespace Container.BaseMedia;

public class Mp4Box : Node
{
    // Header size for a box (4 bytes for size + 4 bytes for type)
    private const int HeaderSize = 8;

    public int Length
    {
        get => Binary.Read32(LengthSegment, 4, Binary.IsBigEndian);
        set => Binary.Write32(LengthSegment.Array, LengthSegment.Offset, Binary.IsBigEndian, value);
    }

    public Mp4Box(BaseMediaWriter writer, byte[] boxType, long dataSize)
        : base(writer, boxType, 4, HeaderSize, dataSize, false)
    {
    }

    protected virtual void WriteBody(BaseMediaWriter writer)
    {
        // Default implementation does nothing. Subclasses should override this method to write the box body.
    }
}

public class FtypBox : Mp4Box
{
    public uint MajorBrand { get; }
    public uint MinorVersion { get; }
    public uint[] CompatibleBrands { get; }

    public FtypBox(BaseMediaWriter writer, uint majorBrand, uint minorVersion, params uint[] compatibleBrands)
        : base(writer, Encoding.ASCII.GetBytes("ftyp"), 0)
    {
        MajorBrand = majorBrand;
        MinorVersion = minorVersion;
        CompatibleBrands = compatibleBrands;
        CalculateDataSize();
    }

    private void CalculateDataSize()
    {
        // Data size = 8 bytes for MajorBrand and MinorVersion +
        //             4 bytes for each CompatibleBrand
        DataSize = 8 + (uint)(CompatibleBrands.Length * 4);
    }

    protected override void WriteBody(BaseMediaWriter writer)
    {
        // Write the MajorBrand and MinorVersion in big-endian format
        writer.WriteInt32BigEndian((int)MajorBrand);
        writer.WriteInt32BigEndian((int)MinorVersion);

        // Write each CompatibleBrand in big-endian format
        foreach (uint compatibleBrand in CompatibleBrands)
        {
            writer.WriteInt32BigEndian((int)compatibleBrand);
        }
    }
}

//MoovBox

public class TrakBox : Mp4Box
{
    public TrakBox(BaseMediaWriter writer)
        : base(writer, Encoding.ASCII.GetBytes("trak"), 0)
    {
        // Leave the dataSize as 0 for now. It will be updated later during the writing process.
    }

    // Define properties to represent fields inside the 'trak' box.
    // For example:
    //public TkhdBox TkhdBox { get; set; }
    //public MdhdBox MdhdBox { get; set; }
    //public HdlrBox HdlrBox { get; set; }
    //public MinfBox MinfBox { get; set; }

    protected override void WriteBody(BaseMediaWriter writer)
    {
        // Write the child boxes in the appropriate order.
        //TkhdBox.Write(writer);
        //MdhdBox.Write(writer);
        //HdlrBox.Write(writer);
        //MinfBox.Write(writer);
    }
}


//public class MdatBox : Mp4Box
//{
//    // Implement the 'mdat' box here.
//    // Similar to FtypBox, define properties to represent fields inside the 'mdat' box.
//    // Implement the WriteBody and PrepareForWriting methods as shown in the FtypBox class.
//    // This box will hold the actual media data (audio, video, etc.).
//}

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