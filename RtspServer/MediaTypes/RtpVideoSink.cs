using Media.Common.Collections.Generic;
using Media.Rtp;
using Media.Rtsp.Server.MediaTypes;
using System;

namespace RtspServer.MediaTypes;

public class RtpVideoSink : RtpSink
{
    #region Fields

    //Should be moved to SourceStream? Should have Fps and calculate for developers?
    //Should then be a TimeSpan or Frequency
    internal protected int ClockRate = 9;//kHz //90 dekahertz

    protected ConcurrentLinkedQueueSlim<RtpFrame> Frames = new ConcurrentLinkedQueueSlim<RtpFrame>();

    protected int FramesPerSecondCounter = 0;

    #endregion

    #region Propeties

    public double FramesPerSecond { get { return Math.Max(FramesPerSecondCounter, 1) / Math.Abs(Uptime.TotalSeconds); } }

    public virtual int Width { get; protected set; } //EnsureDimensios

    public virtual int Height { get; protected set; }

    public virtual int Quality { get; protected set; }

    public virtual bool Interlaced { get; protected set; }

    //Should also allow payloadsize e.g. BytesPerPacketPayload to be set here?

    #endregion

    #region Constructor

    public RtpVideoSink(string name, Uri source, int width, int height, bool interlaced, int quality)
            : base(name, source)
    {
        Width = width;

        Height = height;

        Interlaced = interlaced;

        Quality = quality;
    }

    public RtpVideoSink(string name, Uri source) 
        : base(name, source)
    {
    }

    #endregion
}
