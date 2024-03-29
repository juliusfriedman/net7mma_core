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

using System;
using System.Collections.Generic;
using System.Net;

namespace Media.Rtsp.Server.MediaTypes
{
    /// <summary>
    /// A remote stream the RtspServer aggregates and can be played by clients.
    /// </summary>    
    public class RtspSource : RtpSource
    {
        //needs to have a way to indicate the stream should be kept in memory for play on demand from a source which is not continious, e.g. archiving / caching etc.
        //public static RtspChildStream CreateChild(RtspSourceStream source) { return new RtspChildStream(source); }        

        /// <summary>
        /// If not null the only type of media which will be setup from the source.
        /// </summary>
        public readonly IEnumerable<Sdp.MediaType> SpecificMediaTypes;

        /// <summary>
        /// If not null, The time at which to start the media in the source.
        /// </summary>
        public readonly TimeSpan? MediaStartTime, MediaEndTime;

        #region Properties

        /// <summary>
        /// Gets the RtspClient this RtspSourceStream uses to provide media
        /// </summary>
        public virtual RtspClient RtspClient
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set;
        }

        /// <summary>
        /// Gets the RtpClient used by the RtspClient to provide media
        /// </summary>
        public override Rtp.RtpClient RtpClient
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return IsDisposed ? null : Common.IDisposedExtensions.IsNullOrDisposed(RtspClient) ? null : RtspClient.Client; }
        }

        public override NetworkCredential SourceCredential
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get
            {
                return base.SourceCredential;
            }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set
            {
                if (RtspClient is not null) RtspClient.Credential = value;
                base.SourceCredential = value;
            }
        }

        public override AuthenticationSchemes SourceAuthenticationScheme
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get
            {
                return base.SourceAuthenticationScheme;
            }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set
            {
                if (RtspClient is not null) RtspClient.AuthenticationScheme = value;
                base.SourceAuthenticationScheme = value;
            }
        }

        /// <summary>
        /// SessionDescription from the source RtspClient
        /// </summary>
        public override Sdp.SessionDescription SessionDescription
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return RtspClient.SessionDescription; }
        }

        /// <summary>
        /// Gets or sets the source Uri used in the RtspClient
        /// </summary>
        public override Uri Source
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get
            {
                return base.Source;
            }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set
            {
                //Experimental support for Unreliable and Http enabled with this line commented out
                if (false.Equals(value.Scheme == RtspMessage.ReliableTransportScheme)) throw new ArgumentException("value", "Must have the Reliable Transport scheme \"" + RtspMessage.ReliableTransportScheme + "\"");

                base.Source = value;

                if (Common.IDisposedExtensions.IsNullOrDisposed(RtspClient) is false)
                {
                    bool wasConnected = RtspClient.IsConnected;

                    if (wasConnected) Stop();

                    RtspClient.CurrentLocation = base.Source;

                    if (wasConnected) Start();
                }
            }
        }

        /// <summary>
        /// Indicates if the source RtspClient is Connected and has began to receive data via Rtp
        /// </summary>
        public override bool IsReady
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return base.IsReady && Common.IDisposedExtensions.IsNullOrDisposed(RtspClient) is false && RtspClient.IsPlaying; }
        }

        #endregion

        #region Constructor

        //Todo, make constructor easier to call

        public RtspSource(string name, string location, RtspClient.ClientProtocolType rtpProtocolType, int bufferSize = RtspClient.DefaultBufferSize, Sdp.MediaType? specificMedia = null, TimeSpan? startTime = null, TimeSpan? endTime = null, bool perPacket = false)
            : this(name, location, null, AuthenticationSchemes.None, rtpProtocolType, bufferSize, specificMedia, startTime, endTime, perPacket) { }

        public RtspSource(string name, string sourceLocation, NetworkCredential credential = null, AuthenticationSchemes authType = AuthenticationSchemes.None, Rtsp.RtspClient.ClientProtocolType? rtpProtocolType = null, int bufferSize = RtspClient.DefaultBufferSize, Sdp.MediaType? specificMedia = null, TimeSpan? startTime = null, TimeSpan? endTime = null, bool perPacket = false)
            : this(name, new Uri(sourceLocation), credential, authType, rtpProtocolType, bufferSize, specificMedia.HasValue ? Common.Extensions.Linq.LinqExtensions.Yield(specificMedia.Value) : null, startTime, endTime, perPacket)
        {
            //Check for a null Credential and UserInfo in the Location given.
            if (credential is null && !string.IsNullOrWhiteSpace(m_Source.UserInfo))
            {
                RtspClient.Credential = Media.Common.Extensions.Uri.UriExtensions.ParseUserInfo(m_Source);

                //Remove the user info from the location
                RtspClient.CurrentLocation = new Uri(RtspClient.CurrentLocation.AbsoluteUri.Replace(RtspClient.CurrentLocation.UserInfo + (char)Common.ASCII.AtSign, string.Empty).Replace(RtspClient.CurrentLocation.UserInfo, string.Empty));
            }
        }

        public RtspSource(string name, Uri source, bool perPacket, RtspClient client)
            : base(name, source, perPacket)
        {
            if (Common.IDisposedExtensions.IsNullOrDisposed(client)) throw new ArgumentNullException("client");

            RtspClient = client;
        }

        /// <summary>
        /// Constructs a RtspStream for use in a RtspServer
        /// </summary>
        /// <param name="name">The name given to the stream on the RtspServer</param>
        /// <param name="sourceLocation">The rtsp uri to the media</param>
        /// <param name="credential">The network credential the stream requires</param>
        /// /// <param name="authType">The AuthenticationSchemes the stream requires</param>
        public RtspSource(string name, Uri sourceLocation, NetworkCredential credential = null, AuthenticationSchemes authType = AuthenticationSchemes.None, Rtsp.RtspClient.ClientProtocolType? rtpProtocolType = null, int bufferSize = RtspClient.DefaultBufferSize, IEnumerable<Sdp.MediaType> specificMedia = null, TimeSpan? startTime = null, TimeSpan? endTime = null, bool perPacket = false)
            : base(name, sourceLocation, perPacket)
        {
            //Create the listener if we are the top level stream (Parent)
            if (IsParent)
            {
                RtspClient = new RtspClient(m_Source, rtpProtocolType, bufferSize);

                RtspClient.OnConnect += RtspClient_OnConnect;

                RtspClient.OnDisconnect += RtspClient_OnDisconnect;

                RtspClient.OnPlay += RtspClient_OnPlay;

                RtspClient.OnPause += RtspClient_OnPausing;

                RtspClient.OnStop += RtspClient_OnStop;
            }
            //else it is already assigned via the child

            if (credential is not null)
            {
                RtspClient.Credential = SourceCredential = credential;

                if (false.Equals(authType == AuthenticationSchemes.None)) RtspClient.AuthenticationScheme = SourceAuthenticationScheme = authType;
            }

            //If only certain media should be setup 
            if (specificMedia is not null) SpecificMediaTypes = specificMedia;

            //If there was a start time given
            if (startTime.HasValue) MediaStartTime = startTime;

            if (endTime.HasValue) MediaEndTime = endTime;
        }

        #endregion

        /// <summary>
        /// Beings streaming from the source
        /// </summary>
        public override void Start()
        {
            if (IsDisposed || State >= StreamState.StopRequested) return;

            //May have to re-create client.

            try
            {
                RtspClient.Connect();
            }
            catch (Exception ex)
            {
                Common.ILoggingExtensions.LogException(RtspClient.Logger, ex);

                RtspClient.StopPlaying();

                RtspClient.Disconnect();
            }
        }

        private void RtspClient_OnStop(RtspClient sender, object args)
        {
            base.IsReady = RtspClient.IsPlaying;

            //Should also push event to all clients that the stream is stopping.
        }

        private void RtspClient_OnPlay(RtspClient sender, object args)
        {
            State = StreamState.Started;

            if ((base.IsReady = RtspClient.IsPlaying)) //  && RtspClient.PlayingMedia.Count is equal to what is supposed to be playing
            {
                RtspClient.Client.ThreadEvents = RtspClient.Client.FrameChangedEventsEnabled = PerPacket is false;

                //RtspClient.Client.IListSockets = true;

                m_StartedTimeUtc = RtspClient.StartedPlaying;
            }
        }

        private void RtspClient_OnDisconnect(RtspClient sender, object args)
        {
            base.IsReady = false;
        }

        private void RtspClient_OnPausing(RtspClient sender, object args)
        {
            base.IsReady = RtspClient.IsPlaying;
        }

        private void RtspClient_OnConnect(RtspClient sender, object args)
        {
            if (RtspClient.IsConnected is false || State == StreamState.StartRequested) return;

            //Not quite ready yet.
            State = StreamState.StartRequested;

            try
            {
                //Start playing
                RtspClient.StartPlaying(MediaStartTime, MediaEndTime, SpecificMediaTypes);
            }
            catch (Exception ex)
            {
                //StoPlaying and Disconnect when an exception occurs.
                RtspClient.Disconnect(true);

                Common.ILoggingExtensions.LogException(RtspClient.Logger, ex);

                State = StreamState.Started;
            }
        }

        public override bool TrySetLogger(Common.ILogging logger)
        {
            if (IsReady is false) return false;

            try
            {
                //Set the rtp logger and the rtsp logger
                RtspClient.Logger = logger;

                return base.TrySetLogger(logger);
            }
            catch { return false; }
        }

        /// <summary>
        /// Stops streaming from the source
        /// </summary>
        public override void Stop()
        {
            if (IsDisposed || State < StreamState.Started) return;

            if (Common.IDisposedExtensions.IsNullOrDisposed(RtspClient) is false)
            {
                if (RtspClient.IsPlaying) RtspClient.StopPlaying();
                else if (RtspClient.IsConnected) RtspClient.Disconnect();

                //Client Dispose

            }

            base.Stop();

            m_StartedTimeUtc = null;
        }

        public override void Dispose()
        {
            if (IsDisposed) return;

            base.Dispose();

            if (Common.IDisposedExtensions.IsNullOrDisposed(RtspClient) is false)
            {
                RtspClient.OnConnect -= RtspClient_OnConnect;

                RtspClient.OnDisconnect -= RtspClient_OnDisconnect;

                RtspClient.OnPlay -= RtspClient_OnPlay;

                RtspClient.OnPause -= RtspClient_OnPausing;

                RtspClient.OnStop -= RtspClient_OnStop;

                RtspClient.Dispose();

                RtspClient = null;
            }
        }
    }
}
