using Media.Common.Collections.Generic;
using Media.Common.Extensions.IPEndPoint;
using Media.Common.Extensions.TimeSpan;
using Media.Rtp;
using Media.Sdp;
using Media.Sdp.Lines;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Media.Rtsp.Server.MediaTypes;

public class RtpVideoSink : RtpSink
{
    #region Fields

    //Should be moved to SourceStream? Should have Fps and calculate for developers?
    //Should then be a TimeSpan or Frequency
    internal protected int ClockRate = 9;//kHz //90 dekahertz

    internal protected ConcurrentLinkedQueueSlim<RtpFrame> Frames = new();

    protected ulong FramesPerSecondCounter = 0;

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

    public RtpVideoSink(string name, Sdp.SessionDescription sessionDescription)
        : base(name, source: null)
    {
        ArgumentNullException.ThrowIfNull(sessionDescription);

        SessionDescription = sessionDescription;
    }

    #endregion

    #region Methods

    public override void Start()
    {
        if (RtpClient is not null) return;

        //Create a RtpClient so events can be sourced from the Server to many clients without
        //this Client knowing about all participants
        //If this class was used to send directly to one person it would be setup with the
        //recievers address
        RtpClient = new RtpClient();

        MediaDescription mediaDescription = null;

        if (SessionDescription is null)
        {
            //Add a MediaDescription to our Sdp on any available port for RTP/AVP Transport
            //using the PayloadType
            mediaDescription = new MediaDescription(MediaType.video,
                RtpClient.RtpAvpProfileIdentifier,
                mediaFormat: 97,
                mediaPort: 0)   //Any port...
            {
                //Add the control line, could be anything...
                //This indicates the URI which will appear in the SETUP and PLAY commands
                new SessionDescriptionLine("a=control:trackID=video")
            };

            SessionDescription = new SessionDescription(0, "v√ƒ", Name)
            {
                new SessionConnectionLine
                {
                    ConnectionNetworkType = SessionConnectionLine.InConnectionToken,
                    ConnectionAddressType = SessionDescription.WildcardString,
                    ConnectionAddress = System.Net.IPAddress.Any.ToString()
                },

                //Add the MediaDescription
                mediaDescription,

                //Indicate control to each media description contained
                new SessionDescriptionLine("a=control:*"),

                //Ensure the session members know they can only receive
                new SessionDescriptionLine("a=sendonly"),

                //that this a broadcast.
                new SessionDescriptionLine("a=type:broadcast")
            };
        }
        else
        {
            mediaDescription = SessionDescription.MediaDescriptions.First();
        }

        //Add a Interleave (We are not sending Rtcp Packets becaues the Server is doing that)
        //We would use that if we wanted to use this AudioStream without the server.
        //See the notes about having a Generic.Dictionary to support various tracks

        //Create a context
        RtpClient.TryAddContext(new RtpClient.TransportContext(
            dataChannel: 0, //Data Channel
            controlChannel: 1, //Control Channel
            ssrc: RFC3550.Random32(97), //A randomId which was alredy generated 
            mediaDescription, //This is the media description we just created.
            rtcpEnabled: false, //Don't enable Rtcp reports because this source doesn't communicate with any clients
            senderSsrc: RFC3550.Random32(97), // This context is not in discovery
            minimumSequentialRtpPackets: 0,
            shouldDispose: true)
        {
            //Never has to send
            SendInterval = TimeSpanExtensions.InfiniteTimeSpan,
            //Never has to recieve
            ReceiveInterval = TimeSpanExtensions.InfiniteTimeSpan,
            //Assign a LocalRtp so IsActive is true
            LocalRtp = IPEndPointExtensions.Any,
            //Assign a RemoteRtp so IsActive is true
            RemoteRtp = IPEndPointExtensions.Any
        }); //This context is always valid from the first rtp packet received

        //Make the thread
        var thread = new System.Threading.Thread(SendPackets)
        {
            //IsBackground = true;
            //Priority = System.Threading.ThreadPriority.BelowNormal;
            Name = nameof(RtpVideoSink) + "-" + Id
        };
        thread.TrySetApartmentState(System.Threading.ApartmentState.MTA);

        IsReady = true;
        State = StreamState.Started;
        RtpClient.m_WorkerThread = thread;
        RtpClient.m_WorkerThread.Start();

        //Finally the state is set to Started so the stream can be consumed
        base.Start();
    }

