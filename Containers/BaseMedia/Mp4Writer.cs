using System.Text;
using System;
using Media.Container;

namespace Media.Containers.BaseMedia;

/// <summary>
/// Useful to support (fragmented) writing.
/// </summary>
public class Mp4Writer : BaseMediaWriter
{
    public Mp4Writer(Uri fileName)
        : base(fileName)
    {
        // Write the "ftyp" box
        WriteFtypBox();
    }

    public void WriteBox(string boxType, byte[] data)
    {
        // Write the box header
        WriteInt32LittleEndian(data.Length + Mp4Box.HeaderSize);
        Write(Encoding.UTF8.GetBytes(boxType));

        // Write the box data
        Write(data);
    }

    public void WriteFtypBox()
    {
        uint majorBrand = 0x69736F6D; // "isom"
        uint minorVersion = 0;
        uint[] compatibleBrands = new uint[] { 0x69736F6D, 0x61766331 }; // "isom", "avc1"
        FtypBox ftypBox = new FtypBox(this, majorBrand, minorVersion, compatibleBrands);
        Write(ftypBox);
    }

    public void WriteMoovBox(TimeSpan duration, uint timeScale, int trackId, int sampleCount, int[] sampleSizes, TimeSpan[] sampleTimestamps)
    {
        // Create the moov box
        MoovBox moovBox = new MoovBox(this);

        // Create the mvhd box
        MvhdBox mvhdBox = new MvhdBox(this, (uint)duration.Ticks, timeScale, 3, 4, null, null, 5);
        moovBox.AddChildBox(mvhdBox);

        var hdlrBox = new HdlrBox(this, 0);

        var minfBox = new MinfBox(this);

        MdhdBox mdhdBox = new MdhdBox(this, 0, 1, 2, 3, "", 5);

        // Create the trak box
        TrakBox trakBox = new TrakBox(this, new MdiaBox(this, mdhdBox, hdlrBox, minfBox));
        moovBox.AddChildBox(trakBox);

        // Create the tkhd box
        TkhdBox tkhdBox = new TkhdBox(this, 1, 0);
        trakBox.AddChildBox(tkhdBox);

        // Create the mdia box        
        MdiaBox mdiaBox = new MdiaBox(this, mdhdBox, new HdlrBox(this, 0), minfBox);
        trakBox.AddChildBox(mdiaBox);

        // Create the vmhd box
        VmhdBox vmhdBox = new VmhdBox(this);
        mdiaBox.MinfBox.AddChildBox(vmhdBox);

        // Create the dinf box
        DinfBox dinfBox = new DinfBox(this);
        mdiaBox.MinfBox.AddChildBox(dinfBox);

        // Create the dref box
        DrefBox drefBox = new DrefBox(this);
        dinfBox.AddChildBox(drefBox);

        // Create the url box
        drefBox.AddDataReference("");

        // Create the stbl box
        StblBox stblBox = new StblBox(this);
        mdiaBox.MinfBox.AddChildBox(stblBox);

        // Create the stsd box
        StsdBox stsdBox = new StsdBox(this);
        stblBox.AddChildBox(stsdBox);

        // Create the avc1 box
        Avc1Box avc1Box = new Avc1Box(this, new byte[0]); // Replace with actual avcC data
        stsdBox.AddSampleEntry(avc1Box);

        // Create the stts box
        SttsBox sttsBox = new SttsBox(this);
        foreach (TimeSpan timestamp in sampleTimestamps)
        {
            sttsBox.AddTimeToSampleEntry(1, (int)timestamp.TotalMilliseconds * (int)timeScale / 1000);
        }
        stblBox.AddChildBox(sttsBox);

        // Create the stsz box
        StszBox stszBox = new StszBox(this);
        foreach (int size in sampleSizes)
        {
            stszBox.AddSampleSize(size);
        }
        stblBox.AddChildBox(stszBox);

        // Create the stco box
        StcoBox stcoBox = new StcoBox(this);
        uint offset = 0;
        foreach (int size in sampleSizes)
        {
            stcoBox.AddChunkOffset(offset);
            offset += (uint)size;
        }
        stblBox.AddChildBox(stcoBox);

        // Write the moov box to the file
        AddBox(moovBox);
    }

