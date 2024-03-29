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
using System.Text;

namespace Media.Sdp
{
    #region MediaDescription

    /// <summary>
    /// Represents the MediaDescription in a Session Description.
    /// Parses and Creates.
    /// </summary>
    public class MediaDescription : Common.SuppressedFinalizerDisposable, IEnumerable<SessionDescriptionLine>
    {
        //Nested type for MediaDescriptionLine?

        //Proto fields https://www.iana.org/assignments/sdp-parameters/sdp-parameters-2.csv

        //public sealed class ProtocolFields { const string RealTimeAudioVideoProfile = "RTP/AVP";  }

        #region Fields

        //Created from the m= which is the first line, this is a computed line and not found in Lines.
        protected internal readonly Lines.SessionMediaDescriptionLine MediaDescriptionLine;

        /// <summary>
        /// The MediaType of the MediaDescription
        /// </summary>
        public MediaType MediaType
        {
            get { return MediaDescriptionLine.MediaType; }
            set { MediaDescriptionLine.MediaType = value; }
        }

        /// <summary>
        /// The MediaPort of the MediaDescription as parsed from the port token.
        /// </summary>
        public int MediaPort
        {
            get { return MediaDescriptionLine.MediaPort; }
            set { MediaDescriptionLine.MediaPort = value; }
        }

        /// <summary>
        /// The MediaProtocol of the MediaDescription
        /// </summary>
        public string MediaProtocol
        {
            get { return MediaDescriptionLine.MediaProtocol; }
            set { MediaDescriptionLine.MediaProtocol = value; }
        }

        //Maybe add a few Computed properties such as SampleRate
        //OR
        //Maybe add methods for Get rtpmap, fmtp etc

        //LinesByType etc...

        //Keep in mind that adding/removing or changing lines should change the version of the parent SessionDescription
        internal List<SessionDescriptionLine> m_Lines = [];

        #endregion

        #region Properties

        public bool HasMultiplePorts
        {
            get { return MediaDescriptionLine.HasMultiplePorts; }
        }

        public int NumberOfPorts
        {
            get { return MediaDescriptionLine.NumberOfPorts; }
            set { MediaDescriptionLine.NumberOfPorts = value; }
        }

        /// <summary>
        /// The MediaFormat of the MediaDescription
        /// </summary>
        public string MediaFormat
        {
            get
            {
                return MediaDescriptionLine.MediaFormat;
            }
            protected internal set
            {
                MediaDescriptionLine.MediaFormat = value;
            }
        }

        /// <summary>
        /// Gets or sets the types of payloads which can be found in the MediaDescription
        /// </summary>
        public IEnumerable<int> PayloadTypes
        {
            get
            {
                return MediaDescriptionLine.PayloadTypes;
            }
            protected internal set
            {
                //m_MediaDescriptionLine.PayloadTypes = m_MediaDescriptionLine.PayloadTypes.Concat(value);

                MediaDescriptionLine.PayloadTypes = value;
            }
        }

        public IEnumerable<SessionDescriptionLine> Lines
        {
            get { return this; }
        }

        /// <summary>
        /// Calculates the length in bytes of this MediaDescription.
        /// </summary>
        public int Length
        {
            get
            {
                return MediaDescriptionLine.Length + m_Lines.Sum(l => l.Length);
            }
        }

        #endregion

        #region Constructor

        public MediaDescription(MediaDescription other, bool shouldDispose = true) : base(shouldDispose)
        {
            if (Common.IDisposedExtensions.IsNullOrDisposed(other)) throw new ArgumentNullException(nameof(other));
            MediaDescriptionLine = other.MediaDescriptionLine;
            foreach (Sdp.SessionDescriptionLine line in other.Lines) Add(line);
        }

        public MediaDescription(string mediaDescription)
            : this(mediaDescription.Split(SessionDescription.CRLFSplit, StringSplitOptions.RemoveEmptyEntries), 0)
        {

        }

        public MediaDescription(MediaType mediaType, string mediaProtocol, int mediaFormat, int mediaPort)
            : this(mediaType, mediaProtocol, mediaFormat.ToString(), mediaPort)
        {

        }

        public MediaDescription(MediaType mediaType, string mediaProtocol, string mediaFormat, int mediaPort = 0, bool shouldDispose = true)
            : base(shouldDispose)
        {
            MediaDescriptionLine = [];
            MediaType = mediaType;
            MediaPort = mediaPort;
            MediaProtocol = mediaProtocol;
            MediaFormat = mediaFormat;
        }

