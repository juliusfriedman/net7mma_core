﻿
using System;
using System.Diagnostics;
using System.Globalization;

namespace UnitTests.Code;

// (c) Roger Hardiman 2016, 2021

// This class uses a System Timer to generate a YUV image at regular intervals and Audio at regular intervals
// The ReceivedYUVFrame event is fired for each new YUV image
// The ReceivedAudioFrame event is fired for each chunk of Audio

public class TestCard
{

    // Events that applications can receive
    public event ReceivedYUVFrameHandler ReceivedYUVFrame;
    public event ReceivedAudioFrameHandler ReceivedAudioFrame;

    // Delegated functions (essentially the function prototype)
    public delegate void ReceivedYUVFrameHandler(uint timestamp, int width, int height, byte[] data);
    public delegate void ReceivedAudioFrameHandler(uint timestamp, short[] data);


    // Local variables
    private readonly Stopwatch stopwatch;
    private readonly System.Timers.Timer frame_timer;

    //Create the ImageFormat based on YUV packed but in Planar format with a full height luma plane and half hight chroma planes
    private readonly Media.Codecs.Image.ImageFormat Yuv420P = new(Media.Codecs.Image.ImageFormat.YUV(8, Media.Common.Binary.ByteOrder.Little, Media.Codec.DataLayout.Planar), new int[] { 0, 1, 1 });
    private readonly Media.Codecs.Image.Image yuvImage;

    private readonly byte[] yuv_frame = null;
    private int x_position = 0;
    private int y_position = 0;
    private readonly int fps = 0;
    private readonly int width = 0;
    private readonly int height = 0;
    //private Object generate_lock = new Object();
    private long frame_count = 0;

    private readonly System.Timers.Timer audio_timer;
    private const int audio_duration_ms = 20; // duration of sound samples for mono PCM audio. Hinted at in origial RTP standard from 1996 that mentions 160 audio samples
    private long audio_count = 0;

    // ASCII Font
    // Created by Roger Hardiman using an online generation tool
    // http://www.riyas.org/2013/12/online-led-matrix-font-generator-with.html

