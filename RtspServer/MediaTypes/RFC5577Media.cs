using System;
using System.Linq;

namespace Media.Rtsp.Server.MediaTypes
{
    /// <summary>
    /// Provides an implementation of <see href="https://tools.ietf.org/html/rfc5577">RFC5577</see> which is used for ITU-T Recommendation G.722.1 Audio.
    /// </summary>
    public class RFC5577Media : RFC2435Media //RtpSink
    {
        public class RFC5577Frame : Rtp.RtpFrame
        {
            public RFC5577Frame(byte payloadType) : base(payloadType) { }

            public RFC5577Frame(Rtp.RtpFrame existing) : base(existing) { }

            public RFC5577Frame(RFC5577Frame f) : this((Rtp.RtpFrame)f) { Buffer = f.Buffer; }

            public System.IO.MemoryStream Buffer { get; set; }

            public void Packetize(byte[] data, int mtu = 1500)
            {
                throw new NotImplementedException();
            }

            public void Depacketize()
            {
                /*
                 http://tools.ietf.org/html/rfc5577#page-3
                  */

                throw new NotImplementedException();
            }

            internal void DisposeBuffer()
            {
                if (Buffer is not null)
                {
                    Buffer.Dispose();
                    Buffer = null;
                }
            }

            public override void Dispose()
            {
                if (IsDisposed) return;
                base.Dispose();
                DisposeBuffer();
            }
        }

        #region Constructor

        public RFC5577Media(int width, int height, string name, string directory = null, bool watch = true)
            : base(name, directory, watch, width, height, false, 99)
        {
            Width = width;
            Height = height;
            Width += Width % 8;
            Height += Height % 8;
            ClockRate = 16000;
        }

        #endregion

        #region Methods

        public override void Start()
        {
            if (RtpClient is not null) return;

            base.Start();

            //Remove JPEG Track
            SessionDescription.RemoveMediaDescription(0);
            RtpClient.TransportContexts.Clear();

            //Add a MediaDescription to our Sdp on any available port for RTP/AVP Transport using the given payload type         
            SessionDescription.Add(new Sdp.MediaDescription(Sdp.MediaType.audio, Rtp.RtpClient.RtpAvpProfileIdentifier, 96, 0));

            //Add the control line
            SessionDescription.MediaDescriptions.First().Add(new Sdp.SessionDescriptionLine("a=control:trackID=1"));
            //Should be a field set in constructor.
            //PCMA-WB or PCMU-WB
            SessionDescription.MediaDescriptions.First().Add(new Sdp.SessionDescriptionLine("a=rtpmap:" + SessionDescription.MediaDescriptions.First().MediaFormat + " PCMA-WB/" + ClockRate));
            RtpClient.TryAddContext(new Rtp.RtpClient.TransportContext(0, 1, SourceId, SessionDescription.MediaDescriptions.First(), false, SourceId));
        }

        #endregion
    }
}