        public MediaDescription(string[] sdpLines, int index, bool shouldDispose = true)
            : this(sdpLines, ref index, shouldDispose) { }

        [CLSCompliant(false)]
        public MediaDescription(string[] sdpLines, ref int index, bool shouldDispose = true)
            : base(shouldDispose)
        {
            //Create a MediaDescriptionLine.
            MediaDescriptionLine = new Sdp.Lines.SessionMediaDescriptionLine(sdpLines, ref index);

            //Parse remaining optional entries
            for (int e = sdpLines.Length; index < e;)
            {
                string line = sdpLines[index];

                //NullOrEmptyOrWhiteSpace...

                if (line.StartsWith("m="))
                {
                    //Found the start of another MediaDescription
                    break;
                }
                else
                {

                    if (SessionDescriptionLine.TryParse(sdpLines, ref index, out SessionDescriptionLine parsed)) m_Lines.Add(parsed);
                    else index++;
                }
            }
        }

        #endregion

        #region Methods

        //public Sdp.Lines.FormatTypeLine CreateFormatTypeLine(int payloadType, string parameters)
        //{
        //    return new Lines.FormatTypeLine(payloadType, parameters);
        //}

        //public IEnumerable<Sdp.Lines.FormatTypeLine> CreateFormatTypeLines(string parameters)
        //{
        //    foreach (int payloadType in PayloadTypes)
        //    {
        //        yield return new Lines.FormatTypeLine(payloadType, parameters);
        //    }
        //}

        public void Add(SessionDescriptionLine line)
        {
            if (line is null) return;

            //Should ensure that the line is allowed.

            m_Lines.Add(line);
        }

        public bool Remove(SessionDescriptionLine line)
        {
            return m_Lines.Remove(line);
        }

        internal void Insert(int index, SessionDescriptionLine line)
        {
            m_Lines.Insert(index, line);
        }

        public void RemoveLine(int index)
        {
            m_Lines.RemoveAt(index);
        }

        //Should have a have to get any RtpMap lines which are defined in the Payloadlist

        //GetRtpMapLines

        //GetAttributeLinesForPayloadType(int PayloadType){
        // RtpMapLines.Where(l=> l.m_Parts[0].m_PayloadList
        //}

        #endregion

        #region Overloads

        //[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        //public override bool Equals(object obj)
        //{
        //    if (System.Object.ReferenceEquals(this, obj)) return true;

        //    if (obj is Sdp.Lines.SessionMediaDescriptionLine) return Equals((obj as Sdp.Lines.SessionMediaDescriptionLine));

        //    if (obj is Sdp.SessionDescriptionLine) return this.Contains((obj as Sdp.SessionDescriptionLine));

        //    return Equals(obj as Sdp.MediaDescription);
        //}

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => HashCode.Combine(MediaDescriptionLine.GetHashCode(), m_Lines.GetHashCode());

        public static bool operator ==(MediaDescription a, SessionDescriptionLine b)
        {
            return b is null ? a is null : a.Contains(b);
        }

        public static bool operator !=(MediaDescription a, SessionDescriptionLine b) { return (a == b) is false; }
        public static bool operator ==(MediaDescription a, MediaDescription b)
        {
            return b is null ? a is null : b.Equals(a);
        }

        public static bool operator !=(MediaDescription a, MediaDescription b) { return (a == b) is false; }

        public static bool operator ==(MediaDescription a, Sdp.Lines.SessionMediaDescriptionLine b)
        {
            return b is null ? a is null : b.Equals(a.MediaDescriptionLine);
        }

        public static bool operator !=(MediaDescription a, Sdp.Lines.SessionMediaDescriptionLine b) { return (a == b) is false; }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool Equals(Sdp.Lines.SessionMediaDescriptionLine other) { return this.MediaDescriptionLine.Equals(other); }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public bool Equals(MediaDescription other)
        {
            return Media.Common.Extensions.EnumerableExtensions.SequenceEquals(this, other);

            //using (var one = other.GetEnumerator())
            //{
            //    using (var two = GetEnumerator())
            //    {
            //        while (one.MoveNext() && two.MoveNext())
            //        {
            //            if (one.Current.Equals(two.Current) is false) return false;
            //        }

            //        return true;
            //    }
            //}
        }

