using Media;
using Media.Codec.Interfaces;
using Media.Common;
using Media.Common.Collections.Generic;
using Media.Common.Extensions.IPEndPoint;
using Media.Common.Extensions.TimeSpan;
using Media.Rtp;
using Media.Sdp;
using Media.Sdp.Lines;
using System;
using System.Collections.Generic;

namespace Media.Rtsp.Server.MediaTypes;

public abstract class RtpAudioSink : RtpSink
{
    RtpClient m_RtpClient;

    protected ConcurrentLinkedQueueSlim<RtpFrame> m_Frames = new ConcurrentLinkedQueueSlim<RtpFrame>();

    protected readonly int sourceId;

    internal protected int m_FramesSentCounter = 0;

    /// <summary>
    /// The number of channels in this audio sink.
    /// </summary>
    public int Channels { get; protected set; }

    /// <summary>
    /// The clock rate of this audio sink.
    /// </summary>
    public int ClockRate { get; protected set; }

    /// <summary>
    /// The Payload type of this sink
    /// </summary>
    public int PayloadType { get; }

    /// <summary>
    /// The coded used to encode or decode
    /// </summary>
    public ICodec Codec { get; protected set; }

    /// <summary>
    /// Creates an audio sink and assigns <see cref="PayloadType"/>, <see cref="Channels"/> and <see cref="ClockRate"/>
    /// </summary>
    /// <param name="name"><inheritdoc/></param>
    /// <param name="source"><inheritdoc/></param>
    /// <param name="payloadType"></param>
    /// <param name="channels"></param>
    /// <param name="clockRate"></param>
    public RtpAudioSink(string name, Uri source, int payloadType, int channels, int clockRate) : base(name, source)
    {
        sourceId = RFC3550.Random32(PayloadType ^ Channels); //Doesn't really matter what seed was used

        Channels = channels;

        PayloadType = payloadType;

        ClockRate = clockRate;
    }

    /// <summary>
    /// Logic is general enough to go into RtpSink but RtpSink is working with packets right now.
    /// </summary>
    internal override void SendPackets()
    {
        m_RtpClient.FrameChangedEventsEnabled = false;

        unchecked
        {
            while (State == StreamState.Started)
            {
                try
                {
                    if (m_Frames.Count == 0 && State == StreamState.Started)
                    {
                        if (m_RtpClient.IsActive) m_RtpClient.m_WorkerThread.Priority = System.Threading.ThreadPriority.Lowest;

                        System.Threading.Thread.Sleep(ClockRate);

                        continue;
                    }

                    //int period = (clockRate * 1000 / m_Frames.Count);

                    //Dequeue a frame or die
                    RtpFrame frame;

                    if (!m_Frames.TryDequeue(out frame) || IDisposedExtensions.IsNullOrDisposed(frame) || frame.IsEmpty) continue;

                    //Get the transportChannel for the packet
                    RtpClient.TransportContext transportContext = m_RtpClient.GetContextBySourceId(frame.SynchronizationSourceIdentifier);

                    //If there is a context
                    if (transportContext != null)
                    {
                        //Increase priority
                        m_RtpClient.m_WorkerThread.Priority = System.Threading.ThreadPriority.AboveNormal;

                        transportContext.RtpTimestamp += ClockRate;

                        frame.Timestamp = (int)transportContext.RtpTimestamp;

                        //Fire a frame changed event manually
                        if (m_RtpClient.FrameChangedEventsEnabled) m_RtpClient.OnRtpFrameChanged(frame, transportContext, true);

                        //Take all the packet from the frame                            
                        IEnumerable<RtpPacket> packets = frame;

                        if (Loop) frame = new RtpFrame();

                        //Iterate each packet in the frame
                        foreach (RtpPacket packet in packets)
                        {
                            //Copy the values before we signal the server
                            //packet.Channel = transportContext.DataChannel;
                            packet.SynchronizationSourceIdentifier = (int)sourceId;

                            packet.Timestamp = transportContext.RtpTimestamp;

                            //Assign next sequence number
                            switch (transportContext.RecieveSequenceNumber)
                            {
                                case ushort.MaxValue:
                                    packet.SequenceNumber = transportContext.RecieveSequenceNumber = 0;
                                    break;
                                //Increment the sequence number on the transportChannel and assign the result to the packet
                                default:
                                    packet.SequenceNumber = ++transportContext.RecieveSequenceNumber;
                                    break;
                            }

                            //Fire an event so the server sends a packet to all clients connected to this source
                            if (false == m_RtpClient.FrameChangedEventsEnabled) m_RtpClient.OnRtpPacketReceieved(packet, transportContext);

                            //Put the packet back to ensure the timestamp and other values are correct.
                            if (Loop) frame.Add(packet);

                            //Update the jitter and timestamp
                            transportContext.UpdateJitterAndTimestamp(packet);
                        }

                        packets = null;

                        //Check for if previews should be updated?
                        //if (DecodeFrames)
                        //{
                        //    //Codec.Decoder
                        //}

                        ++m_FramesSentCounter;
                    }

                    //If we are to loop images then add it back at the end
                    if (Loop)
                    {
                        m_Frames.Enqueue(frame);
                    }
                    else
                    {
                        frame.Dispose();
                    }

                    m_RtpClient.m_WorkerThread.Priority = System.Threading.ThreadPriority.BelowNormal;

                    System.Threading.Thread.Sleep(ClockRate);
                }
                catch (Exception ex)
                {
                    if (ex is System.Threading.ThreadAbortException)
                    {
                        //Handle the abort
                        System.Threading.Thread.ResetAbort();

                        Stop();

                        return;
                    }
                    
                    continue;
                }
            }
        }
    }

