#region Copyright
/*
This file came from Managed Media Aggregation, You can always find the latest version @ https://net7mma.codeplex.com/
  
 Julius.Friedman@gmail.com / (SR. Software Engineer ASTI Transportation Inc. http://www.asti-trans.com)

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
#endregion
using Media.Common;
using Media.Rtp;
using Media.Sdp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using static Media.Rtsp.RtspClient;

namespace Media.Rtsp
{
    /// <summary>
    /// Represents the resources in use by each Session created (during SETUP)
    /// </summary>
    /// <remarks>Todo, Unify by creating Session and HttpSession to wit this type shall derive hence forth</remarks>
    public class RtspSession : SuppressedFinalizerDisposable
    {
        #region Fields

        readonly RtspClient m_Client;

        //Todo, ensure used from session and remove from client level.

        /// <summary>
        /// The remote RtspEndPoint
        /// </summary>
        readonly EndPoint m_RemoteRtsp;

        /// <summary>
        /// The protcol in which Rtsp data will be transpored from the server
        /// </summary>
        readonly ProtocolType m_RtpProtocol;

        internal RtpClient m_RtpClient;

        readonly ClientProtocolType m_RtspProtocol;

        RtspMessage m_LastTransmitted;

        AuthenticationSchemes m_AuthenticationScheme;

        string m_UserAgent = "ASTI RTSP Client", m_AuthorizationHeader;

        /// <summary>
        /// The socket used for Rtsp Communication
        /// </summary>
        internal Socket m_RtspSocket;

        /// <summary>
        /// The remote IPAddress to which the Location resolves via Dns
        /// </summary>
        readonly IPAddress m_RemoteIP;

        /// <summary>
        /// Keep track of certain values.
        /// </summary>
        internal int m_SentBytes, m_ReceivedBytes,
             m_RtspPort,
             m_CSeq, m_RCSeq, //-1 values, rtsp 2. indicates to start at 0...
             m_SentMessages,
             m_ReTransmits,
             m_ReceivedMessages,
             m_PushedMessages,
             m_MaximumTransactionAttempts = (int)Media.Common.Extensions.TimeSpan.TimeSpanExtensions.MicrosecondsPerMillisecond,//10
             m_SocketPollMicroseconds;

        /// Keep track of timed values.
        /// </summary>
        internal TimeSpan m_RtspSessionTimeout = DefaultSessionTimeout,
            m_ConnectionTime = Media.Common.Extensions.TimeSpan.TimeSpanExtensions.InfiniteTimeSpan,
            m_LastServerDelay = Media.Common.Extensions.TimeSpan.TimeSpanExtensions.InfiniteTimeSpan,
            //Appendix G.  Requirements for Unreliable Transport of RTSP
            m_LastMessageRoundTripTime = DefaultConnectionTime;

        internal DateTime? m_BeginConnect, m_EndConnect, m_StartedPlaying;

        internal NetworkCredential m_Credential;

        /// <summary>
        /// The media items which are in the play state.
        /// </summary>
        internal readonly Dictionary<MediaDescription, MediaSessionState> m_Playing = new Dictionary<MediaDescription, MediaSessionState>();//Could just be a list but the dictionary offers faster indexing at the cost of more memory...

        /// <summary>
        /// A threading resource which is used to synchronize access to the underlying buffer during message parsing and completion.
        /// </summary>
        internal readonly ManualResetEventSlim m_InterleaveEvent;

        /// <summary>
        /// The buffer this client uses for all requests 4MB * 2 by default.
        /// </summary>
        internal Common.MemorySegment m_Buffer;

        /// <summary>
        /// The value passed to the <see cref="DateTime.ToString"/> method when <see cref="DateRequests"/> is true.
        /// </summary>
        public string DateFormat = DefaultDateFormat;

        /// <summary>
        /// As given by the OPTIONS response or set otherwise.
        /// </summary>
        public readonly HashSet<string> SupportedFeatures = new HashSet<string>();

        /// <summary>
        /// Values which will be set in the Required tag.
        /// </summary>
        public readonly HashSet<string> RequiredFeatures = new HashSet<string>();

        /// <summary>
        /// Any additional headers which may be required by the RtspClient.
        /// </summary>
        public readonly Dictionary<string, string> AdditionalHeaders = new Dictionary<string, string>();

        /// <summary>
        /// Gets the methods supported by the server recieved in the options request.
        /// </summary>
        public readonly HashSet<string> SupportedMethods = new HashSet<string>();

        //Todo, should be property with protected set.

        /// <summary>
        /// A ILogging instance
        /// </summary>
        public Common.ILogging Logger;

        #endregion

        #region Properties [obtained during OPTIONS]

        //Options message...

        #endregion

        #region Properties [obtained during DESCRIBE]

        //Describe message? only because the Content-Base is relevant, could also just keep a Uri for the Location...

        /// <summary>
        /// The Uri to which RtspMessages must be addressed within the session.
        /// Typically this is the same as Location in the RtspClient but under NAT a different address may be used here.
        /// </summary>
        public System.Uri ControlLocation { get; internal protected set; }

        public Sdp.SessionDescription SessionDescription { get; protected set; }

        #endregion

        #region Properties [obtained during SETUP]

        /// <summary>
        /// 3.4 Session Identifiers
        /// Session identifiers are opaque strings of arbitrary length. Linear
        /// white space must be URL-escaped. A session identifier MUST be chosen
        /// randomly and MUST be at least eight octets long to make guessing it
        /// more difficult. (See Section 16.)
        /// </summary>
        public string SessionId { get; internal protected set; }

        /// <summary>
        /// The time in which a request must be sent to keep the session active.
        /// </summary>
        public System.TimeSpan Timeout { get; protected set; }

        /// <summary>
        /// The TransportContext of the RtspSession
        /// </summary>
        /// Should be either a object or a derived class, should not be required due to raw or other transport, could be ISocketReference
        /// Notes that a session can share one or more context's
        public Rtp.RtpClient.TransportContext Context { get; internal protected set; }

        //public Sdp.MediaDescription MediaDescription { get; protected set; } => {Context.MediaDescription;}

        // DateTimeOffset Started or LastStarted

        // TimeSpan Remaining


        //PauseTime

        #endregion

        #region Properties

        /// <summary>
        /// Indicates if the RtspClient shares the <see cref="RtspSocket"/> with the underlying Transport.
        /// </summary>
        public bool SharesSocket
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get
            {
                //The socket is shared with the GC
                if (Common.IDisposedExtensions.IsNullOrDisposed(this)) return true;

                // A null or disposed client or one which is no longer connected cannot share the socket
                if (Common.IDisposedExtensions.IsNullOrDisposed(m_RtpClient) || m_RtpClient.IsActive is false) return false;

                //The socket is shared if there is a context using the same socket
                RtpClient.TransportContext context = m_RtpClient.GetContextBySocket(m_RtspSocket);

                return Common.IDisposedExtensions.IsNullOrDisposed(context) is false && context.IsActive;// && context.HasAnyRecentActivity;
            }
        }

        /// <summary>
        /// Indicates if the RtspClient is currently sending or receiving data.
        /// </summary>
        public bool InUse
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_InterleaveEvent.IsSet is false &&
                    m_InterleaveEvent.Wait(Common.Extensions.TimeSpan.TimeSpanExtensions.TwoHundedNanoseconds) is false; //m_InterleaveEvent.Wait(1); // ConnectionTime
            }
        }

        /// <summary>
        /// The amount of bytes sent by the RtspClient
        /// </summary>
        public int BytesSent
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return m_SentBytes; }
        }

        /// <summary>
        /// The amount of bytes recieved by the RtspClient
        /// </summary>
        public int BytesRecieved
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return m_ReceivedBytes; }
        }

        /// <summary>
        /// Indicates the amount of messages which were transmitted more then one time.
        /// </summary>
        public int RetransmittedMessages
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return m_ReTransmits; }
        }

        /// <summary>
        /// Indicates if the client has tried to Authenticate using the current <see cref="Credential"/>'s
        /// </summary>
        public bool TriedCredentials
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return string.IsNullOrWhiteSpace(m_AuthorizationHeader) is false; }
        }

        /// <summary>
        /// The amount of <see cref="RtspMessage"/>'s sent by this instance.
        /// </summary>
        public int MessagesSent
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return m_SentMessages; }
        }

        /// <summary>
        /// The amount of <see cref="RtspMessage"/>'s receieved by this instance.
        /// </summary>
        public int MessagesReceived
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return m_ReceivedMessages; }
        }

        /// <summary>
        /// The amount of messages pushed by the remote party
        /// </summary>
        public int MessagesPushed
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return m_PushedMessages; }
        }

        /// <summary>
        /// Gets or Sets amount the fraction of time the client will wait during a responses for a response without blocking.
        /// If less than or equal to 0 the value 1 will be used.
        /// </summary>
        public int ResponseTimeoutInterval
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return m_MaximumTransactionAttempts; }
            set { m_MaximumTransactionAttempts = Binary.Clamp(value, 1, int.MaxValue); }
        }

        /// <summary>
        /// Gets or sets the maximum amount of microseconds the <see cref="RtspSocket"/> will wait before performing an operations.
        /// </summary>
        public int SocketPollMicroseconds
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return m_SocketPollMicroseconds; }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set { m_SocketPollMicroseconds = value; }
        }

        /// <summary>
        /// Gets the remote <see cref="EndPoint"/>
        /// </summary>
        public EndPoint RemoteEndpoint
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return m_RtspSocket.RemoteEndPoint; }
        }

        public Socket RtspSocket
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return m_RtspSocket; }
        }

        /// <summary>
        /// Indicates if the RtspClient is connected to the remote host
        /// </summary>
        /// <notes>May want to do a partial receive for 1 byte which would take longer but indicate if truly connected. Udp may not be Connected.</notes>
        public bool IsConnected
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return Common.IDisposedExtensions.IsNullOrDisposed(this) is false && m_ConnectionTime >= TimeSpan.Zero && m_RtspSocket is not null; /*&& m_RtspSocket.Connected*/; }
        }

        /// <summary>
        /// The amount of time taken to connect to the remote party.
        /// </summary>
        public TimeSpan ConnectionTime
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return m_ConnectionTime; }
        }

        /// <summary>
        /// The current SequenceNumber of the RtspClient
        /// </summary>
        public int ClientSequenceNumber
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return m_CSeq; }
        }

        /// <summary>
        /// The current SequenceNumber of the remote RTSP party
        /// </summary>
        public int RemoteSequenceNumber
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return m_RCSeq; }
        }

        /// <summary>
        /// Increments and returns the current <see cref="ClientSequenceNumber"/>
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        internal int NextClientSequenceNumber() { return ++m_CSeq; }

        /// <summary>
        /// Determines if a request must be sent periodically to keep the session and any underlying connection alive
        /// </summary>
        public bool EnableKeepAliveRequest { get; internal protected set; }

        /// <summary>
        /// The last RtspMessage sent
        /// </summary>
        public RtspMessage LastRequest { get; internal protected set; }

        /// <summary>
        /// The last RtspMessage received
        /// </summary>
        public RtspMessage LastResponse { get; internal protected set; }

        /// <summary>
        /// The amount of time taken from when the LastRequest was sent to when the LastResponse was created.
        /// </summary>
        public System.TimeSpan RoundTripTime
        {
            get { return LastResponse.Created - (LastRequest.Transferred ?? LastRequest.Created); }
        }

        /// <summary>
        /// The last RtspMessage recieved from the remote source
        /// </summary>
        public RtspMessage LastInboundRequest { get; internal protected set; }

        /// <summary>
        /// The last RtspMessage sent in response to a RtspMessage received from the remote source
        /// </summary>
        public RtspMessage LastInboundResponse { get; internal protected set; }

        /// <summary>
        /// The amount of time taken from when the LastInboundRequest was received to when the LastInboundResponse was Transferred.
        /// </summary>
        public System.TimeSpan ResponseTime
        {
            get { return LastInboundResponse.Created - (LastInboundRequest.Transferred ?? LastInboundRequest.Created); }
        }

        /// <summary>
        /// Time time remaining before the the session becomes Inactive
        /// </summary>
        public System.TimeSpan SessionTimeRemaining
        {
            get { return Timeout <= Common.Extensions.TimeSpan.TimeSpanExtensions.InfiniteTimeSpan ? Common.Extensions.TimeSpan.TimeSpanExtensions.InfiniteTimeSpan : Timeout - (System.DateTime.UtcNow - (LastResponse.Transferred ?? LastResponse.Created)); }
        }

        /// <summary>
        /// Indicates if the session has become inactive
        /// </summary>
        public bool TimedOut
        {
            get { return SessionTimeRemaining > System.TimeSpan.Zero; }
        }

        /// <summary>
        /// Indicates if the client will try to automatically reconnect during send or receive operations.
        /// </summary>
        public bool AutomaticallyReconnect
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set;
        }

        /// <summary>
        /// Indicates if the client will automatically disconnect the RtspSocket after StartPlaying is called.
        /// </summary>
        public bool AutomaticallyDisconnectAfterStartPlaying
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set;
        }

        /// <summary>
        /// Indicates if the client will send a <see cref="KeepAliveRequest"/> during <see cref="StartPlaying"/> if no data is flowing immediately after the PLAY response is recieved.
        /// </summary>
        public bool SendKeepAliveImmediatelyAfterStartPlaying
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set;
        }

        /// <summary>
        /// Indicates if the client will add the Timestamp header to outgoing requests.
        /// </summary>
        public bool TimestampRequests
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set;
        }

        /// <summary>
        /// Indicates if the client will use the Timestamp header to incoming responses.
        /// </summary>
        public bool CalculateServerDelay
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set;
        }

        /// <summary>
        /// Indicates if the client will send the Blocksize header during the SETUP request.
        /// The value of which will reflect the <see cref="Buffer.Count"/>
        /// </summary>
        public bool SendBlocksize
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set;
        }

        /// <summary>
        /// Indicates if the Date header should be sent during requests.
        /// </summary>
        public bool DateRequests
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set;
        }

        /// <summary>
        /// Indicates if the RtspClient will send the UserAgent header.
        /// </summary>
        public bool SendUserAgent
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set;
        }

        //Maybe AllowHostChange
        //public bool IgnoreRedirectOrFound { get; set; }

        /// <summary>
        /// Indicates if the client will take any `X-` headers and use them in future requests.
        /// </summary>
        public bool EchoXHeaders
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set;
        }

        /// <summary>
        /// Indicates if the client will process messages which are pushed during the session.
        /// </summary>
        public bool IgnoreServerSentMessages
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set;
        }

        /// <summary>
        /// Indicates if Keep Alive Requests will be sent
        /// </summary>
        public bool DisableKeepAliveRequest
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set;
        }

        /// <summary>
        /// Gets or Sets a value which indicates if the client will attempt an alternate style of connection if one cannot be established successfully.
        /// Usually only useful under UDP when NAT prevents RTP packets from reaching a client, it will then attempt TCP or HTTP transport.
        /// </summary>
        public bool AllowAlternateTransport
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set;
        }

        /// <summary>
        /// Gets or sets a value indicating of the RtspSocket should be left open when Disposing.
        /// </summary>
        public bool LeaveOpen
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set;
        }

        /// <summary>
        /// The version of Rtsp the client will utilize in messages
        /// </summary>
        public double ProtocolVersion
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set;
        }

        /// <summary>
        /// Gets or Sets the method which is called when the <see cref="RtspSocket"/> is created, 
        /// typically during the call to <see cref="Connect"/>
        /// By default <see cref="ConfigureRtspSocket"/> is utilized.
        /// </summary>
        public Action<Socket> ConfigureSocket
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set;
        }

        // <summary>
        /// The type of AuthenticationScheme to utilize in RtspRequests, if this is not set then the Credential will not send until it has been determined from a Not Authroized response.
        /// </summary>
        public AuthenticationSchemes AuthenticationScheme
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return m_AuthenticationScheme; }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set
            {
                if (value == m_AuthenticationScheme) return;

                switch (m_AuthenticationScheme)
                {
                    case AuthenticationSchemes.Basic:
                    case AuthenticationSchemes.Digest:
                    case AuthenticationSchemes.None:
                        break;
                    default: throw new System.InvalidOperationException("Only None, Basic and Digest are supported");
                }

                m_AuthenticationScheme = value;

                m_AuthorizationHeader = null;
            }
        }

        /// <summary>
        /// The network credential to utilize in RtspRequests
        /// </summary>
        public NetworkCredential Credential
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return m_Credential; }

            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set
            {
                m_Credential = value;

                m_AuthorizationHeader = null;
            }
        }

        /// <summary>
        /// If playing, the TimeSpan which represents the time this media started playing from.
        /// </summary>
        public TimeSpan? StartTime
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get
            {
                if (Common.IDisposedExtensions.IsNullOrDisposed(m_RtpClient)) return null;

                TimeSpan? startTime = default(TimeSpan?);

                foreach (RtpClient.TransportContext tc in m_RtpClient.GetTransportContexts()) if (startTime.HasValue is false || tc.m_StartTime > startTime) startTime = tc.m_StartTime;

                return startTime;
            }
        }

        /// <summary>
        /// If playing, the TimeSpan which represents the time the media will end.
        /// </summary>
        public TimeSpan? EndTime
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get
            {
                if (Common.IDisposedExtensions.IsNullOrDisposed(m_RtpClient)) return null;

                TimeSpan? endTime = default(TimeSpan?);

                foreach (RtpClient.TransportContext tc in m_RtpClient.GetTransportContexts()) if (endTime.HasValue is false || tc.m_EndTime > endTime) endTime = tc.m_EndTime;

                return endTime;
            }
        }

        //Remaining?

        /// <summary>
        /// If playing, indicates if the RtspClient is playing from a live source which means there is no absolute start or end time and seeking may not be supported.
        /// </summary>
        public bool LivePlay
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return EndTime.Equals(Common.Extensions.TimeSpan.TimeSpanExtensions.InfiniteTimeSpan); }
        }

        /// <summary>
        /// Indicates if there is any media being played by the RtspClient at the current time.
        /// </summary>
        public bool IsPlaying
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get
            {
                //If started playing
                if (m_Playing.Count > 0 && m_StartedPlaying.HasValue)
                {
                    //Try to determine playing status from the transport and the MediaState.
                    try
                    {
                        System.TimeSpan? endTime = EndTime;

                        //If not playing anymore do nothing
                        if (endTime.HasValue && false.Equals(endTime.Value.Equals(Media.Common.Extensions.TimeSpan.TimeSpanExtensions.InfiniteTimeSpan)) &&
                            DateTime.UtcNow - m_StartedPlaying.Value > endTime.Value)
                        {
                            return false;
                        }

                        //If the media is playing the RtspClient is only playing if the socket is shared or the Transport is connected.
                        if (Common.IDisposedExtensions.IsNullOrDisposed(m_RtpClient)) return false;

                        //Just takes more time...
                        //foreach (RtpClient.TransportContext tc in m_RtpClient.GetTransportContexts())
                        //{
                        //    if (tc.HasAnyRecentActivity) return true;
                        //}

                        //if the client is active the RtspClient is probably playing.
                        return m_RtpClient.IsActive;

                    }
                    catch (Exception ex)
                    {
                        Media.Common.ILoggingExtensions.Log(Logger, ToString() + "@IsPlaying - " + ex.Message);
                    }
                }

                //The RtspClient is not playing
                return false;
            }
        }

        /// <summary>
        /// Gets or Sets the ReadTimeout of the underlying NetworkStream / Socket (msec)
        /// </summary>
        public int SocketReadTimeout
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return Common.IDisposedExtensions.IsNullOrDisposed(this) || m_RtspSocket is null ? -1 : m_RtspSocket.ReceiveTimeout; }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set { if (Common.IDisposedExtensions.IsNullOrDisposed(this) || m_RtspSocket is null) return; m_RtspSocket.ReceiveTimeout = value; }
        }

        /// <summary>
        /// Gets or Sets the WriteTimeout of the underlying NetworkStream / Socket (msec)
        /// </summary>
        public int SocketWriteTimeout
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return Common.IDisposedExtensions.IsNullOrDisposed(this) || m_RtspSocket is null ? -1 : m_RtspSocket.SendTimeout; }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set { if (Common.IDisposedExtensions.IsNullOrDisposed(this) || m_RtspSocket is null) return; m_RtspSocket.SendTimeout = value; }
        }

        /// <summary>
        /// The UserAgent sent with every RtspRequest
        /// </summary>
        public string UserAgent
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return m_UserAgent; }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentNullException("UserAgent cannot consist of only null or whitespace."); m_UserAgent = value; }
        }

        ///HasAuthenticated

        #endregion

        //Playing List

        #region Constructor

        internal RtspSession(RtspClient parent, bool shouldDispose = true) : base(shouldDispose)
        {
            m_Client = parent;
            m_AuthenticationScheme = parent.m_AuthenticationScheme;
            m_AuthorizationHeader = parent.m_AuthorizationHeader;
            m_BeginConnect = parent.m_BeginConnect;
            m_Buffer = parent.m_Buffer;
            m_ConnectionTime = parent.m_ConnectionTime;
            m_Credential = parent.m_Credential;
            m_CSeq = parent.m_CSeq;
            m_EndConnect = parent.m_EndConnect;
            m_InterleaveEvent = parent.m_InterleaveEvent;
            m_LastMessageRoundTripTime = parent.m_LastMessageRoundTripTime;
            m_LastServerDelay = parent.m_LastServerDelay;
            m_LastTransmitted = parent.m_LastTransmitted;
            m_MaximumTransactionAttempts = parent.m_MaximumTransactionAttempts;
            m_Playing = parent.m_Playing;
            m_PushedMessages = parent.m_PushedMessages;
            m_RCSeq = parent.m_RCSeq;
            m_ReceivedBytes = parent.m_ReceivedBytes;
            m_ReceivedMessages = parent.m_ReceivedMessages;
            m_RemoteIP = parent.m_RemoteIP;
            m_RemoteRtsp = parent.m_RemoteRtsp;
            m_ReTransmits = parent.m_ReTransmits;
            m_RtpClient = parent.m_RtpClient;
            m_RtpProtocol = parent.m_RtpProtocol;
            m_RtspPort = parent.m_RtspPort;
            m_RtspProtocol = parent.m_RtspProtocol;
            m_RtspSessionTimeout = parent.m_RtspSessionTimeout;
            m_RtspSocket = parent.m_RtspSocket;
            m_SentBytes = parent.m_SentBytes;
            m_SentMessages = parent.m_SentMessages;
            m_SocketPollMicroseconds = parent.m_SocketPollMicroseconds;
            m_StartedPlaying = parent.m_StartedPlaying;
            m_UserAgent = parent.m_UserAgent;
        }

        public RtspSession(RtspClient parent, RtspMessage request, RtspMessage response, bool shouldDispose = true)
           : this(parent, shouldDispose)
        {
            LastRequest = request;

            if (response is not null)
            {
                ParseSessionIdAndTimeout(LastResponse = response);
            }
        }

        #endregion

        #region Methods

        public RtspMessage SendRtspMessage(RtspMessage message, bool useClientProtocolVersion = true, bool hasResponse = true)
        {
            return SendRtspMessage(message, out SocketError error, useClientProtocolVersion, hasResponse);
        }

        public RtspMessage SendRtspMessage(RtspMessage message, out SocketError error, bool useClientProtocolVersion = true, bool hasResponse = true)
        {
            //Don't try to send if already disposed.
            CheckDisposed();

            unchecked
            {
                //Indicate a send has not been attempted
                error = SocketError.SocketError;

                //Indicate the sequence number has not been observed
                var sequenceNumber = -1;

                bool wasBlocked = false, fatal = false;

                try
                {
                    int retransmits = 0, attempt = m_MaximumTransactionAttempts, //The attempt counter itself
                        sent = 0, received = 0, //counter for sending and receiving locally
                        offset = 0, length = 0,
                        startSequenceNumber = -1;

                    //Half of the session timeout in milliseconds
                    int halfTimeout = (int)(m_RtspSessionTimeout.TotalMilliseconds / 2);

                    byte[] buffer = null;

                    #region Check for a message

                    bool wasConnected = IsConnected;

                    //If there is no message to send then check for response
                    if (message is null) goto Connect;

                    #endregion

                    #region useClientProtocolVersion

                    //Ensure the request version matches the protocol version of the client if enforceVersion is true.
                    if (useClientProtocolVersion && (message.Version == ProtocolVersion) is false)
                        message.Version = ProtocolVersion;

                    #endregion

                    #region Additional Headers

                    //Use any additional headers if given
                    if (AdditionalHeaders.Count > 0)
                        foreach (var additional in AdditionalHeaders)
                            message.AppendOrSetHeader(additional.Key, additional.Value);

                    #endregion

                    #region CSeq

                    //Get the next Sequence Number and set it in the request. (If not already present)
                    //Store the result - 1

                    //Todo, use session...
                    if (message.ContainsHeader(RtspHeaders.CSeq).Equals(fatal))
                        startSequenceNumber += sequenceNumber = message.CSeq = NextClientSequenceNumber();
                    else
                        startSequenceNumber += sequenceNumber = message.CSeq;

                    #endregion

                    #region ContentEncoding

                    //Add the content encoding header if required
                    if (message.ContainsHeader(RtspHeaders.ContentEncoding) is false &&
                        message.ContentEncoding.WebName.Equals(RtspMessage.DefaultEncoding.WebName) is false)
                        message.SetHeader(RtspHeaders.ContentEncoding, message.ContentEncoding.WebName);

                    #endregion

                    #region DateRequests

                    //Set the Date header if required, todo
                    if (DateRequests && message.ContainsHeader(RtspHeaders.Date).Equals(fatal))
                        message.SetHeader(RtspHeaders.Date, DateTime.UtcNow.ToString(DateFormat));

                    #endregion

                    #region SessionId

                    //Set the Session header if required and not already contained.
                    if (string.IsNullOrWhiteSpace(SessionId) is false &&
                        message.ContainsHeader(RtspHeaders.Session) is false)
                        message.SetHeader(RtspHeaders.Session, SessionId);

                    #endregion

                    #region SendUserAgent

                    //Add the user agent if required
                    if (SendUserAgent &&
                        message.ContainsHeader(RtspHeaders.UserAgent) is false)
                        message.SetHeader(RtspHeaders.UserAgent, m_UserAgent);

                    #endregion

                    #region Credentials

                    //Todo AuthenticatorState { IsAuthenticated, LastAuthenticationDateTime, LastAuthenticationStatus, LastAuthenticationHeaders, Credentials, Cache etc }
                    //Authenticate(Async)(AuthenticatorState = Session.AuthenticatorState)

                    //If there not already an Authorization header and there is an AuthenticationScheme utilize the information in the Credential
                    if (message.ContainsHeader(RtspHeaders.Authorization) is false &&
                        m_AuthenticationScheme > AuthenticationSchemes.None && //Using this as an unknown value at first..
                        Credential is not null)
                    {
                        //Basic
                        if (m_AuthenticationScheme == AuthenticationSchemes.Basic)
                        {
                            message.SetHeader(RtspHeaders.Authorization, RtspHeaders.BasicAuthorizationHeader(message.ContentEncoding, Credential));
                        }
                        else if (m_AuthenticationScheme == AuthenticationSchemes.Digest)
                        {
                            //Could get values from m_LastTransmitted.
                            //Digest
                            message.SetHeader(RtspHeaders.Authorization,
                                RtspHeaders.DigestAuthorizationHeader(message.ContentEncoding,
                                message.RtspMethod,
                                message.Location,
                                Credential,
                                null, null, null, null, null,
                                false,
                                null,
                                message.Body));
                        }
                        else
                        {
                            message.SetHeader(RtspHeaders.Authorization, m_AuthenticationScheme.ToString());
                        }
                    }

                    #endregion

                    Timestamp:
                    #region Timestamp
                    //If requests should be timestamped
                    if (TimestampRequests) Timestamp(message);

                    //Take note of the timestamp of the message out
                    string timestampSent = message[RtspHeaders.Timestamp];

                    //Get the bytes of the request
                    buffer = m_RtspProtocol == ClientProtocolType.Http ? RtspMessage.ToHttpBytes(message) : message.ToBytes();

                    offset = m_Buffer.Offset;

                    length = buffer.Length;
                    #endregion

                    //-- MessageTransfer can be reused.

                    Connect:
                    #region Connect
                    //Wait for any existing requests to finish first
                    wasBlocked = InUse;

                    //If was block wait for that to finish
                    //if (wasBlocked) m_InterleaveEvent.Wait();

                    if (wasConnected is false && (wasConnected = IsConnected) is false) Connect();

                    //If the client is not connected then nothing can be done.

                    //Othewise we are connected
                    if ((wasConnected = IsConnected) is false) return null;

                    //Set the block if a response is required.
                    if (hasResponse && wasBlocked is false) m_InterleaveEvent.Reset();


                    //If nothing is being sent this is a receive only operation
                    if (Common.IDisposedExtensions.IsNullOrDisposed(message)) goto NothingToSend;

                    #endregion

                    Send:
                    #region Send
                    //If the message was Transferred previously
                    if (message.Transferred.HasValue)
                    {
                        Media.Common.ILoggingExtensions.Log(Logger, SessionId + "SendRtspMessage Retransmit");

                        //Make the message not Transferred
                        message.Transferred = null;

                        //Increment counters for retransmit
                        ++retransmits;

                        ++m_ReTransmits;
                    }

                    //Because SocketReadTimeout or SocketWriteTimeout may be 0 do a read to avoid the abort of the connection.
                    //TCP RST occurs when the ACK is missed so keep the window open.
                    if (IsConnected &&
                        SharesSocket is false &&
                        m_RtspSocket.Poll(m_SocketPollMicroseconds >> 4, SelectMode.SelectRead) /*&& m_RtspSocket.Available > 0*/)
                    {
                        //Receive if data is actually available.
                        goto Receive;
                    }

                    //If we can write before the session will end
                    if (IsConnected &&
                        m_RtspSocket is not null &&
                        m_RtspSocket.Poll(m_SocketPollMicroseconds >> 4, SelectMode.SelectWrite))
                    {
                        //Send all the data now
                        sent += Common.Extensions.Socket.SocketExtensions.SendTo(buffer, 0, length, m_RtspSocket, m_RemoteRtsp, SocketFlags.None, out error);
                    }

                    #region Handle SocketError.Send

                    switch (error)
                    {
                        case SocketError.ConnectionAborted:
                        case SocketError.ConnectionReset:
                        case SocketError.Shutdown:
                            {
                                if (AutomaticallyReconnect && Common.IDisposedExtensions.IsNullOrDisposed(this) is false)
                                {
                                    //Check if the client was connected already
                                    if (wasConnected && IsConnected is false)
                                    {
                                        Reconnect(true);

                                        goto Send;
                                    }

                                    throw new SocketException((int)error);
                                }
                                else fatal = true;

                                goto default;
                            }
                        case SocketError.Success:
                        default:
                            {
                                //if the client is not disposed and a fatal error was not encountered.
                                if (Common.IDisposedExtensions.IsNullOrDisposed(this) is false &&
                                    fatal is false)
                                {
                                    //If this is not a re-transmit
                                    if (sent >= length)
                                    {
                                        //Set the time when the message was transferred if this is not a retransmit.
                                        message.Transferred = DateTime.UtcNow;

                                        //Fire the event (sets Transferred)
                                        Requested(message);

                                        //Increment for messages sent or the messages retransmitted.
                                        ++m_SentMessages;

                                        //Increment our byte counters for Rtsp
                                        m_SentBytes += sent;

                                        //Attempt to receive so start attempts back at 0
                                        /*sent = */
                                        attempt = 0;

                                        //Release the reference to the array
                                        buffer = null;
                                    }
                                    else if (sent < length &&
                                        ++attempt < m_MaximumTransactionAttempts)
                                    {
                                        //Make another attempt @
                                        //Sending the rest
                                        goto Send;
                                    }
                                }

                                break;
                            }

                    }

                    #endregion

                    #endregion

                    NothingToSend:
                    #region NothingToSend
                    //Check for no response.
                    if (hasResponse is false || Common.IDisposedExtensions.IsNullOrDisposed(this)) return null;

                    //If the socket is shared the response will be propagated via an event.
                    if (Common.IDisposedExtensions.IsNullOrDisposed(this) is false && SharesSocket) goto Wait;
                    #endregion

                    //Receive some data (only referenced by the check for disconnection)
                    Receive:
                    #region Receive

                    //While nothing bad has happened.
                    if (fatal is false &&
                        SharesSocket is false &&
                        IsConnected &&
                        m_RtspSocket.Poll(m_SocketPollMicroseconds >> 4, SelectMode.SelectRead)/* ||  
                        attempts.Equals(m_MaximumTransactionAttempts) &&
                        message is not null*/)
                    {
                        //Todo, Media.Sockets.TcpClient

                        //Todo, if OutOfBand data is not received in this data stream then process seperately.
                        //if(false.Equals(Media.Common.Extensions.Socket.SocketExtensions.GetTcpOutOfBandInLine(m_RtspSocket)))
                        //received += m_RtspSocket.Receive(m_Buffer.Array, offset, m_Buffer.Count, SocketFlags.OutOfBand, out error);
                        //else
                        //Receive

                        received += m_RtspSocket.Receive(m_Buffer.Array, offset, m_Buffer.Count, SocketFlags.None, out error);
                    }

                    #region Handle SocketError.Recieve

                    switch (error)
                    {
                        case SocketError.ConnectionAborted:
                        case SocketError.ConnectionReset:
                        case SocketError.Shutdown:
                            {
                                if (AutomaticallyReconnect && Common.IDisposedExtensions.IsNullOrDisposed(this) is false)
                                {
                                    //Check if the client was connected already
                                    if (wasConnected && IsConnected is false)
                                    {
                                        Reconnect(true);

                                        //May have to reset sent...

                                        goto Send;
                                    }

                                    throw new SocketException((int)error);
                                }
                                else fatal = true;

                                goto default;
                            }
                        case SocketError.Success:
                        default:
                            {
                                //If anything was received
                                if (Common.IDisposedExtensions.IsNullOrDisposed(this) is false &&
                                    received > 0 &&
                                    SharesSocket is false)
                                {

                                    ///Because of the terrible decisions made in realtion to framing with respect to the data subject to transport within the protocol,
                                    ///the horrible design of the application layer framing even for 1998 and the reluctance to use existing known techniques which can fix this in a compatible way;
                                    ///combined with the other suggestions made by the RFC inclusing but not limited to the restriction on TCP retransmissions and message retransmission                         
                                    ///Message handling at this level must be extremely flexible, the message class itself should be able to be used as a construct to retain data which is unexpected;
                                    ///This may eventually happen through an even on the message class such as `OnInvalidData` / ``OnUnexpectedData`
                                    ///This also indicates that the message class itself should be more flexible or that it should be based upon or include a construct which offers such events
                                    ///from the construct itself such that instances which utilize or share memory with the construct can safely intepret the data therein.

#if UNSAFE
                        if (char.IsLetterOrDigit(((*(byte*)System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement<byte>(m_Buffer.Array, offset))) is false)
#else
                                    if (Common.IDisposedExtensions.IsNullOrDisposed(m_RtpClient) is false &&
                                        char.IsLetterOrDigit((char)m_Buffer.Array[offset]) is false)
#endif
                                    {
                                        //Some people just start sending packets hoping that the context will be created dynamically.
                                        //I guess you could technically skip describe and just receive everything raising events as required...
                                        //White / Black Hole Feature(s)? *cough*QuickTime*cough*

                                        //Make sure the thread is ready for the RtpClient client
                                        if (m_RtpClient.IsActive is false
                                            && Common.IDisposedExtensions.IsNullOrDisposed(this) is false)
                                        {
                                            //Store the offset
                                            m_RtpClient.m_SignalOffset = offset;

                                            //Store the socket needed to receive the data.
                                            m_RtpClient.m_SignalSocket = m_RtspSocket;

                                            //Indicate how much was received out of thread
                                            m_RtpClient.m_SignalCount = received;

                                            //Activate the RtpClient
                                            m_RtpClient.Activate();

                                            //Don't handle any data now, wait the event from the other thread.
                                            received = 0;
                                        }

                                        //Deliver any data which was intercepted to the underlying Transport.
                                        //Any data handled in the rtp layer is should not count towards what was received.
                                        //This can throw off the call if SharesSocket changes during the life of this call.
                                        //In cases where this is off it can be fixed by using Clamp, it usually only occurs when someone is disconnecting.
                                        //received -= m_RtpClient.ProcessFrameData(m_Buffer.Array, offset, received, m_RtspSocket);

                                        //Handle when the client received a lot of data and no response was found when interleaving.
                                        //One possibility is transport packets such as Rtp or Rtcp.
                                        //if (received < 0) received = 0; // Common.Binary.Clamp(received, 0, m_Buffer.Count);                           
                                    }
                                    else
                                    {
                                        //Otherwise just process the data via the event.
                                        //Possibly overflow, should min.
                                        m_Client.ProcessInterleavedData(this, m_Buffer.Array, offset, Common.Binary.Min(received, m_Buffer.Count - offset));
                                    }
                                } //Nothing was received, if the socket is not shared
                                else if (Common.IDisposedExtensions.IsNullOrDisposed(this) is false && SharesSocket is false)
                                {
                                    //Check for non fatal exceptions and continue to wait
                                    if (++attempt <= m_MaximumTransactionAttempts &&
                                        fatal is false)
                                    {
                                        //We don't share the socket so go to recieve again (note if this is the timer thread this can delay outgoing requests)
                                        goto Wait;
                                    }

                                    //Todo, this isn't really needed once there is a thread monitoring the protocol.
                                    //Right now it probably isn't really needed either.
                                    //Raise the exception (may be success to notify timer thread...)
                                    if (Common.IDisposedExtensions.IsNullOrDisposed(message)) throw new SocketException((int)error);
                                    else return m_LastTransmitted;
                                }

                                break;
                            }
                    }

                    #endregion

                    #endregion

                    //Wait for the response while the amount of data received was less than RtspMessage.MaximumLength
                    Wait:
                    #region Waiting for response, Backoff or Retransmit
                    DateTime lastAttempt = DateTime.UtcNow;

                    //Wait while
                    while (Common.IDisposedExtensions.IsNullOrDisposed(this) is false &&//The client connected and is not disposed AND
                                                                                        //There is no last transmitted message assigned AND it has not already been disposed
                        Common.IDisposedExtensions.IsNullOrDisposed(m_LastTransmitted) &&
                        //AND the client is still allowed to wait
                        ++attempt <= m_MaximumTransactionAttempts &&
                        m_InterleaveEvent.Wait(Common.Extensions.TimeSpan.TimeSpanExtensions.OneTick) is false)
                    {
                        //Check for any new messages
                        if (Common.IDisposedExtensions.IsNullOrDisposed(m_LastTransmitted) is false) goto HandleResponse;

                        //Calculate how much time has elapsed
                        TimeSpan taken = DateTime.UtcNow - lastAttempt;

                        int readTimeoutmSec = SocketReadTimeout;

                        //If more time has elapsed than allowed by reading
                        if (Common.IDisposedExtensions.IsNullOrDisposed(this) is false &&
                            IsConnected &&
                            readTimeoutmSec > 0 &&
                            taken > m_LastMessageRoundTripTime &&
                            taken.TotalMilliseconds >= readTimeoutmSec/* && 
                            error == SocketError.TimedOut*/)
                        {
                            //Check if we can back off further
                            if (taken.TotalMilliseconds >= halfTimeout) break;
                            else if (readTimeoutmSec < halfTimeout)
                            {
                                //Backoff
                                /*pollTime += (int)(Common.Extensions.TimeSpan.TimeSpanExtensions.MicrosecondsPerMillisecond */

                                SocketWriteTimeout = SocketReadTimeout *= 2;

                                Media.Common.ILoggingExtensions.Log(Logger, SessionId + "SendRtspMessage Timeout = " + readTimeoutmSec + " - " + readTimeoutmSec);

                                ////Ensure the client transport is connected if previously playing and it has since disconnected.
                                //if (IsPlaying &&
                                //    m_RtpClient is not null &&
                                //    false == m_RtpClient.IsActive) m_RtpClient.Activate();
                            }

                            //If the client was not disposed re-trasmit the request if there is not a response pending already.
                            //Todo allow an option for this feature? (AllowRetransmit)
                            if (Common.IDisposedExtensions.IsNullOrDisposed(this) is false &&
                                m_LastTransmitted is null /*&& request.Method != RtspMethod.PLAY*/)
                            {
                                //handle re-transmission under UDP
                                if (m_RtspSocket.ProtocolType == ProtocolType.Udp)
                                {
                                    Media.Common.ILoggingExtensions.Log(Logger, SessionId + "SendRtspMessage Retransmit Request");

                                    //Change the Timestamp if TimestampRequests is true
                                    if (TimestampRequests)
                                    {
                                        //Reset what to send.
                                        sent = 0;

                                        goto Timestamp;
                                    }

                                    //Reset what was sent so far.
                                    sent = 0;

                                    //Retransmit the exact same data
                                    goto Send;
                                }
                            }
                        }

                        //If not sharing socket trying to receive again.
                        if (SharesSocket is false)
                        {
                            //If the event is set check the response
                            if (m_InterleaveEvent.Wait(Common.Extensions.TimeSpan.TimeSpanExtensions.OneTick)) goto HandleResponse;

                            //If we have a message to send and did not send it then goto send.
                            //message.Transferred.HasValue
                            if (message is not null && sent is 0)
                                goto Send;

                            //Receive again
                            goto Receive;
                        }
                    }

                    #endregion

                    HandleResponse:
                    #region HandleResponse

                    //Update counters for any data received.
                    m_ReceivedBytes += received;

                    //If nothing was received wait for cache to clear.
                    if (Common.IDisposedExtensions.IsNullOrDisposed(m_LastTransmitted))
                    {
                        //Wait
                        m_InterleaveEvent.Wait(Common.Extensions.TimeSpan.TimeSpanExtensions.OneTick);
                    }
                    else /* if (Common.IDisposedExtensions.IsNullOrDisposed(message) is false) */
                    {

                        //Handle the message recieved

                        switch (m_LastTransmitted.RtspMessageType)
                        {
                            case RtspMessageType.Request:

                                m_Client.ProcessServerSentRequest(m_LastTransmitted);

                                //Todo, maybe wait more depdning on if a message was sent or not.

                                break;

                            case RtspMessageType.Response:
                                //If the event is not in disposed already
                                //If the client is not disposed
                                if (Common.IDisposedExtensions.IsNullOrDisposed(this) is false)
                                {
                                    ////Log for incomplete messages.
                                    //if (m_LastTransmitted.IsComplete is false)
                                    //{
                                    //    Media.Common.ILoggingExtensions.Log(Logger, InternalId + "SendRtspMessage, response incomplete.");
                                    //}

                                    //Check the protocol.
                                    if (m_LastTransmitted.ParsedProtocol.Equals(m_LastTransmitted.Protocol) is false)
                                    {
                                        Media.Common.ILoggingExtensions.Log(Logger, SessionId + "SendRtspMessage, Unexpected Protocol in response, Expected = " + m_LastTransmitted.Protocol + ", Found = " + m_LastTransmitted.ParsedProtocol);
                                    }

                                    //Could also check session header and Timestamp

                                    //else if (m_LastTransmitted.ContainsHeader(RtspHeaders.Timestamp))
                                    //{
                                    //    //Todo
                                    //    //Double check the Timestamp portion received is what was sent.
                                    //    //if it's not this is a response to an older request which was retransmitted.
                                    //}

                                    #region Notes

                                    //m_LastTransmitted is either null or not
                                    //if it is not null it may not be the same response we are looking for. (mostly during threaded sends and receives)
                                    //this could be dealt with by using a hash `m_Transactions` which holds requests which are sent and a space for their response if desired.
                                    //Then a function GetMessage(message) would be able to use that hash to get the outgoing or incoming message which resulted.
                                    //The structure of the hash would allow any response to be stored.

                                    #endregion

                                    if (message is not null /*&& m_LastTransmitted.StatusLineParsed*/)
                                    {
                                        //Obtain the CSeq of the response if present.
                                        int sequenceNumberSent = message.CSeq, sequenceNumberReceived = m_LastTransmitted.CSeq;

                                        //If the sequence number was present and did not match then log
                                        if (sequenceNumberSent >= 0 && false.Equals(sequenceNumberReceived.Equals(sequenceNumberSent)) && m_LastTransmitted.ParsedProtocol.Equals(m_LastTransmitted.Protocol))
                                        {
                                            Media.Common.ILoggingExtensions.Log(Logger, SessionId + "SendRtspMessage, response CSeq Does not Match request");

                                            //if the message was not in response to a request sent previously and socket is shared
                                            if (m_LastTransmitted.IsComplete is false)
                                            {
                                                if (SharesSocket)
                                                {
                                                    //Event the message received.
                                                    Received(message, m_LastTransmitted);

                                                    //Mark disposed
                                                    //Remove the message to avoid confusion
                                                    using (m_LastTransmitted) m_LastTransmitted = null;

                                                    //Reset the block
                                                    m_InterleaveEvent.Reset();

                                                    //Allow more waiting
                                                    attempt = received = 0;

                                                    goto Wait;
                                                }
                                                else if (++attempt <= m_MaximumTransactionAttempts /*|| m_RtspSocket.Available > 0*/)
                                                {
                                                    //Might need to retransmit...

                                                    goto Receive;
                                                }
                                            }

                                        }



                                        //else the sequenceNumberReceived is >= startSequenceNumber

                                        //Calculate the amount of time taken to receive the message.
                                        //Which is given by the time on the wall clock minus when the message was transferred or created.
                                        TimeSpan lastMessageRoundTripTime = (DateTime.UtcNow - (message.Transferred ?? message.Created));

                                        //Ensure positive values for the RTT
                                        //if (lastMessageRoundTripTime < TimeSpan.Zero) lastMessageRoundTripTime = lastMessageRoundTripTime.Negate();

                                        //Assign it
                                        m_LastMessageRoundTripTime = lastMessageRoundTripTime.Duration();
                                    }
                                    //else
                                    //{
                                    //    //Calculate from elsewhere, e.g. m_LastTransmitted.
                                    //}


                                    //TODO
                                    //REDIRECT (Handle loops)
                                    //if(m_LastTransmitted.StatusCode == RtspStatusCode.MovedPermanently)

                                    switch (m_LastTransmitted.RtspStatusCode)
                                    {
                                        case RtspStatusCode.OK:
                                            if (message is not null)
                                            {

                                                //Ensure message is added to supported methods.
                                                SupportedMethods.Add(message.MethodString);
                                            }

                                            break;
                                        case RtspStatusCode.NotImplemented:
                                            if (m_LastTransmitted.CSeq.Equals(message.CSeq))
                                            {
                                                SupportedMethods.Remove(message.MethodString);
                                            }

                                            break;
                                        //case RtspStatusCode.MethodNotValidInThisState:
                                        //    {
                                        //        //Idea was to see if anything followed this message, e.g. back to back
                                        //        //if (m_LastTransmitted.ContainsHeader(RtspHeaders.Allow)) MonitorProtocol();

                                        //        break;
                                        //    }
                                        case RtspStatusCode.Unauthorized:
                                            //If we were not authorized and we did not give a nonce and there was an WWWAuthenticate header given then we will attempt to authenticate using the information in the header
                                            //If there was a WWWAuthenticate header in the response
                                            if (m_LastTransmitted.ContainsHeader(RtspHeaders.WWWAuthenticate) &&
                                                Credential is not null) //And there have been Credentials assigned
                                            {
                                                //Event the received message.
                                                Received(message, m_LastTransmitted);

                                                //Return the result of Authenticating with the given request and response (forcing the request if the credentails have not already been tried)
                                                return Authenticate(message, m_LastTransmitted);
                                            }

                                            //break
                                            break;
                                        case RtspStatusCode.RtspVersionNotSupported:
                                            {
                                                //if enforcing the version
                                                if (useClientProtocolVersion)
                                                {
                                                    //Read the version from the response
                                                    ProtocolVersion = m_LastTransmitted.Version;

                                                    //Send the request again. SHOULD USE out error, 
                                                    return SendRtspMessage(message, out error, useClientProtocolVersion, hasResponse);
                                                }

                                                //break
                                                break;
                                            }
                                        default: break;
                                    }

                                    #region EchoXHeaders

                                    //If the client should echo X headers
                                    if (EchoXHeaders)
                                    {
                                        //iterate for any X headers 
                                        foreach (string xHeader in m_LastTransmitted.GetHeaders().Where(h => h.Length > 2 && h[1] == Common.ASCII.HyphenSign && char.ToLower(h[0]) == 'x'))
                                        {
                                            //If contained already then update
                                            if (AdditionalHeaders.ContainsKey(xHeader))
                                            {
                                                AdditionalHeaders[xHeader] += ((char)Common.ASCII.SemiColon).ToString() + m_LastTransmitted.GetHeader(xHeader).Trim();
                                            }
                                            else
                                            {
                                                //Add
                                                AdditionalHeaders.Add(xHeader, m_LastTransmitted.GetHeader(xHeader).Trim());
                                            }
                                        }
                                    }

                                    #endregion

                                    #region Parse Session Header

                                    //For any other request besides teardown update the sessionId and timeout
                                    if (message is not null &&
                                        false.Equals(message.RtspMethod == RtspMethod.TEARDOWN))
                                    {
                                        //Get the header.
                                        string sessionHeader = m_LastTransmitted[RtspHeaders.Session];

                                        //If there is a session header it may contain the option timeout
                                        if (string.IsNullOrWhiteSpace(sessionHeader) is false)
                                        {
                                            //Check for session and timeout

                                            //Get the values
                                            string[] sessionHeaderParts = sessionHeader.Split(RtspHeaders.SemiColon, 2, StringSplitOptions.RemoveEmptyEntries); //Only 2 sub strings...

                                            //RtspHeaders.ParseHeader(sessionHeader);

                                            int headerPartsLength = sessionHeaderParts.Length;

                                            //Check if a valid value was given
                                            if (headerPartsLength > 0)
                                            {
                                                //Trim it of whitespace
                                                string value = sessionHeaderParts.LastOrDefault(p => string.IsNullOrWhiteSpace(p) is false);

                                                //If we dont have an exiting id then this is valid if the header was completely recieved only.
                                                if (string.IsNullOrWhiteSpace(value) is false &&
                                                    string.IsNullOrWhiteSpace(SessionId) ||
                                                    false.Equals(string.Compare(value, SessionId) is Common.Binary.Zero))
                                                {
                                                    //Get the SessionId if present
                                                    SessionId = sessionHeaderParts[0].Trim();

                                                    //Check for a timeout
                                                    if (sessionHeaderParts.Length > 1)
                                                    {
                                                        int timeoutStart = 1 + sessionHeaderParts[1].IndexOf(Media.Sdp.SessionDescription.EqualsSign);
                                                        if (timeoutStart >= Common.Binary.Zero && int.TryParse(sessionHeaderParts[1].Substring(timeoutStart), out timeoutStart))
                                                        {
                                                            //Should already be set...
                                                            if (timeoutStart <= Common.Binary.Zero)
                                                            {
                                                                m_RtspSessionTimeout = DefaultSessionTimeout;
                                                            }
                                                            else
                                                            {
                                                                m_RtspSessionTimeout = TimeSpan.FromSeconds(timeoutStart);
                                                            }
                                                        }
                                                    }
                                                }

                                                //done
                                            }
                                            else if (string.IsNullOrWhiteSpace(SessionId))
                                            {
                                                //The timeout was not present
                                                SessionId = sessionHeader.Trim();

                                                m_RtspSessionTimeout = DefaultSessionTimeout;//Default
                                            }
                                        }
                                    }

                                    #endregion

                                    #region CalculateServerDelay

                                    if (CalculateServerDelay)
                                    {
                                        RtspHeaders.TryParseTimestamp(m_LastTransmitted[RtspHeaders.Timestamp], out string timestamp, out m_LastServerDelay);

                                        timestamp = null;
                                    }

                                    #endregion

                                    #region UpdateSession

                                    if (string.IsNullOrWhiteSpace(SessionId) is false)
                                    {
                                        UpdateMessages(message, m_LastTransmitted);
                                    }

                                    #endregion

                                    //Raise an event for the message received
                                    Received(message, m_LastTransmitted);

                                    //Todo, Event => CloseRequested...
                                    ////string connection = m_LastTransmitted.GetHeader(RtspHeaders.Connection);

                                    ////if (string.IsNullOrWhiteSpace(connection) is false && connection.IndexOf("close", StringComparison.InvariantCultureIgnoreCase) >= 0)
                                    ////{
                                    ////    Disconnect(true);

                                    ////    if (AutomaticallyReconnect)
                                    ////    {
                                    ////        Connect();
                                    ////    }
                                    ////}

                                }//This client is in use...


                                break;

                            case RtspMessageType.Invalid: break;
                        }
                    }

                    #endregion
                }
                catch (Exception ex)
                {
                    Common.ILoggingExtensions.Log(Logger, ToString() + "@SendRtspMessage: " + ex.Message);
                }
                finally
                {
                    //Unblock (should not be needed)
                    if (wasBlocked is false) m_InterleaveEvent.Set();
                }

                //Return the result
                //return message is not null && m_LastTransmitted is not null && message.CSeq == m_LastTransmitted.CSeq ? m_LastTransmitted : null;
                return m_LastTransmitted;

            }//Unchecked
        }

        /// <summary>
        /// Uses the given request to Authenticate the RtspClient when challenged.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="force"></param>
        /// <returns></returns>
        public virtual RtspMessage Authenticate(RtspMessage request, RtspMessage response = null, bool force = false)
        {
            //If not forced and already TriedCredentials and there was no response then return null.

            //StackOverflow baby..
            if (force is false && TriedCredentials && Common.IDisposedExtensions.IsNullOrDisposed(response)) return response;

            #region Example header

            //http://tools.ietf.org/html/rfc2617
            //3.2.1 The WWW-Authenticate Response Header
            //Example
            //WWW-Authenticate: Basic realm="nmrs_m7VKmomQ2YM3:", Digest realm="GeoVision", nonce="b923b84614fc11c78c712fb0e88bc525"\r\n

            #endregion

            //If there was a response get the WWWAuthenticate header from it.

            string authenticateHeader = response is null ? string.Empty : response[RtspHeaders.WWWAuthenticate];

            //Basic auth shouldn't expire, but to be supported there should be an AuthenticationState class which
            //holds the state for Authentication, e.g. LastAuthenticationTime, Attempts etc.
            //Then using that we can really narrow down if the Auth is expired or just not working.

            //For now, if there was no header or if we already tried to authenticate and the header doesn't contain "stale" then return the response given.
            if (string.IsNullOrWhiteSpace(authenticateHeader) || TriedCredentials)
            {

                int staleIndex = authenticateHeader.IndexOf(RtspHeaderFields.Authorization.Attributes.Stale, StringComparison.OrdinalIgnoreCase),
                    whiteSpaceAfter;

                if (staleIndex < 0) return response;

                whiteSpaceAfter = authenticateHeader.IndexOf(RtspHeaderFields.Authorization.Attributes.Stale, staleIndex);

                if (whiteSpaceAfter >= 0)
                {
                    //Stale= (6 chars)
                    authenticateHeader = authenticateHeader.Substring(staleIndex + 6, authenticateHeader.Length - (6 + whiteSpaceAfter));
                }
                else
                {
                    authenticateHeader = authenticateHeader.Substring(staleIndex);
                }

                bool stl;

                if (bool.TryParse(authenticateHeader, out stl))
                {
                    if (stl is false) return response;
                }
            }

            //Note should not be using ASCII, the request and response have the characters already encoded.

            //Should also be a hash broken up by key appropriately.

            //Get the tokens in the header
            //Todo, use response.m_StringWhiteSpace to ensure the encoding is parsed correctly...
            string[] baseParts = authenticateHeader.Split(Media.Common.Extensions.Linq.LinqExtensions.Yield(((char)Common.ASCII.Space)).ToArray(), 2, StringSplitOptions.RemoveEmptyEntries);

            //If nothing was in the header then return the response given.
            if (baseParts.Length is 0) return response;
            else if (baseParts.Length > 1) baseParts = Media.Common.Extensions.Linq.LinqExtensions.Yield(baseParts[0]).Concat(baseParts[1].Split(RtspHeaders.Comma).Select(s => s.Trim())).ToArray();

            if (string.Compare(baseParts[0].Trim(), RtspHeaderFields.Authorization.Basic, true) is 0 || m_AuthenticationScheme == AuthenticationSchemes.Basic)
            {
                AuthenticationScheme = AuthenticationSchemes.Basic;

                request.SetHeader(RtspHeaders.Authorization, m_AuthorizationHeader = RtspHeaders.BasicAuthorizationHeader(request.ContentEncoding, Credential));

                request.RemoveHeader(RtspHeaders.Timestamp);

                request.RemoveHeader(RtspHeaders.CSeq);

                request.Transferred = null;

                //Recurse the call with the info from then authenticate header
                return SendRtspMessage(request);
            }
            else if (string.Compare(baseParts[0].Trim(), RtspHeaderFields.Authorization.Digest, true) is 0 || m_AuthenticationScheme == AuthenticationSchemes.Digest)
            {
                AuthenticationScheme = AuthenticationSchemes.Digest;

                //May use a different algorithmm
                string algorithm = baseParts.Where(p => p.StartsWith(RtspHeaderFields.Authorization.Attributes.Algorithm, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();

                //Todo, RtspHeaderFields.Authorization.Attributes.Algorithms...

                if (string.IsNullOrWhiteSpace(algorithm)) algorithm = "MD5";
                else
                {
                    if (algorithm.IndexOf("MD5", 10, StringComparison.InvariantCultureIgnoreCase) >= 0) algorithm = "MD5";
                    else Media.Common.TaggedExceptionExtensions.RaiseTaggedException(response, "See the response in the Tag.", new NotSupportedException("The algorithm indicated in the authenticate header is not supported at this time. Create an issue for support."));
                }

                string username = baseParts.Where(p => p.StartsWith(RtspHeaderFields.Authorization.Attributes.UserName, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(username) is false) username = username.Substring(9);
                else username = Credential.UserName; //use the username of the credential.

                string realm = Credential.Domain;

                //Get the realm if we don't have one.
                if (string.IsNullOrWhiteSpace(realm))
                {
                    //Check for the realm token
                    realm = baseParts.Where(p => p.StartsWith(RtspHeaderFields.Authorization.Attributes.Realm, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();

                    //If it was there
                    if (string.IsNullOrWhiteSpace(realm) is false)
                    {
                        //Parse it
                        realm = realm.Substring(6).Replace("\"", string.Empty).Replace("\'", string.Empty).Trim();

                        //Store it
                        Credential.Domain = realm;
                    }
                }

                string nc = baseParts.Where(p => p.StartsWith(RtspHeaderFields.Authorization.Attributes.Nc, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(nc) is false) nc = nc.Substring(3);

                string nonce = baseParts.Where(p => p.StartsWith(RtspHeaderFields.Authorization.Attributes.Nonce, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(nonce) is false) nonce = nonce.Substring(6).Replace("\"", string.Empty).Replace("\'", string.Empty);

                string cnonce = baseParts.Where(p => p.StartsWith(RtspHeaderFields.Authorization.Attributes.Cnonce, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(cnonce) is false)
                {

                    //if (Common.IDisposedExtensions.IsNullOrDisposed(m_LastTransmitted) is false)
                    //{
                    //    cnonce = "";
                    //}

                    cnonce = cnonce.Substring(7).Replace("\"", string.Empty).Replace("\'", string.Empty);
                }

                string uri = baseParts.Where(p => p.StartsWith(RtspHeaderFields.Authorization.Attributes.Uri, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                bool rfc2069 = string.IsNullOrWhiteSpace(uri) is false && uri.Contains(RtspHeaders.HyphenSign) is false;

                if (string.IsNullOrWhiteSpace(uri) is false)
                {
                    if (rfc2069) uri = uri.Substring(4);
                    else uri = uri.Substring(11);
                }

                string qop = baseParts.Where(p => string.Compare(RtspHeaderFields.Authorization.Attributes.QualityOfProtection, p, true) is 0).FirstOrDefault();

                if (string.IsNullOrWhiteSpace(qop) is false)
                {
                    qop = qop.Replace("qop=", string.Empty);
                    if (string.IsNullOrWhiteSpace(nc) is false && nc.Length > 3) nc = nc.Substring(3);
                }

                string opaque = baseParts.Where(p => p.StartsWith(RtspHeaderFields.Authorization.Attributes.Opaque, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
                if (string.IsNullOrWhiteSpace(opaque) is false) opaque = opaque.Substring(7);

                //Set the header and store it for use later.
                request.SetHeader(RtspHeaders.Authorization, m_AuthorizationHeader = RtspHeaders.DigestAuthorizationHeader(request.ContentEncoding, request.RtspMethod, request.Location, Credential, qop, nc, nonce, cnonce, opaque, rfc2069, algorithm, request.Body));

                //Todo 'Authorization' property?

                request.RemoveHeader(RtspHeaders.Timestamp);
                request.RemoveHeader(RtspHeaders.CSeq);

                request.Transferred = null;

                //Recurse the call with the info from then authenticate header
                return SendRtspMessage(request);
            }
            else
            {
                throw new NotSupportedException("The given Authorization type is not supported, '" + baseParts[0] + "' Please use Basic or Digest.");
            }
        }

        /// <summary>
        /// DisconnectsSockets, Connects and optionally reconnects the Transport if reconnectClient is true.
        /// </summary>
        /// <param name="reconnectClient"></param>
        internal protected virtual void Reconnect(bool reconnectClient = true)
        {
            DisconnectSocket();

            Connect();

            if (reconnectClient && IsPlaying && m_RtpClient.IsActive is false) m_RtpClient.Activate();

            m_AuthorizationHeader = null;
        }

        public event RtspClientAction OnConnect;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal protected void OnConnected()
        {
            if (Common.IDisposedExtensions.IsNullOrDisposed(this)) return;

            RtspClientAction action = OnConnect;

            if (action is null) return;

            foreach (RtspClientAction handler in action.GetInvocationList().Cast<RtspClientAction>())
            {
                try { handler(this.m_Client, EventArgs.Empty); }
                catch (Exception e)
                {
                    Common.ILoggingExtensions.LogException(Logger, e);

                    break;
                }
            }

        }

        /// <summary>
        /// If <see cref="IsConnected"/> nothing occurs.
        /// Disconnects the RtspSocket if Connected and <see cref="LeaveOpen"/> is false.  
        /// Sets the <see cref="ConnectionTime"/> to <see cref="Utility.InfiniteTimepan"/> so IsConnected is false.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        public void DisconnectSocket(bool force = false)
        {
            //If not connected and not forced return
            if (IsConnected is false && force is false) return;

            //When disconnecting the credentials must be used again when re-connecting.
            m_AuthorizationHeader = null;

            //Raise an event
            OnDisconnected();

            //If there is a socket
            if (m_RtspSocket is not null)
            {
                //If LeaveOpen was false and the socket is not shared.
                if (force || LeaveOpen is false && SharesSocket is false)
                {
                    #region The Great Debate on Closing

                    //Don't allow further sending
                    //m_RtspSocket.Shutdown(SocketShutdown.Send);

                    //Should receive any data in buffer while not getting 0?

                    //m_RtspSocket.Close();

                    //May take to long because of machine level settings.
                    //m_RtspSocket.Deactivate(true);

                    #endregion

                    //Dispose the socket
                    m_RtspSocket.Dispose();
                }

                //Set the socket to null (no longer will Share Socket)
                m_RtspSocket = null;

                //Reset the event to prevent further writing on this instance because the socket is still in use and now is owned by the RtpClient.
                // m_InterleaveEvent.Reset();
            }

            //Indicate not connected.
            m_BeginConnect = m_EndConnect = null;

            m_ConnectionTime = Media.Common.Extensions.TimeSpan.TimeSpanExtensions.InfiniteTimeSpan;
        }

        public event RtspClientAction OnDisconnect;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal void OnDisconnected()
        {
            if (Common.IDisposedExtensions.IsNullOrDisposed(this)) return;

            RtspClientAction action = OnDisconnect;

            if (action is null) return;

            foreach (RtspClientAction handler in action.GetInvocationList().Cast<RtspClientAction>())
            {
                try { handler(this.m_Client, EventArgs.Empty); }
                catch (Exception e)
                {
                    Common.ILoggingExtensions.LogException(Logger, e);

                    break;
                }
            }
        }

        public event RequestHandler OnRequest;

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal protected void Requested(RtspMessage request)
        {
            if (Common.IDisposedExtensions.IsNullOrDisposed(this)) return;

            RequestHandler action = OnRequest;

            if (action is null) return;

            foreach (RequestHandler handler in action.GetInvocationList().Cast<RequestHandler>())
            {
                try { handler(this.m_Client, request); }
                catch (Exception e)
                {
                    Common.ILoggingExtensions.LogException(Logger, e);

                    break;
                }
            }
        }

        public event ResponseHandler OnResponse; // = m_LastTransmitted...

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal protected void Received(RtspMessage request, RtspMessage response)
        {
            if (Common.IDisposedExtensions.IsNullOrDisposed(this)) return;

            ResponseHandler action = OnResponse;

            if (action is null) return;

            foreach (ResponseHandler handler in action.GetInvocationList().Cast<ResponseHandler>())
            {
                try { handler(this.m_Client, request, response); }
                catch (Exception e)
                {
                    Common.ILoggingExtensions.LogException(Logger, e);

                    break;
                }
            }
        }

        public void UpdateMessages(RtspMessage request, RtspMessage response)
        {
            if (Common.IDisposedExtensions.IsNullOrDisposed(request) is false &&
                Common.IDisposedExtensions.IsNullOrDisposed(LastRequest) is false)
            {
                LastRequest.IsPersistent = false;

                LastRequest.Dispose();
            }

            if (Common.IDisposedExtensions.IsNullOrDisposed(LastRequest = request) is false)
            {
                LastRequest.IsPersistent = true;
            }

            if (Common.IDisposedExtensions.IsNullOrDisposed(LastResponse) is false)
            {
                LastResponse.IsPersistent = false;

                LastResponse.Dispose();
            }

            if (Common.IDisposedExtensions.IsNullOrDisposed(LastResponse = response) is false)
            {
                LastResponse.IsPersistent = true;
            }
        }

        public void UpdatePushedMessages(RtspMessage request, RtspMessage response)
        {
            if (Common.IDisposedExtensions.IsNullOrDisposed(request) is false && Common.IDisposedExtensions.IsNullOrDisposed(LastInboundRequest) is false)
            {
                LastInboundRequest.IsPersistent = false;

                LastInboundRequest.Dispose();
            }


            if (Common.IDisposedExtensions.IsNullOrDisposed(LastInboundRequest = request) is false)
            {
                LastInboundRequest.IsPersistent = true;
            }

            if (Common.IDisposedExtensions.IsNullOrDisposed(LastInboundResponse) is false)
            {
                LastInboundResponse.IsPersistent = false;

                LastInboundResponse.Dispose();
            }

            if (Common.IDisposedExtensions.IsNullOrDisposed(LastInboundResponse = response) is false)
            {
                LastInboundResponse.IsPersistent = true;
            }
        }

        public bool ParseSessionIdAndTimeout(RtspMessage from)
        {
            SessionId = from[RtspHeaders.Session];

            Timeout = System.TimeSpan.FromSeconds(60);//Default

            //If there is a session header it may contain the option timeout
            if (false == string.IsNullOrWhiteSpace(SessionId))
            {
                //Check for session and timeout

                //Get the values
                string[] sessionHeaderParts = SessionId.Split(RtspHeaders.SemiColon);

                int headerPartsLength = sessionHeaderParts.Length;

                //Check if a valid value was given
                if (headerPartsLength > 0)
                {
                    //Trim it of whitespace
                    string value = System.Linq.Enumerable.LastOrDefault(sessionHeaderParts, (p => false == string.IsNullOrWhiteSpace(p)));

                    //If we dont have an exiting id then this is valid if the header was completely recieved only.
                    if (false == string.IsNullOrWhiteSpace(value) &&
                        true == string.IsNullOrWhiteSpace(SessionId) ||
                        value[0] != SessionId[0])
                    {
                        //Get the SessionId if present
                        SessionId = sessionHeaderParts[0].Trim();

                        //Check for a timeout
                        if (sessionHeaderParts.Length > 1)
                        {
                            string timeoutPart = sessionHeaderParts[1];

                            if (false == string.IsNullOrWhiteSpace(timeoutPart))
                            {
                                int timeoutStart = 1 + timeoutPart.IndexOf(Media.Sdp.SessionDescription.EqualsSign);

                                if (timeoutStart >= 0 && int.TryParse(timeoutPart.AsSpan(timeoutStart), out timeoutStart))
                                {
                                    if (timeoutStart > 0)
                                    {
                                        Timeout = System.TimeSpan.FromSeconds(timeoutStart);
                                    }
                                }
                            }
                        }

                        value = null;
                    }
                }

                sessionHeaderParts = null;

                return true;
            }

            return false;
        }

        public string TransportHeader;

        public System.TimeSpan LastServerDelay { get; protected set; }

        public void ParseDelay(RtspMessage from)
        {
            //Determine if delay was honored.
            string timestampHeader = from.GetHeader(RtspHeaders.Timestamp);

            //If there was a Timestamp header
            if (false == string.IsNullOrWhiteSpace(timestampHeader))
            {
                timestampHeader = timestampHeader.Trim();

                //check for the delay token
                int indexOfDelay = timestampHeader.IndexOf("delay=");

                //if present
                if (indexOfDelay >= 0)
                {
                    //attempt to calculate it from the given value
                    if (double.TryParse(timestampHeader.Substring(indexOfDelay + 6).TrimEnd(), out double delay))
                    {
                        //Set the value of the servers delay
                        LastServerDelay = System.TimeSpan.FromSeconds(delay);

                        //Could add it to the existing SocketReadTimeout and SocketWriteTimeout.
                    }
                }
                else
                {
                    //MS servers don't use a ; to indicate delay
                    string[] parts = timestampHeader.Split(RtspMessage.SpaceSplit, 2);

                    //If there was something after the space
                    if (parts.Length > 1)
                    {
                        //attempt to calulcate it from the given value
                        if (double.TryParse(parts[1].Trim(), out double delay))
                        {
                            //Set the value of the servers delay
                            LastServerDelay = System.TimeSpan.FromSeconds(delay);
                        }
                    }

                }
            }
        }

        public void Timestamp(RtspMessage message)
        {
            string timestamp = ((DateTime.UtcNow - m_EndConnect) ?? TimeSpan.Zero).TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);

            message.SetHeader(RtspHeaders.Timestamp, timestamp);
        }

        /// <summary>
        /// If <see cref="IsConnected"/> and not forced an <see cref="InvalidOperationException"/> will be thrown.
        /// 
        /// <see cref="DisconnectSocket"/> is called if there is an existing socket.
        /// 
        /// Creates any required client socket stored the time the call was made and calls <see cref="ProcessEndConnect"/> unless an unsupported Proctol is specified.
        /// </summary>
        /// <param name="force">Indicates if a previous existing connection should be disconnected.</param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.Synchronized)]
        public virtual void Connect(bool force = false)
        {
            try
            {
                //Ensure logic for UDP is correct, may have to store flag.

                //If not forcing and is already connected or started to connect return
                if (force is false && IsConnected || m_BeginConnect.HasValue) return;

                //If there is an RtpClient already connected then attempt to find a socket used by the client with the EndPoint
                //required to be connected to
                if (Common.IDisposedExtensions.IsNullOrDisposed(m_RtpClient) && m_RtpClient.IsActive)
                {
                    //Todo, should be interface.
                    foreach (RtpClient.TransportContext transportContext in m_RtpClient.GetTransportContexts())
                    {
                        //If disposed continue, should be handled in GetTransportContexts()..
                        if (Common.IDisposedExtensions.IsNullOrDisposed(transportContext) || transportContext.IsActive is false) continue;

                        //Get the sockets in reference by the context
                        foreach (Socket socket in ((ISocketReference)transportContext).GetReferencedSockets())
                        {
                            //Check for the socket to not be disposed...
                            if (socket is null || socket.Connected is false) continue;

                            IPEndPoint ipendPoint = (IPEndPoint)socket.RemoteEndPoint;

                            if (ipendPoint.Address.Equals(m_RemoteIP) &&
                                ipendPoint.Port.Equals(m_RtspPort) &&
                                socket.Connected)
                            {
                                //Assign the socket (Update ConnectionTime etc)>
                                m_RtspSocket = socket;

                                //m_InterleaveEvent.Reset();

                                return;
                            }
                        }

                    }
                }

                //Wait for existing writes
                //m_InterleaveEvent.Wait();

                //Deactivate any existing previous socket and erase connect times.
                if (m_RtspSocket is not null) DisconnectSocket();

                //Based on the ClientProtocolType
                switch (m_RtspProtocol)
                {
                    case ClientProtocolType.Http:
                    case ClientProtocolType.Tcp:
                        {
                            /*  9.2 Reliability and Acknowledgements
                             If a reliable transport protocol is used to carry RTSP, requests MUST
                             NOT be retransmitted; the RTSP application MUST instead rely on the
                             underlying transport to provide reliability.
                             * 
                             If both the underlying reliable transport such as TCP and the RTSP
                             application retransmit requests, it is possible that each packet
                             loss results in two retransmissions. The receiver cannot typically
                             take advantage of the application-layer retransmission since the
                             transport stack will not deliver the application-layer
                             retransmission before the first attempt has reached the receiver.
                             If the packet loss is caused by congestion, multiple
                             retransmissions at different layers will exacerbate the congestion.
                             * 
                             If RTSP is used over a small-RTT LAN, standard procedures for
                             optimizing initial TCP round trip estimates, such as those used in
                             T/TCP (RFC 1644) [22], can be beneficial.
                             * 
                            The Timestamp header (Section 12.38) is used to avoid the
                            retransmission ambiguity problem [23, p. 301] and obviates the need
                            for Karn's algorithm.
                             * 
                           Each request carries a sequence number in the CSeq header (Section
                           12.17), which is incremented by one for each distinct request
                           transmitted. If a request is repeated because of lack of
                           acknowledgement, the request MUST carry the original sequence number
                           (i.e., the sequence number is not incremented).
                             * 
                           Systems implementing RTSP MUST support carrying RTSP over TCP and MAY
                           support UDP. The default port for the RTSP server is 554 for both UDP
                           and TCP.
                             * 
                           A number of RTSP packets destined for the same control end point may
                           be packed into a single lower-layer PDU or encapsulated into a TCP
                           stream. RTSP data MAY be interleaved with RTP and RTCP packets.
                           Unlike HTTP, an RTSP message MUST contain a Content-Length header
                           whenever that message contains a payload. Otherwise, an RTSP packet
                           is terminated with an empty line immediately following the last
                           message header.
                             * 
                            */

                            //Create the socket
                            m_RtspSocket = new Socket(m_RemoteIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                            break;
                        }
                    case ClientProtocolType.Udp:
                        {
                            //Create the socket
                            m_RtspSocket = new Socket(m_RemoteIP.AddressFamily, SocketType.Dgram, ProtocolType.Udp);

                            break;
                        }
                    default: throw new NotSupportedException("The given ClientProtocolType is not supported.");
                }

                if (ConfigureSocket is not null)
                {
                    ConfigureSocket(m_RtspSocket);

                    //Socket must be connected to get mss... however you can specify it beforehand.
                    //Todo, MaxSegmentSize based on receive, send or both?
                    //Should also account for the IP Header....


                    //Media.Common.Extensions.Socket.SocketExtensions.SetMaximumSegmentSize(m_RtspSocket, m_Buffer.Count >> 2);

                    //if (m_RtspSocket.AddressFamily == AddressFamily.InterNetwork)
                    //{
                    //    //Media.Common.Extensions.Socket.SocketExtensions.SetMaximumSegmentSize(socket, Media.Common.Extensions.NetworkInterface.NetworkInterfaceExtensions.GetNetworkInterface(socket).GetIPProperties().GetIPv6Properties().Mtu);                        
                    //}
                    //else if (m_RtspSocket.AddressFamily == AddressFamily.InterNetworkV6)
                    //{
                    //    //Media.Common.Extensions.Socket.SocketExtensions.SetMaximumSegmentSize(socket, Media.Common.Extensions.NetworkInterface.NetworkInterfaceExtensions.GetNetworkInterface(socket).GetIPProperties().GetIPv6Properties().Mtu);
                    //}

                    //int mss;

                    //Media.Common.Extensions.Socket.SocketExtensions.GetMaximumSegmentSize(m_RtspSocket, out mss);

                    //int mtu = Media.Common.Extensions.Socket.SocketExtensions.GetMaximumTransmittableUnit(m_RtspSocket);

                    //if (mtu < mss)
                    //{
                    //    Media.Common.Extensions.Socket.SocketExtensions.SetMaximumSegmentSize(m_RtspSocket, mtu - 42);
                    //}
                    //else
                    //{
                    //    Media.Common.Extensions.Socket.SocketExtensions.SetMaximumSegmentSize(m_RtspSocket, mtu + 42);
                    //}


                }

                //We started connecting now.
                m_BeginConnect = DateTime.UtcNow;

                //Handle the connection attempt (Assumes there is already a RemoteRtsp value)
                ProcessEndConnect(null);

            }
            catch (Exception ex)
            {
                Common.ILoggingExtensions.Log(Logger, ex.Message);

                throw;
            }
        }

        /// <summary>
        /// Calls Connect on the usynderlying socket.
        /// 
        /// Marks the time when the connection was established.
        /// 
        /// Increases the <see cref="SocketWriteTimeout"/> AND <see cref="SocketReadTimeout"/> by the time it took to establish the connection in milliseconds * 2.
        /// 
        /// </summary>
        /// <param name="state">Ununsed.</param>
        protected virtual void ProcessEndConnect(object state, int multiplier = 2)//should be vaarible in class
        {
            //Todo,
            //IConnection

            if (m_RemoteRtsp is null) throw new InvalidOperationException("A remote end point must be assigned");

            //Todo, BeginConnect will allow the amount of time to be specified and then you can cancel the connect if it doesn't finish within that time.
            //System.Net.Sockets.Socket s = new Socket(m_RtspSocket.SocketType, m_RtspSocket.ProtocolType);

            //bool async = false;

            //bool fail = false;

            //var cc = s.BeginConnect(m_RemoteRtsp, new AsyncCallback((iar)=>{

            //    if (iar == null || iar.IsCompleted is false || s == null) return;                    

            //    if(s.Connected) s.EndConnect(iar);

            //    if (async)
            //    {
            //        if (s.Connected is false) s.Dispose();

            //        s = null;
            //    }
            //    else
            //    {
            //        async = true;

            //        m_RtspSocket.Dispose();

            //        m_RtspSocket = s;
            //    }
            //}), null);

            //ThreadPool.QueueUserWorkItem((_) =>
            //{
            //    while (async is false) if (DateTime.UtcNow - m_BeginConnect.Value > Common.Extensions.TimeSpan.TimeSpanExtensions.OneSecond)
            //        {
            //            async = true;

            //            fail = true;

            //            using (cc.AsyncWaitHandle)
            //            {
            //                s.Dispose();

            //                m_RtspSocket.Dispose();
            //            }

            //            s = null;
            //        }
            //        else System.Threading.Thread.Yield();
            //});

            //if (async is false && cc.IsCompleted is false)
            //{
            //Try to connect.
            m_RtspSocket.Connect(m_RemoteRtsp);

            //    async = true;
            //}

            //if (fail) return;

            //Sample the clock after connecting
            m_EndConnect = DateTime.UtcNow;

            //Calculate the connection time.
            m_ConnectionTime = m_EndConnect.Value - m_BeginConnect.Value;

            //When timeouts are set then ensure they are within the amount of time the connection took to establish
            if ((SocketWriteTimeout + SocketReadTimeout) <= 0)
            {
                //Possibly in a VM the timing may be off (Hardware Abstraction Layer BUGS) and if the timeout occurs a few times witin the R2 the socket may be closed
                //To prefent this check the value first.
                int multipliedConnectionTime = (int)(m_ConnectionTime.TotalMilliseconds * multiplier);

                ////If it took longer than 50 msec to connect 
                //if (multipliedConnectionTime > SocketWriteTimeout ||
                //    multipliedConnectionTime > SocketReadTimeout)
                //{
                //    ////Set the read and write timeouts based upon such a time (should include a min of the m_RtspSessionTimeout.)
                //    //if (m_ConnectionTime > TimeSpan.Zero)
                //    //{
                //    //    //Set read and write timeout...
                //    //    SocketWriteTimeout = SocketReadTimeout = multipliedConnectionTime; //(int)DefaultConnectionTime.TotalMilliseconds;
                //    //}
                //    //....else 
                //}

                //Set the connection time using the multiplied value
                m_ConnectionTime = System.TimeSpan.FromMilliseconds(multipliedConnectionTime);
            }

            //Determine the poll time now.
            m_SocketPollMicroseconds = Media.Common.Binary.Min((int)Media.Common.Extensions.TimeSpan.TimeSpanExtensions.TotalMicroseconds(m_ConnectionTime), m_SocketPollMicroseconds);

            //Use the multiplier to set the poll time.
            //m_SocketPollMicroseconds >>= multiplier;

            //The Send and Receive Timeout values are maintained as whatever they are when the socket was created.

            //Todo, post configure socket, offer API in SocketReference with Connection time overload as well as logger.

            //If the protocol is TCP
            if (m_RtspSocket.ProtocolType == ProtocolType.Tcp)
            {
                //If the connection time was >= 500 msec enable congestion algorithm
                if (ConnectionTime.TotalMilliseconds >= DefaultConnectionTime.TotalMilliseconds)
                {
                    // Enable CongestionAlgorithm
                    Common.Extensions.Exception.ExceptionExtensions.ResumeOnError(() => Media.Common.Extensions.Socket.SocketExtensions.EnableTcpCongestionAlgorithm(m_RtspSocket));
                }
            }

            //Don't block (possibly another way to work around the issue)
            //m_RtspSocket.Blocking = false;                

            //Raise the Connected event.
            OnConnected();
        }

        #endregion

        #region Overloads

        /// <summary>
        /// Disposes <see cref="LastRequest"/> and <see cref="LastResponse"/>.
        /// Removes any references stored in the instance.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing is false || ShouldDispose is false) return;

            base.Dispose(ShouldDispose);

            //If there is a LastRequest
            if (LastRequest is not null)
            {
                //It is no longer persistent
                using (LastRequest) LastRequest.IsPersistent = false;

                //It is no longer scoped.
                LastRequest = null;
            }

            //If there is a LastResponse
            if (LastResponse is not null)
            {
                //It is no longer persistent
                using (LastResponse) LastResponse.IsPersistent = false;

                //It is no longer scoped.
                LastResponse = null;
            }

            //If there is a SessionDescription
            if (Common.IDisposedExtensions.IsNullOrDisposed(SessionDescription) is false)
            {
                //Call dispose
                SessionDescription.Dispose();

                //Remove the reference
                SessionDescription = null;
            }

            //If there is a MediaDescription
            //if (MediaDescription is not null)
            //{
            //    //Call dispose
            //    //MediaDescription.Dispose();

            //    //Remove the reference
            //    MediaDescription = null;
            //}

            //If there is a Context
            if (Context is not null)
            {
                //Call dispose
                //Context.Dispose();

                //Remove the reference
                Context = null;
            }

            TransportHeader = null;
        }

        #endregion
    }
}
