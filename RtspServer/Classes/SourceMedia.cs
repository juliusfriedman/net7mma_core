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
using System.Linq;
using System.Net;

namespace Media.Rtsp.Server
{
    /// <summary>
    /// The base class of all sources the RtspServer can service.
    /// </summary>
    /// <remarks>
    /// Provides a way to augment all classes from one place.
    /// API is not yet 100% finalized or complete, yet shouldn't matter as rtp itself is incomplete and still over 20 years later we use it on all of cameras along with rtsp which is complete but completely buggy.
    /// </remarks>
    public abstract class SourceMedia : Common.BaseDisposable, IMediaSource
    {
        internal const string UriScheme = "rtspserver://";

        #region StreamState Enumeration

        public enum StreamState
        {
            Stopped,
            StopRequested,
            Started,
            StartRequested,
            //Faulted,
            Unknown
        }

        #endregion

        #region Fields

        internal DateTime? m_StartedTimeUtc;
        internal Guid m_Id = Guid.NewGuid();
        internal string m_Name;
        internal Uri m_Source;
        internal NetworkCredential m_SourceCred;
        internal HashSet<string> m_Aliases = [];
        //internal bool m_Child = false;
        public virtual Sdp.SessionDescription SessionDescription { get; protected internal set; }

        //Maybe should be m_AllowUdp?
        internal bool m_ForceTCP;//= true; // To force clients to utilize TCP, Interleaved in Rtsp or Rtp

        internal bool m_DisableQOS; //Disabled optional quality of service, In Rtp this is Rtcp

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets a value which indicates if Start can be called.
        /// </summary>
        public bool IsDisabled { get; set; }

        /// <summary>
        /// The amount of time the Stream has been Started
        /// </summary>
        public TimeSpan Uptime
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get
            {
                return m_StartedTimeUtc.HasValue ? DateTime.UtcNow - m_StartedTimeUtc.Value : TimeSpan.MinValue;
            }
        }

        /// <summary>
        /// The unique Id of the RtspStream
        /// </summary>
        public Guid Id
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return m_Id; }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            protected internal set { m_Id = value; }
        }

        /// <summary>
        /// The name of this stream, also used as the location on the server
        /// </summary>
        public virtual string Name
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return m_Name; }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Name", "Cannot be null or consist only of whitespace"); m_Aliases.Add(m_Name); m_Name = value; }
        }

        /// <summary>
        /// Any Aliases the stream is known by
        /// </summary>
        public virtual IEnumerable<string> Aliases
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return m_Aliases; }
        }

        /// <summary>
        /// The credential the source requires
        /// </summary>
        public virtual NetworkCredential SourceCredential
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return m_SourceCred; }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set { m_SourceCred = value; }
        }

        /// <summary>
        /// The type of Authentication the source requires for the SourceCredential
        /// </summary>
        public virtual AuthenticationSchemes SourceAuthenticationScheme { get; set; }

        /// <summary>
        /// Gets a Uri which indicates to the RtspServer the name of this stream reguardless of alias
        /// </summary>
        public virtual Uri ServerLocation
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return new Uri(UriScheme + Id.ToString()); }
        }

        /// <summary>
        /// State of the stream 
        /// </summary>
        public virtual StreamState State { get; protected set; }

        /// <summary>
        /// Is this RtspStream dependent on another
        /// </summary>
        public bool IsParent
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return false == (this is ChildMedia); }
        }

        /// <summary>
        /// The Uri to the source media
        /// </summary>
        public virtual Uri Source
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get { return m_Source; }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            set { m_Source = value; }
        }

        /// <summary>
        /// Indicates the source is ready to have clients connect
        /// </summary>
        public virtual bool IsReady
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            protected set;
        }

        /// <summary>
        /// Indicates if the souce should attempt to decode frames which change.
        /// </summary>
        public bool DecodeFrames
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get;
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            protected set;
        }

        #endregion

        #region Constructor        

        public SourceMedia(string name, Uri source, bool shouldDispose = true)
            : base(shouldDispose)
        {
            //The stream name cannot be null or consist only of whitespace
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("The stream name cannot be null or consist only of whitespace", nameof(name));

            m_Name = name;
            m_Source = source;
        }

        public SourceMedia(string name, Uri source, NetworkCredential sourceCredential, bool shouldDispose = true)
            : this(name, source, shouldDispose)
        {
            m_SourceCred = sourceCredential;
        }

        #endregion

        #region Events        

        public delegate void FrameDecodedHandler(object sender, System.Drawing.Image decoded);

        public delegate void DataDecodedHandler(object sender, byte[] decoded);

        public event FrameDecodedHandler FrameDecoded;

        public event DataDecodedHandler DataDecoded;

        internal void OnFrameDecoded(System.Drawing.Image decoded)
        {
            if (DecodeFrames && decoded is not null)
            {
                FrameDecoded?.Invoke(this, decoded);
            }
        }

        internal void OnFrameDecoded(byte[] decoded)
        {
            if (DecodeFrames && decoded is not null)
            {
                DataDecoded?.Invoke(this, decoded);
            }
        }

        #endregion

        #region Methods

        #region Virtual

        //Sets the logger
        public virtual bool TrySetLogger(Media.Common.ILogging logger)
        {
            //Logger = logger...

            return false;
        }

        public virtual bool TryGetLogger(out Media.Common.ILogging logger)
        {
            logger = null;

            return false;
        }

        //Sets the State = StreamState.Started
        public virtual void Start()
        {
            if (IsDisabled) return;

            State = StreamState.Started;

            m_StartedTimeUtc = DateTime.UtcNow;
        }

        //Sets the State = StreamState.Stopped
        public virtual void Stop()
        {
            State = StreamState.Stopped;

            m_StartedTimeUtc = null;
        }

        #endregion

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void AddAlias(string name)
        {
            if (m_Aliases.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase))) return;

            m_Aliases.Add(name);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void RemoveAlias(string alias)
        {
            m_Aliases.Remove(alias);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void ClearAliases()
        {
            m_Aliases.Clear();
        }

        #endregion

        public override void Dispose()
        {
            if (IsDisposed) return;

            if (SessionDescription is not null)
            {
                SessionDescription.Dispose();
                SessionDescription = null;
            }

            Stop();

            base.Dispose();
        }
    }
}