    /// <summary>
    /// Implements creating the <see cref="RtpClient"/> and <see cref="SessionDescription"/> required for this audio sink.
    /// </summary>
    public override void Start()
    {
        if (m_RtpClient != null) return;

        //Create a RtpClient so events can be sourced from the Server to many clients without this Client knowing about all participants
        //If this class was used to send directly to one person it would be setup with the recievers address
        m_RtpClient = new RtpClient();

        SessionDescription = new SessionDescription(0, "v√ƒ", Name);
        SessionDescription.Add(new SessionConnectionLine()
        {
            ConnectionNetworkType = SessionConnectionLine.InConnectionToken,
            ConnectionAddressType = SessionDescription.WildcardString,
            ConnectionAddress = System.Net.IPAddress.Any.ToString()
        });

        //Add a MediaDescription to our Sdp on any available port for RTP/AVP Transport using the PayloadType            
        var mediaDescription = new MediaDescription(MediaType.audio,
            0,  //Any port...
            RtpClient.RtpAvpProfileIdentifier,
            PayloadType);
        SessionDescription.Add(mediaDescription);

        //Indicate control to each media description contained
        SessionDescription.Add(new SessionDescriptionLine("a=control:*"));

        //Ensure the session members know they can only receive
        SessionDescription.Add(new SessionDescriptionLine("a=sendonly")); //recvonly?

        //that this a broadcast.
        SessionDescription.Add(new SessionDescriptionLine("a=type:broadcast"));


        //Add a Interleave (We are not sending Rtcp Packets becaues the Server is doing that) We would use that if we wanted to use this AudioStream without the server.            
        //See the notes about having a Generic.Dictionary to support various tracks

        //Create a context
        m_RtpClient.TryAddContext(new RtpClient.TransportContext(
            0, //Data Channel
            1, //Control Channel
            RFC3550.Random32(PayloadType), //A randomId which was alredy generated 
            mediaDescription, //This is the media description we just created.
            false, //Don't enable Rtcp reports because this source doesn't communicate with any clients
            sourceId, // This context is not in discovery
            2,
            true)
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

        //Add the control line, could be anything... this indicates the URI which will appear in the SETUP and PLAY commands
        mediaDescription.Add(new SessionDescriptionLine("a=control:trackID=audio"));

        //Finally the state is set to Started so the stream can be consumed
        base.Start();
    }

    /// <summary>
    /// Stops the audio stink
    /// </summary>
    public override void Stop()
    {
        base.Stop();

        m_Frames.Clear();

        SessionDescription = null;
    }
}