    /// <summary>
    /// Stops the audio stink
    /// </summary>
    public override void Stop()
    {
        base.Stop();

        Frames.Clear();

        SessionDescription = null;
    }

    internal override void SendPackets()
    {
        RtpClient.FrameChangedEventsEnabled = false;

        while (State is StreamState.Started)
        {
            try
            {
                if (Frames.Count is 0 && State is StreamState.Started)
                {
                    if (RtpClient.IsActive)
                        RtpClient.m_WorkerThread.Priority = System.Threading.ThreadPriority.Lowest;

                    System.Threading.Thread.Sleep(ClockRate);
                    continue;
                }

                //int period = (clockRate * 1000 / m_Frames.Count);

                //Dequeue a frame or die
                if (!Frames.TryDequeue(out RtpFrame frame) ||
                    Common.IDisposedExtensions.IsNullOrDisposed(frame) ||
                    frame.IsEmpty) continue;

                //Get the transportChannel for the packet
                Rtp.RtpClient.TransportContext transportContext =
                    RtpClient.GetContextBySourceId(frame.SynchronizationSourceIdentifier);

                //If there is a context
                if (transportContext is not null)
                {
                    //Increase priority
                    RtpClient.m_WorkerThread.Priority = System.Threading.ThreadPriority.AboveNormal;

                    //Ensure HasRecievedRtpWithinSendInterval is true
                    //transportContext.m_LastRtpIn = DateTime.UtcNow;

                    transportContext.RtpTimestamp += ClockRate * 1000;

                    frame.Timestamp = transportContext.RtpTimestamp;

                    //Fire a frame changed event manually
                    if (RtpClient.FrameChangedEventsEnabled)
                        RtpClient.OnRtpFrameChanged(frame, transportContext, true);

                    //Todo, should not copy packets

                    //Take all the packet from the frame
                    IEnumerable<Rtp.RtpPacket> packets = frame;

                    //Clear the frame to reset sequence numbers (could add method to do this)
                    //frame.RemoveAllPackets();

                    if (Loop) frame = [];

                    //Todo, should provide access to property or provide a method which updates this property.

                    //Iterate each packet in the frame
                    foreach (Rtp.RtpPacket packet in packets)
                    {
                        //Copy the values before we signal the server
                        //packet.Channel = transportContext.DataChannel;
                        packet.SynchronizationSourceIdentifier = SourceId;
                        packet.Timestamp = transportContext.RtpTimestamp;
                        //Assign next sequence number
                        packet.SequenceNumber = transportContext.RecieveSequenceNumber switch
                        {
                            ushort.MaxValue => transportContext.RecieveSequenceNumber = 0,
                            //Increment the sequence number on the transportChannel and assign the result to the packet
                            _ => ++transportContext.RecieveSequenceNumber,
                        };

                        //Fire an event so the server sends a packet to all clients connected to this source
                        if (RtpClient.FrameChangedEventsEnabled is false)
                            RtpClient.OnRtpPacketReceieved(packet, transportContext);

                        //Put the packet back to ensure the timestamp and other values are correct.
                        if (Loop) frame.Add(packet);

                        //Update the jitter and timestamp
                        transportContext.UpdateJitterAndTimestamp(packet);

                        //Todo, should provide access to property or provide a method which updates this property.

                        //Ensure HasSentRtpWithinSendInterval is true
                        //transportContext.m_LastRtpOut = DateTime.UtcNow;
                    }

                    packets = null;

                    ++FramesPerSecondCounter;
                }

                //If we are to loop images then add it back at the end
                if (Loop)
                {
                    Frames.Enqueue(frame);
                }
                else
                {
                    frame.Dispose();
                }

                RtpClient.m_WorkerThread.Priority = System.Threading.ThreadPriority.BelowNormal;

                System.Threading.Thread.Sleep(ClockRate);
            }
            catch (Exception)
            {
                continue;
            }
        }
    }

    #endregion
}
