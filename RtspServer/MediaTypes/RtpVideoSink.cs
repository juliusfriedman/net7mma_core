using Media.Common.Collections.Generic;
using Media;
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

    protected readonly int sourceId;

    protected ConcurrentLinkedQueueSlim<RtpFrame> m_Frames = new ConcurrentLinkedQueueSlim<RtpFrame>();

    protected int m_FramesPerSecondCounter = 0;

    #endregion

    #region Propeties

    public virtual double FramesPerSecond { get { return Math.Max(m_FramesPerSecondCounter, 1) / Math.Abs(Uptime.TotalSeconds); } }

    public virtual int Width { get; protected set; } //EnsureDimensios

    public virtual int Height { get; protected set; }

    public virtual int Quality { get; protected set; }

    public virtual bool Interlaced { get; protected set; }

    //Should also allow payloadsize e.g. BytesPerPacketPayload to be set here?

    #endregion

    public RtpVideoSink(string name, Uri source) : base(name, source)
    {
    }
}