        public override bool Equals(object obj)
        {
            return obj is MediaDescription md
                ? md.Equals(this)
                : obj is Sdp.Lines.SessionMediaDescriptionLine sdml
                ? Equals(sdml)
                : obj is SessionDescriptionLine sdl && m_Lines.Contains(sdl);
        }

        public override string ToString()
        {
            return ToString(null);
        }

        public string ToString(SessionDescription sdp = null)
        {
            StringBuilder buffer = new();

            //Check if the mapping matches..., should not be done at this level.
            //All instance still need the sdp in ToString to check if the encoding matches?

            //if (sdp is not null)
            //{
            //    //Todo, maybe use m_Type because the line may not be typed as a ConnectionLine yet.
            //    Sdp.Lines.SessionConnectionLine connectionLine = sdp.Lines.OfType<Sdp.Lines.SessionConnectionLine>().FirstOrDefault();

            //    /*
            //    If multiple addresses are specified in the "c=" field and multiple
            //    ports are specified in the "m=" field, a one-to-one mapping from
            //    port to the corresponding address is implied.  For example:

            //      c=IN IP4 224.2.1.1/127/2
            //      m=video 49170/2 RTP/AVP 31
            //    */
            //    if (connectionLine is not null && connectionLine.HasMultipleAddresses)
            //    {
            //        int numberOfAddresses = connectionLine.NumberOfAddresses;

            //        if (numberOfAddresses > 1)
            //        {
            //            //buffer.Append(Sdp.Lines.SessionMediaDescriptionLine.MediaDescriptionType.ToString() + Sdp.SessionDescription.EqualsSign + string.Join(SessionDescription.Space.ToString(), MediaType, MediaPort.ToString() + ((char)Common.ASCII.ForwardSlash).ToString() + numberOfAddresses.ToString(), MediaProtocol, MediaFormat) + SessionDescription.NewLineString);

            //            buffer.Append(Sdp.Lines.SessionMediaDescriptionLine.MediaDescriptionType);

            //            buffer.Append(Sdp.SessionDescription.EqualsSign);

            //            buffer.Append(
            //            string.Join(SessionDescription.Space.ToString(), MediaType, MediaPort.ToString() + ((char)Common.ASCII.ForwardSlash).ToString() + numberOfAddresses.ToString(), MediaProtocol, MediaFormat)
            //            );

            //            buffer.Append(SessionDescription.NewLineString)

            //            goto LinesOnly;
            //        }
            //    }
            //}

            //Note if Unassigned MediaFormat is used that this might have to be a 'char' to be exactly what was given
            buffer.Append(MediaDescriptionLine.ToString());

            //LinesOnly:
            foreach (SessionDescriptionLine l in m_Lines.Where(l => l.m_Type is not Sdp.Lines.SessionBandwidthLine.BandwidthType and not Sdp.Lines.SessionAttributeLine.AttributeType))
                buffer.Append(l.ToString());

            foreach (SessionDescriptionLine l in m_Lines.Where(l => l.m_Type == Sdp.Lines.SessionBandwidthLine.BandwidthType))
                buffer.Append(l.ToString());

            foreach (SessionDescriptionLine l in m_Lines.Where(l => l.m_Type == Sdp.Lines.SessionAttributeLine.AttributeType))
                buffer.Append(l.ToString());

            return buffer.ToString();
        }

        #endregion

        #region Named Lines

        //Could all be extension methods.

        public IEnumerable<SessionDescriptionLine> AttributeLines
        {
            get
            {
                return m_Lines.Where(l => l.m_Type == Sdp.Lines.SessionAttributeLine.AttributeType);
            }
        }

        //Should be typed as Bandwidth Lines...
        public IEnumerable<SessionDescriptionLine> BandwidthLines
        {
            get
            {
                return m_Lines.Where(l => l.m_Type == Sdp.Lines.SessionBandwidthLine.BandwidthType);
            }
        }

        public SessionDescriptionLine ConnectionLine { get { return m_Lines.FirstOrDefault(l => l.m_Type == Sdp.Lines.SessionConnectionLine.ConnectionType); } }