    private readonly byte[] ascii_0 = { 0x00, 0x3c, 0x42, 0x42, 0x42, 0x42, 0x42, 0x3c };
    private readonly byte[] ascii_1 = { 0x00, 0x08, 0x18, 0x28, 0x08, 0x08, 0x08, 0x3e };
    private readonly byte[] ascii_2 = { 0x00, 0x3e, 0x42, 0x02, 0x0c, 0x30, 0x40, 0x7e };
    private readonly byte[] ascii_3 = { 0x00, 0x7c, 0x02, 0x02, 0x3c, 0x02, 0x02, 0x7c };
    private readonly byte[] ascii_4 = { 0x00, 0x0c, 0x14, 0x24, 0x44, 0x7e, 0x04, 0x04 };
    private readonly byte[] ascii_5 = { 0x00, 0x7e, 0x40, 0x40, 0x7c, 0x02, 0x02, 0x7c };
    private readonly byte[] ascii_6 = { 0x00, 0x3e, 0x40, 0x40, 0x7c, 0x42, 0x42, 0x3c };
    private readonly byte[] ascii_7 = { 0x00, 0x7e, 0x02, 0x02, 0x04, 0x08, 0x10, 0x20 };
    private readonly byte[] ascii_8 = { 0x00, 0x3c, 0x42, 0x42, 0x3c, 0x42, 0x42, 0x3c };
    private readonly byte[] ascii_9 = { 0x00, 0x3c, 0x42, 0x42, 0x3c, 0x02, 0x02, 0x3e };
    private readonly byte[] ascii_colon = { 0x00, 0x00, 0x18, 0x18, 0x00, 0x18, 0x18, 0x00 };
    private readonly byte[] ascii_space = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
    private readonly byte[] ascii_dot = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0x18, 0x00 };

    // Constructor
    public TestCard(int width, int height, int fps)
    {
        this.width = width;
        this.height = height;
        this.fps = fps;

        yuvImage = new Media.Codecs.Image.Image(Yuv420P, width, height);

        yuv_frame = yuvImage.Data.Array;

        // Set all values to 127
        Array.Fill<byte>(yuv_frame, 127);

        stopwatch = new Stopwatch();
        stopwatch.Start();

        // Start timer. The Timer will generate each YUV frame
        frame_timer = new System.Timers.Timer
        {
            Interval = 1, // on first pass timer will fire straight away (cannot have zero interval)
            AutoReset = false // do not restart timer after the time has elapsed
        };
        frame_timer.Elapsed += (object sender, System.Timers.ElapsedEventArgs e) =>
        {
            // send a video frame
            Send_YUV_Frame();
            frame_count++;

            // Some CPU cycles will have been used in Sending the YUV Frame.
            // Compute the delay required (the Timer Interval) before sending the next YUV frame
            long time_for_next_tick_ms = (frame_count * 1000) / fps;
            long time_to_wait = time_for_next_tick_ms - stopwatch.ElapsedMilliseconds;
            if (time_to_wait <= 0) time_to_wait = 1; // cannot have negative or zero intervals
            frame_timer.Interval = time_to_wait;
            frame_timer.Start();
        };
        frame_timer.Start();

        // Start timer. The Timer will generate each Audio frame
        audio_timer = new System.Timers.Timer
        {
            Interval = 1, // on first pass timer will fire straight away (cannot have zero interval)
            AutoReset = false // do not restart timer after the time has elapsed
        };
        audio_timer.Elapsed += (object sender, System.Timers.ElapsedEventArgs e) =>
        {
            // send an audio frame
            Send_Audio_Frame();
            audio_count++;

            // Some CPU cycles will have been used in Sending the YUV Frame.
            // Compute the delay required (the Timer Interval) before sending the next YUV frame
            long time_for_next_tick_ms = (audio_count * audio_duration_ms); // 20ms samples is 50 audio packets per second
            long time_to_wait = time_for_next_tick_ms - stopwatch.ElapsedMilliseconds;
            if (time_to_wait <= 0) time_to_wait = 1; // cannot have negative or zero intervals
            audio_timer.Interval = time_to_wait;
            audio_timer.Start();
        };
        audio_timer.Start();
    }

    // Dispose
    public void Disconnect()
    {
        // Stop the frame timer
        frame_timer.Stop();
        frame_timer.Dispose();

        // Stop the audio timer
        audio_timer.Stop();
        audio_timer.Dispose();
    }


    private void Send_YUV_Frame()
    {
        //lock (generate_lock)
        {
            // Get the current time
            DateTime now_utc = DateTime.UtcNow;
            DateTime now_local = now_utc.ToLocalTime();

            string overlay;

            if (width >= 96)
            {
                // Need 12 characters of 8x8 pixels. 12*8 = 96
                // HH:MM:SS.mmm
                overlay = now_local.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture); // do not replace : or . by local formats
            }
            else
            {
                // Min for most video formats is 16x16, enough for 2 characters
                overlay = now_local.ToString("ss", CultureInfo.InvariantCulture); // do not replace : or . by local formats
            }

            // process each character
            int start_row = (height / 2) - 4; // start 4 pixels above the centre row (4 is half the font height)
            for (int c = 0; c < overlay.Length; c++)
            {
                byte[] font = ascii_space;
                if (overlay[c] == '0') font = ascii_0;
                if (overlay[c] == '1') font = ascii_1;
                if (overlay[c] == '2') font = ascii_2;
                if (overlay[c] == '3') font = ascii_3;
                if (overlay[c] == '4') font = ascii_4;
                if (overlay[c] == '5') font = ascii_5;
                if (overlay[c] == '6') font = ascii_6;
                if (overlay[c] == '7') font = ascii_7;
                if (overlay[c] == '8') font = ascii_8;
                if (overlay[c] == '9') font = ascii_9;
                if (overlay[c] == ' ') font = ascii_space;
                if (overlay[c] == ':') font = ascii_colon;
                if (overlay[c] == '.') font = ascii_dot;

                // process the font character
                for (int rows = 0; rows < 8; rows++)
                {
                    int y_plane_pos = (start_row * width) + (rows * width) + (c * 8);
                    byte row_byte = font[rows];
                    // bit shift the row byte into individual pixels where the font On/Off maps to Y intensity 50 or 200
                    for (int bits = 0; bits < 8; bits++)
                    {
                        if ((row_byte & 0x80) == 0x80)
                        {
                            // Pixel On
                            yuv_frame[y_plane_pos] = 200;
                        }
                        else
                        {
                            yuv_frame[y_plane_pos] = 50;
                        }
                        y_plane_pos++;
                        row_byte = (byte)(row_byte << 1); // shift up so the next 'bit' to process is the most significant bit
                    }
                }
            }

            // Toggle the pixel value
            byte pixel_value = yuv_frame[(y_position * width) + x_position];

            // change brightness of pixel			
            pixel_value = pixel_value > 128 ? (byte)30 : (byte)230;

            yuv_frame[(y_position * width) + x_position] = pixel_value;

            // move the x and y position
            x_position += 5;
            if (x_position >= width)
            {
                x_position = 0;
                y_position++;
            }

            if (y_position >= height)
            {
                y_position = 0;
            }

            // fire the Event
            if (ReceivedYUVFrame is not null)
            {
                ReceivedYUVFrame((uint)stopwatch.ElapsedMilliseconds, width, height, yuv_frame);
            }
        }
    }

    // 8 KHz audio / 20ms samples
    private const int frame_size = 8000 * audio_duration_ms / 1000;  // = 8000 / (1000/audio_duration_ms)
    private readonly short[] audio_frame = new short[frame_size]; // This is an array of 16 bit values

    private void Send_Audio_Frame()
    {
        //lock (generate_lock)
        {
            // Get the current time
            DateTime now_utc = DateTime.UtcNow;
            DateTime now_local = now_utc.ToLocalTime();

            long timestamp_ms = now_utc.Ticks / TimeSpan.TicksPerMillisecond;

            // Add beep sounds.
            // We add a 0.1 second beep every second
            // Every 10 seconds we use a different sound.
            // At the start of each new minute we add a 0.3 second beep

            // Sound 1, 0.3 seconds at 12:00:00, 12:01:00, 12:02:00
            // Sound 1, 0.1 seconds at 12:00:10, 12:00:20, 12:00:30 ... 12:00:50
            // Sound 2, 0.1 seconds at 12:00:01, 12:00:02, 12:00:03 ... 12:00:09, then 12:00:11, 12:00:12

            long currentSeconds = (timestamp_ms / 1000);
            long currentMilliSeconds = timestamp_ms % 1000;

            int soundToPlay = ((currentSeconds % 60) is 0) && (currentMilliSeconds < 300)
                ? 1
                : ((currentSeconds % 10) is 0) && (currentMilliSeconds < 100)
                ? 1
                : ((currentSeconds % 10) != 0) && (currentMilliSeconds < 100) ? 2 : 0; // 0 = silence

            // Add the sound
            for (int i = 0; i < audio_frame.Length; i++)
            {
                if (soundToPlay == 1) audio_frame[i] = (short)(i * 30000); // random-ish garbage
                else if (soundToPlay == 2) audio_frame[i] = (short)(i * 65000); // random-ish garbage
                else audio_frame[i] = 0; // or silence CNG
            }

            // Fire the Event
            if (ReceivedAudioFrame is not null)
            {
                ReceivedAudioFrame((uint)stopwatch.ElapsedMilliseconds, audio_frame);
            }
        }
    }
}
