﻿/*
This file came from Managed Media Aggregation, You can always find the latest version @ https://github.com/juliusfriedman/net7mma_core
  
 Julius.Friedman@gmail.com / (SR. Software Engineer ASTI Transportation Inc. https://www.asti-trans.com)

Permission is hereby granted, free of charge, 
 * to any person obtaining a copy of this software and associated documentation files (the "Software"), 
 * to deal in the Software without restriction, 
 * including without limitation the rights to :
 * use, 
 * copy, 
 * modify, 
 * merge, 
 * publish, 
 * distribute, 
 * sublicense, 
 * and/or sell copies of the Software, 
 * and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 * 
 * 
 * JuliusFriedman@gmail.com should be contacted for further details.

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
 * 
 * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
 * DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, 
 * TORT OR OTHERWISE, 
 * ARISING FROM, 
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 * 
 * v//
 */

//https://tools.ietf.org/html/rfc6184

using System.Linq;

namespace Media.Rtsp.Server.MediaTypes
{

    /// <summary>
    /// Provides an implementation of <see href="https://tools.ietf.org/html/rfc6190">RFC6190</see> which is used for H.264/SVC Encoded video.
    /// </summary>
    public class RFC6190Media : RFC6184Media
    {
        public class RFC6190Frame : RFC6184Media.RFC6184Frame
        {

            #region Constrcutor

            public RFC6190Frame(byte payloadType) : base(payloadType) { }

            public RFC6190Frame(Rtp.RtpFrame existing) : base(existing) { }

            public RFC6190Frame(RFC6184Frame f) : base(f) { }

            public RFC6190Frame(RFC6190Frame f) : base(f) { }

            #endregion

            protected internal override void ProcessPacket(Rtp.RtpPacket packet, bool ignoreForbiddenZeroBit = true, bool fullStartCodes = false)
            {

                //Determine if the forbidden bit is set and the type of nal from the first byte
                byte firstByte = packet.Payload[packet.HeaderOctets];

                byte nalUnitType = (byte)(firstByte & Common.Binary.FiveBitMaxValue);

                //Determine if extended nals are present
                switch (nalUnitType)
                {
                    case Media.Codecs.Video.H264.NalUnitType.Prefix: //Prefix Nal
                        {
                            return;
                        }
                    case Media.Codecs.Video.H264.NalUnitType.SequenceParameterSetSubset: // Subset sequence parameter set
                        {
                            return;
                        }
                    case Media.Codecs.Video.H264.NalUnitType.SliceExtension: // Coded slice in scalable extension
                        {
                            return;
                        }
                    case Media.Codecs.Video.H264.NalUnitType.PayloadContentScalabilityInformation: // PACSI NAL unit
                        {
                            //if more than 10 bytes present containesSei = true.

                            //Skip 10 bytes

                            //read nal unit size

                            //Write nal unit size.

                            return;
                        }
                    case Media.Codecs.Video.H264.NalUnitType.NonInterleavedMultiTimeAggregation: // Empty, NT-MTAP etc.
                        {
                            //Get subType
                            //Read nal unit size
                            //skip TS offset (2 bytes)
                            //Skip DON if present
                            return;
                        }
                    default:
                        {
                            //Handle as per RFC6184
                            base.ProcessPacket(packet, ignoreForbiddenZeroBit, fullStartCodes);
                            return;
                        }
                }
            }
        }

        #region Constructor

        public RFC6190Media(int width, int height, string name, string directory = null, bool watch = true)
            : base(width, height, name, directory, watch) { }

        #endregion

        #region Methods

        public override void Start()
        {
            if (RtpClient is not null) return;

            base.Start();

            //Remove H264 Track
            SessionDescription.RemoveMediaDescription(0);
            RtpClient.TransportContexts.Clear();

            //Add a MediaDescription to our Sdp on any available port for RTP/AVP Transport using the given payload type            
            SessionDescription.Add(new Sdp.MediaDescription(Sdp.MediaType.video, Rtp.RtpClient.RtpAvpProfileIdentifier, 96, 0));

            //Add the control line and media attributes to the Media Description
            SessionDescription.MediaDescriptions.First().Add(new Sdp.SessionDescriptionLine("a=control:trackID=1"));
            SessionDescription.MediaDescriptions.First().Add(new Sdp.SessionDescriptionLine("a=rtpmap:96 H264-SVC/90000"));
            //SessionDescription.MediaDescriptions.First().Add(new Sdp.SessionDescriptionLine("a=fmtp:96 profile-level-id=" + Common.Binary.ReadU24(sps, 4, Media.Common.Binary.IsBigEndian).ToString("X2") + ";sprop-parameter-sets=" + Convert.ToBase64String(sps, 4, sps.Length - 4) + ',' + Convert.ToBase64String(pps, 4, pps.Length - 4)));

            RtpClient.TryAddContext(new Rtp.RtpClient.TransportContext(0, 1, SourceId, SessionDescription.MediaDescriptions.First(), false, SourceId));
        }

        #endregion
    }
}