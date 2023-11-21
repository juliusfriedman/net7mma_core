using Media.Common;
using Media.Container;
using System.Collections.Generic;
using System.IO;
using System;

namespace Media.Containers.BaseMedia;

public class BaseMediaWriter : MediaFileWriter
{
    private readonly List<Mp4Box> boxes = new List<Mp4Box>();

    public override Node Root => boxes[0];

    public override Node TableOfContents => boxes[1];

    public BaseMediaWriter(Uri filename)
        : base(filename, FileAccess.ReadWrite)
    {
        //AddBox(new FtypBox(this, 7, 0, 1, 2, 3, 4, 5, 6));
        //AddBox(new MoovBox(this));
    }

    public void AddBox(Mp4Box box)
    {
        if (box is null)
            throw new ArgumentNullException(nameof(box));

        boxes.Add(box);

        Write(box);
    }

    public override IEnumerator<Node> GetEnumerator() => boxes.GetEnumerator();

    public override IEnumerable<Track> GetTracks() => Tracks;

    public override SegmentStream GetSample(Track track, out TimeSpan duration)
    {
        //Needs sampleOffsets box.
        throw new NotImplementedException();
    }

    public override string ToTextualConvention(Node node) => BaseMediaReader.ToUTF8FourCharacterCode(node.Identifier, node.IdentifierSize);

    //Need overloads with type e.g. CreateFragmentedTrack etc
    public override Track CreateTrack(Media.Sdp.MediaType mediaType)
    {
        var trakBox = new TrakBox(this, new MdiaBox(this, new MdhdBox(this, 0, 0, 0, 0, 0x55C4, 0), new HdlrBox(this, 0), new MinfBox(this, null)));
        var track = new Track(trakBox, "track", 1, DateTime.UtcNow, DateTime.UtcNow, 1, 0, 0, TimeSpan.Zero, TimeSpan.Zero, 0, mediaType, new byte[4]);

        track.UserData = new Dictionary<string, object>();

        return track;
    }

    public override bool TryAddTrack(Track track)
    {
        if (track.Header.Master != this) return false;
        if (Tracks.Contains(track)) return false;

        Tracks.Add(track);

        //Some data in the track... needs to be written
        track.Header.Data = new(new byte[track.DataStream.Length]);

        //Copy any dataStream in the track to the dataStream in the header.
        track.DataStream.CopyTo(track.Header.DataStream);

        //Write the header
        Write(track.Header);

        return true;
    }
}