        public SessionDescriptionLine RtpMapLine
        {
            get
            {
                return m_Lines.FirstOrDefault(l => l.m_Type == Sdp.Lines.SessionAttributeLine.AttributeType && l.m_Parts.Count > 0 && l.m_Parts[0].StartsWith(AttributeFields.RtpMap, StringComparison.InvariantCultureIgnoreCase));
            }
        }

        //public IEnumerable<Media.Sdp.Lines.RtpMapLine> RtpMapLines
        //{
        //    get
        //    {
        //        return m_Lines.Where(l => l.m_Type == Sdp.Lines.SessionAttributeLine.AttributeType && l.m_Parts.Count > 0 && l.m_Parts[0].StartsWith(AttributeFields.RtpMap, StringComparison.InvariantCultureIgnoreCase));
        //    }
        //}


        public SessionDescriptionLine FmtpLine
        {
            get
            {
                return m_Lines.FirstOrDefault(l => l.m_Type == Sdp.Lines.SessionAttributeLine.AttributeType && l.m_Parts.Count > 0 && l.m_Parts[0].StartsWith(AttributeFields.FormatType, StringComparison.InvariantCultureIgnoreCase));
            }
        }

        public SessionDescriptionLine RangeLine
        {
            get { return m_Lines.FirstOrDefault(l => l.m_Type == Sdp.Lines.SessionAttributeLine.AttributeType && l.m_Parts.Count > 0 && l.m_Parts[0].StartsWith(AttributeFields.Range, StringComparison.InvariantCultureIgnoreCase)); }
        }

        public SessionDescriptionLine ControlLine
        {
            get
            {
                return m_Lines.FirstOrDefault(l => l.m_Type == Sdp.Lines.SessionAttributeLine.AttributeType && l.m_Parts.Count > 0 && l.m_Parts[0].StartsWith(AttributeFields.Control, StringComparison.InvariantCultureIgnoreCase));
            }
        }

        public SessionDescriptionLine SsrcLine
        {
            get
            {
                return m_Lines.FirstOrDefault(l => l.m_Type == Sdp.Lines.SessionAttributeLine.AttributeType && l.m_Parts.Count > 0 && l.m_Parts[0].StartsWith(AttributeFields.SynchronizationSourceIdentifier, StringComparison.InvariantCultureIgnoreCase));
            }
        }

        public SessionDescriptionLine RtcpLine
        {
            get
            {
                return m_Lines.FirstOrDefault(l => l.m_Type == Sdp.Lines.SessionAttributeLine.AttributeType && l.m_Parts.Count > 0 && l.m_Parts[0].StartsWith(AttributeFields.Rtcp, StringComparison.InvariantCultureIgnoreCase));
            }
        }

        #endregion

        #region Lines

        public IEnumerator<SessionDescriptionLine> GetEnumerator()
        {
            yield return MediaDescriptionLine;

            foreach (var line in m_Lines)
            {
                if (line is null) continue;

                yield return line;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<SessionDescriptionLine>)this).GetEnumerator();
        }

