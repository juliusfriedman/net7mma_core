using Media.Common;
using Media.Container;
using System;
using System.Collections.Generic;
using System.IO;
using static Media.Containers.Riff.RiffReader;

public class RiffWriter : MediaFileWriter
{
    private long videoFrameIndex;
    private readonly int width;
    private readonly int height;
    private readonly int framesPerSecond;
    private readonly uint dataRate;
    private readonly uint bufferSize;

    public override Node Root => throw new NotImplementedException();

    public override Node TableOfContents => throw new NotImplementedException();

    public RiffWriter(Uri location, int width, int height, int framesPerSecond)
        : base(location, FileAccess.Write)
    {
        // Set AVI-specific properties
        this.width = width;
        this.height = height;
        this.framesPerSecond = framesPerSecond;
        this.dataRate = 1000000 / (uint)framesPerSecond; // 1 second in microseconds
        this.bufferSize = (uint)(width * height * 3); // RGB24 format

        // Write AVI file header
        WriteHeader();
    }

    // Helper method to write a 4-byte integer value to the stream
    private void WriteInt32(int value)
    {
        WriteByte((byte)(value & 0xFF));
        WriteByte((byte)((value >> 8) & 0xFF));
        WriteByte((byte)((value >> 16) & 0xFF));
        WriteByte((byte)((value >> 24) & 0xFF));
    }

    protected void WriteInt16(short value)
    {
        WriteByte((byte)value);
        WriteByte((byte)(value >> 8));
    }

    public override void WriteHeader()
    {
        // Ensure the file stream is open
        if (BaseStream == null || !BaseStream.CanWrite)
            throw new InvalidOperationException("The file stream is not open for writing.");

        // Write the RIFF header
        WriteRiffHeader();

        // Write the AVI header
        WriteAviHeader();

        // Write the video stream header
        WriteVideoStreamHeader(width, height, framesPerSecond, FourCharacterCode.MJPG);

        // Write the audio stream header (if audio is supported)
        WriteAudioStreamHeader();
    }

    private void WriteRiffHeader()
    {
        // Write the 'RIFF' chunk ID
        WriteInt32((int)FourCharacterCode.RIFF);

        // Placeholder for the RIFF chunk size (will be updated later)
        long riffSizePos = BaseStream.Position;
        WriteInt32(0); // 0 size for now

        // Write the 'AVI ' chunk type
        WriteInt32((int)FourCharacterCode.AVI);
    }

    private void WriteAviHeader()
    {
        // Write the 'LIST' chunk ID
        WriteInt32((int)FourCharacterCode.LIST);

        // Placeholder for the 'LIST' chunk size (will be updated later)
        long listSizePos = BaseStream.Position;
        WriteInt32(0); // 0 size for now

        // Write the 'hdlr' chunk type
        WriteInt32((int)FourCharacterCode.hdlr);
    }

    private void WriteVideoStreamHeader(int width, int height, double frameRate, FourCharacterCode codecCode)
    {
        // Write the 'strh' chunk ID
        WriteInt32((int)FourCharacterCode.strh);

        // Write the 'strh' chunk size (set to 56 for video stream header)
        WriteInt32(56);

        // Write the 'vids' four character code (video stream type)
        WriteInt32((int)FourCharacterCode.vids);

        // Write the 'fccHandler' (four character code) for MJPEG codec
        WriteInt32((int)codecCode);

        // Write the flags (set to 0 for uncompressed video)
        WriteInt32(0);

        // Write the priority, language, and initial frames (all set to 0)
        WriteInt32(0);
        WriteInt32(0);
        WriteInt32(0);

        // Write the scale and rate (frame rate in frames per second)
        int scale = 1;
        int rate = (int)(frameRate * scale);
        WriteInt32(scale);
        WriteInt32(rate);

        // Write the start time and length (both set to 0)
        WriteInt32(0);
        WriteInt32(0);

        // Write the suggested buffer size and quality (both set to 0)
        WriteInt32(0);
        WriteInt32(0);

        // Write the rectangle (video dimensions)
        WriteInt32(0);
        WriteInt32(0);
        WriteInt32(width);
        WriteInt32(height);

        // Write the 'strf' chunk ID
        WriteInt32((int)FourCharacterCode.strf);

        // Write the 'strf' chunk size (set to 40 for MJPEG video stream format)
        WriteInt32(40);

        // Write the biSize (size of BITMAPINFOHEADER structure, set to 40 for MJPEG)
        WriteInt32(40);

        // Write the biWidth and biHeight (video dimensions)
        WriteInt32(width);
        WriteInt32(height);

        // Write the biPlanes (set to 1)
        WriteInt16(1);

        // Write the biBitCount (bits per pixel, set to 24 for MJPEG)
        WriteInt16(24);

        // Write the biCompression (four character code, set to MJPG for MJPEG)
        WriteInt32((int)FourCharacterCode.MJPG);

        // Write the biSizeImage (set to 0 for MJPEG)
        WriteInt32(0);

        // Write the biXPelsPerMeter and biYPelsPerMeter (both set to 0)
        WriteInt32(0);
        WriteInt32(0);

        // Write the biClrUsed and biClrImportant (both set to 0)
        WriteInt32(0);
        WriteInt32(0);
    }

    private void WriteAudioStreamHeader()
    {
        // Write the 'strl' chunk ID
        WriteInt32((int)FourCharacterCode.strl);

        // Placeholder for the 'strl' chunk size (will be updated later)
        long strlSizePos = BaseStream.Position;
        WriteInt32(0); // 0 size for now

        // Write the 'strh' chunk ID
        WriteInt32((int)FourCharacterCode.strh);

        // Write the 'strh' chunk size (always 56 for audio stream)
        WriteInt32(56);

        // Write the audio stream header data (AVIStreamHeader structure)
        // (Not implemented in this example)
        // You need to write the AVIStreamHeader structure based on the audio codec you're using.

        // Update the size of the 'strl' chunk
        long endPos = BaseStream.Position;
        int strlSize = (int)(endPos - strlSizePos - 4);
        BaseStream.Position = strlSizePos;
        WriteInt32(strlSize);
        BaseStream.Position = endPos;
    }

    public override void WriteVideoFrame(byte[] frameData)
    {
        // Write video frame data
        // (Not implemented in this example)
        // This method will handle writing video frame data in the AVI format
        // It may involve writing chunk data, video stream data, frame headers, etc.
    }

    public override void WriteAudioSamples(byte[] audioData)
    {
        // Write audio samples
        // (Not implemented in this example)
        // This method will handle writing audio samples in the AVI format
        // It may involve writing chunk data, audio stream data, sample headers, etc.
    }

    public override void Close()
    {
        // Write AVI file footer and cleanup
        // (Not implemented in this example)

        base.Close();
    }

    public override IEnumerator<Node> GetEnumerator()
    {
        throw new NotImplementedException();
    }

    public override IEnumerable<Track> GetTracks()
    {
        throw new NotImplementedException();
        //yield return new Track(Root, "", 1, create)
    }

    public override SegmentStream GetSample(Track track, out TimeSpan duration)
    {
        //Should use the reader?
        throw new NotImplementedException();
    }
}