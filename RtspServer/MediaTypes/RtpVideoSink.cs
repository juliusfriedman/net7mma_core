using Media.Rtsp.Server.MediaTypes;
using System;

namespace RtspServer.MediaTypes;

public class RtpVideoSink : RtpSink
{


    public RtpVideoSink(string name, Uri source) : base(name, source)
    {
    }
}