        #endregion
    }

    public static class MediaDescriptionExtensions
    {
        /// <summary>
        /// Gets absolute control uri from <see cref="MediaDescription.ControlLine"/>.
        /// </summary>
        /// <param name="mediaDescription"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        public static Uri GetAbsoluteControlUri(this MediaDescription mediaDescription, Uri source, SessionDescription sessionDescription = null)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (source.IsAbsoluteUri is false) throw new InvalidOperationException("source.IsAbsoluteUri must be true.");

            if (Common.IDisposedExtensions.IsNullOrDisposed(mediaDescription)) return source;

            if (source.IsAbsoluteUri is false) throw new InvalidOperationException("source.IsAbsoluteUri must be true.");

            SessionDescriptionLine controlLine = mediaDescription.ControlLine;

            //If there is a control line in the SDP it contains the URI used to setup and control the media
            if (controlLine is not null)
            {
                //Todo, make typed line for controlLine
                string controlPart = controlLine.Parts.LastOrDefault(); //controlLine.Parts.Where(p => p.StartsWith(AttributeFields.Control)).FirstOrDefault();

                //If there is a controlPart in the controlLine
                if (string.IsNullOrWhiteSpace(controlPart) is false)
                {
                    //Prepare the part
                    controlPart = controlPart.Split(Media.Sdp.SessionDescription.ColonSplit, 2, StringSplitOptions.RemoveEmptyEntries).Last();

                    //Create a uri
                    Uri controlUri = new(controlPart, UriKind.RelativeOrAbsolute);

                    //Determine if its a Absolute Uri
                    if (controlUri.IsAbsoluteUri) return controlUri;

                    //Return a new uri using the original string and the controlUri relative path.
                    //Hopefully the direction of the braces matched..

                    string raw = source.OriginalString.EndsWith(SessionDescription.ForwardSlashString)
                        ? source.OriginalString + controlUri.OriginalString
                        : string.Join(SessionDescription.ForwardSlashString, source.OriginalString, controlUri.OriginalString);
                    return new Uri(raw);

                    //Todo, ensure that any parameters have also been restored...

                    #region Explanation

                    //I wonder if Mr./(Dr) Fielding is happy...
                    //Let source = 
                    //rtsp://alt1.v7.cache3.c.youtube.com/CigLENy73wIaHwmddh2T-s8niRMYDSANFEgGUgx1c2VyX3VwbG9hZHMM/0/0/0/1/video.3gp/trackID=0
                    //Call
                    //return new Uri(source, controlUri);
                    //Result = 
                    //rtsp://alt1.v7.cache3.c.youtube.com/CigLENy73wIaHwmddh2T-s8niRMYDSANFEgGUgx1c2VyX3VwbG9hZHMM/0/0/0/1/trackID=0


                    //Useless when the source doesn't end with '/', e.g. same problem with Uri constructor.

                    //System.UriBuilder builder = new UriBuilder(source);
                    //builder.Path += controlUri.ToString();

                    //"rtsp://wowzaec2demo.streamlock.net/vod/mp4:BigBuckBunny_115k.mov/trackID=1"

                    #endregion
                }
            }

            //Try to take the session level control uri
            //If there was a session description given and it supports aggregate media control then return that uri
            if (Common.IDisposedExtensions.IsNullOrDisposed(sessionDescription) is false &&
                sessionDescription.SupportsAggregateMediaControl(out Uri sessionControlUri, source))
                return sessionControlUri;

            //There is no control line, just return the source.
            return source;
        }

        public static TimeDescription GetTimeDescription(this MediaDescription mediaDescription, SessionDescription sessionDescription)
        {
            if (Common.IDisposedExtensions.IsNullOrDisposed(mediaDescription) || Common.IDisposedExtensions.IsNullOrDisposed(sessionDescription)) return null;

            //Get index of mediaDescription

            //Needs a better way to get the index of the media description
            int index = sessionDescription.GetIndexFor(mediaDescription);  //Array.IndexOf(sessionDescription.MediaDescriptions.ToArray(), mediaDescription);

            return index == -1 || index >= sessionDescription.TimeDescriptionsCount ? null : sessionDescription.GetTimeDescription(index);
        }

        //Should have a date when or should return the date playable, which would then be used by another method to compare against a time.
        public static bool IsPlayable(this MediaDescription mediaDescription, SessionDescription sessionDescription) //, DateTime? check = null) ,TimeSpan within = TimeSpan.Zero
        {
            if (Common.IDisposedExtensions.IsNullOrDisposed(mediaDescription) || Common.IDisposedExtensions.IsNullOrDisposed(sessionDescription)) return false;

            //Get index of mediaDesription

            //Check TimeDescription @ index.

            TimeDescription td = GetTimeDescription(mediaDescription, sessionDescription);

            //Assume true
            if (Common.IDisposedExtensions.IsNullOrDisposed(td)) return true;

            //Unbound start and end ?
            if (td.IsPermanent) return true;

            //Notes multiple calls to UtcNow... (avoid with a within parameter)?
            try
            {
                //Ensure not a bounded end and that the end time is less than now
                if (false.Equals(td.StopTime is 0)
                    &&
                    td.NtpStopDateTime >= DateTime.UtcNow) return false;

                //Ensure start time is not bounded and that the start time is greater than now
                if (false.Equals(td.StartTime is 0)
                    &&
                    td.NtpStartDateTime > DateTime.UtcNow) return false;

                //Check repeat times.

                //td.RepeatTimes;
            }
            //Todo, should not access property again during exception especially when out of range is potential.
            catch
            {
                //Out of range values for conversion, assume true if end is unbounded
                if (false.Equals(td.StopTime is 0)) return false;
            }
            finally
            {
                td = null;
            }

            return true;
        }
    }

    #endregion
}