    public void WriteMoofBox(uint sequenceNumber, int trackId, TimeSpan baseMediaDecodeTime, int[] sampleSizes, uint[] sampleFlags)
    {
        // Create "moof" box
        MoofBox moofBox = new MoofBox(this);

        // Create "mfhd" box
        MfhdBox mfhdBox = new MfhdBox(this, sequenceNumber);

        // Create "traf" box
        TrafBox trafBox = new TrafBox(this);

        // Create "tfhd" box
        TfhdBox tfhdBox = new TfhdBox(this, (uint)trackId, (uint)baseMediaDecodeTime.Ticks / 10, 0, 0);

        // Create "tfdt" box
        TfdtBox tfdtBox = new TfdtBox(this, (uint)baseMediaDecodeTime.Ticks / 10);

        // Create "trun" box
        TrunBox trunBox = new TrunBox(this, (uint)sampleSizes.Length, 0, 0);
        //trunBox.SampleSizes = sampleSizes;
        trunBox.SampleFlags = sampleFlags;

        // Add boxes to their parent boxes
        trafBox.AddChildBox(tfhdBox);
        trafBox.AddChildBox(tfdtBox);
        trafBox.AddChildBox(trunBox);
        moofBox.AddChildBox(mfhdBox);
        moofBox.AddChildBox(trafBox);

        // Write "moof" box
        AddBox(moofBox);
    }

    public void AddAudioTrack(uint sampleRate, ushort channelCount, int[] sampleSizes, TimeSpan[] sampleDurations)
    {
        if (sampleRate == 0 || channelCount == 0)
            throw new ArgumentException("Invalid sample rate or channel count");

        var mdhdBox = new MdhdBox(this, 0, sampleRate, 0, 0, "", 0);
        var hdlrBox = new HdlrBox(this, 0); // Modify handler type as needed

        var minfBox = new MinfBox(this);
        minfBox.AddChildBox(mdhdBox);
        minfBox.AddChildBox(hdlrBox);

        var dinfBox = new DinfBox(this);
        minfBox.AddChildBox(dinfBox);

        var drefBox = new DrefBox(this);
        dinfBox.AddChildBox(drefBox);

        drefBox.AddDataReference(""); // Add data reference as needed

        var stblBox = new StblBox(this);
        minfBox.AddChildBox(stblBox);

        var stsdBox = new StsdBox(this);
        stblBox.AddChildBox(stsdBox);

        // Create the audio sample entry box (e.g., Mp4aBox or other suitable class)
        var audioSampleEntry = CreateAudioSampleEntry(sampleRate, channelCount);

        stsdBox.AddSampleEntry(audioSampleEntry);

        // Create the sample size box (stsz)
        var stszBox = new StszBox(this);
        foreach (int size in sampleSizes)
        {
            stszBox.AddSampleSize(size);
        }
        stblBox.AddChildBox(stszBox);

        // Create the time-to-sample box (stts)
        var sttsBox = new SttsBox(this);
        foreach (TimeSpan duration in sampleDurations)
        {
            // Convert duration to appropriate time scale
            uint timeScale = sampleRate; // Adjust as needed
            uint durationInTimeScale = (uint)(duration.TotalSeconds * timeScale);
            sttsBox.AddTimeToSampleEntry(1, (int)durationInTimeScale);
        }
        stblBox.AddChildBox(sttsBox);

        // Create the chunk offset box (stco)
        var stcoBox = new StcoBox(this);
        uint offset = 0;
        foreach (int size in sampleSizes)
        {
            stcoBox.AddChunkOffset(offset);
            offset += (uint)size;
        }
        stblBox.AddChildBox(stcoBox);

        var trakBox = new TrakBox(this, new MdiaBox(this, mdhdBox, hdlrBox, minfBox));
        AddBox(trakBox);

        Tracks.Add(new Track(trakBox, "", 1, DateTime.UtcNow, DateTime.UtcNow, 0, 0, 0, TimeSpan.Zero, TimeSpan.Zero, 0, Sdp.MediaType.audio, null, 1, 8, true));
    }

    private Mp4aBox CreateAudioSampleEntry(uint sampleRate, ushort channelCount)
    {
        var audioSampleEntry = new Mp4aBox(this);
        audioSampleEntry.EntryVersion = 0;
        audioSampleEntry.ChannelCount = channelCount;
        audioSampleEntry.SampleSize = 16; // 16-bit samples
        audioSampleEntry.CompressionId = 0; // No compression
        audioSampleEntry.PacketSize = 0; // 0 for uncompressed audio
        audioSampleEntry.SampleRate = sampleRate;

        // Set other audio-specific properties as needed

        return audioSampleEntry;
    }

}