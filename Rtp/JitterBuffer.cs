﻿namespace Media.Rtp
{
    #region JitterBuffer

    //Useful for holding onto frame for longer than one cycle.
    //Could be used from the application during the FrameChangedEvent when 'final' is set to true.
    //E.g. when final, =>
    //Common.BaseDisposable.SetShouldDispose(frame, false, false);
    //JitterBuffer.Add(frame);
    //Could also be used by the RtpPacketRecieved event when not using FrameChangedEvents.

    /// <summary>
    /// RtpPacket and RtpFrame storage.
    /// </summary>
    public class JitterBuffer : Common.BaseDisposable
    {
        #region Nested Types

        internal class RtpPacketEqualityComparer : System.Collections.Generic.EqualityComparer<RtpPacket>
        {
            public override bool Equals(RtpPacket x, RtpPacket y)
            {
                return x.Equals(y);
            }

            public override int GetHashCode(RtpPacket obj)
            {
                return obj.GetHashCode();
            }
        }

        internal class RtpFrameEqualityComparer : System.Collections.Generic.EqualityComparer<RtpFrame>
        {
            public override bool Equals(RtpFrame x, RtpFrame y)
            {
                return x.Equals(y);
            }

            public override int GetHashCode(RtpFrame obj)
            {
                return obj.GetHashCode();
            }
        }

        #endregion

        //PayloadType, Frames for PayloadType
        private readonly Common.Collections.Generic.ConcurrentThesaurus<int, RtpFrame> Frames = [];
        private readonly System.Collections.Generic.Dictionary<int, Sdp.MediaDescription> MediaDescriptions = [];

        //Todo
        //Properties to track for max, Memory, Packets, Time etc.

        //MediaDescription for each payloadType which is known about...

        //readonly System.Collections.Generic.Dictionary<int, Sdp.MediaDescription> MediaDescriptionDictionary = new System.Collections.Generic.Dictionary<int, Sdp.MediaDescription>();

        #region Properties        

        #endregion

        #region Constructor

        public JitterBuffer(bool shouldDispose) : base(shouldDispose) { }

        #endregion


        #region Methods

        public Sdp.SessionDescription CreateSessionDescription(int version = 0)
        {
            Sdp.SessionDescription sdp = new(version);
            foreach (Sdp.MediaDescription md in MediaDescriptions.Values) sdp.Add(new Sdp.MediaDescription(md));
            return sdp;
        }

        public System.TimeSpan GetDuration(int payloadType)
        {
            if (TryGetFrames(payloadType, out System.Collections.Generic.IEnumerable<RtpFrame> frames))
            {
                if (MediaDescriptions.TryGetValue(payloadType, out Sdp.MediaDescription mediaDescription))
                {
                    Sdp.Lines.RtpMapLine rtpMap = new(mediaDescription.RtpMapLine);
                    return System.TimeSpan.FromMilliseconds(System.Linq.Enumerable.Last(frames).Timestamp - System.Linq.Enumerable.First(frames).Timestamp * rtpMap.ClockRate);
                }
            }
            //return Common.Extensions.TimeSpan.TimeSpanExtensions.InfiniteTimeSpan;
            return System.TimeSpan.Zero;
        }

        /// <summary>
        /// Adds the given frame using the PayloadType specified by the frame.
        /// </summary>
        /// <param name="frame"></param>
        public void Add(RtpFrame frame) { Add(frame.PayloadType, frame); }

        /// <summary>
        /// Adds a frame using the specified payloadType.
        /// </summary>
        /// <param name="payloadType"></param>
        /// <param name="frame"></param>
        public void Add(int payloadType, RtpFrame frame)
        {
            if (Common.IDisposedExtensions.IsNullOrDisposed(frame)) return;

            Frames.Add(payloadType, frame);
        }

        /// <summary>
        /// Adds a packet using the PayloadType specified in the packet.
        /// </summary>
        /// <param name="packet">The packet</param>
        public void Add(RtpPacket packet) { Add(packet.PayloadType, packet); }

        /// <summary>
        /// Adds a packet with the specified payloadType
        /// </summary>
        /// <param name="payloadType"></param>
        /// <param name="packet"></param>
        public void Add(int payloadType, RtpPacket packet, bool allowDuplicatePackets = false, bool allowPacketsAfterMarker = false)
        {

            Add(payloadType, allowDuplicatePackets, allowPacketsAfterMarker, packet, out RtpFrame addedTo);
        }

        /// <summary>
        /// Adds the given packet to the contained frames and provides the frame which was added to.
        /// </summary>
        /// <param name="payloadType">The payloadType to use for the add operation</param>
        /// <param name="packet">The packet to add</param>
        /// <param name="addedTo">The frame which the packet was added to.</param>
        /// <returns>True if <paramref name="addedTo"/> is complete (it is no longer contained), otherwise false.</returns>
        public bool Add(int payloadType, bool allowDuplicatePackets, bool allowPacketsAfterMarker, RtpPacket packet, out RtpFrame addedTo)
        {
            addedTo = null;

            if (Common.IDisposedExtensions.IsNullOrDisposed(packet)) return false;


            //Use the given payloadType to get frames
            if (Frames.TryGetValueList(ref payloadType, out System.Collections.Generic.IList<RtpFrame> framesList))
            {
                //loop the frames found
                foreach (RtpFrame frame in framesList)
                {
                    //if the timestamp is eqaul try to add the packet
                    if (frame.Timestamp == packet.Timestamp)
                    {
                        //Try to add the packet and if added return.
                        if (frame.TryAdd(packet, allowDuplicatePackets, allowPacketsAfterMarker))
                        {
                            addedTo = frame;

                            //If the add results in completion
                            if (frame.IsComplete)
                            {
                                //Remove the frame
                                framesList.Remove(frame);

                                //Return true
                                return true;
                            }
                        }
                    }
                }

                //Must add a new frame to frames.
                addedTo = new RtpFrame(packet);

                if (addedTo.IsComplete) return true;

                Frames.Add(ref payloadType, ref addedTo);
            }

            return false;
        }

        /// <summary>
        /// Attempts to retrieve all <see cref="RtpFrame"/> instances related to the given payloadType
        /// </summary>
        /// <param name="payloadType"></param>
        /// <param name="frames"></param>
        /// <returns>True if <paramref name="payloadType"/> was contained, otherwise false.</returns>
        public bool TryGetFrames(int payloadType, out System.Collections.Generic.IEnumerable<RtpFrame> frames) { return Frames.TryGetValue(payloadType, out frames); }

        //Remove with timestamp start and end

        /// <summary>
        /// Clears all contained frames and optionally disposes all contained frames when removed.
        /// </summary>
        /// <param name="disposeFrames"></param>
        public void Clear(bool disposeFrames = true)
        {
            int[] keys = System.Linq.Enumerable.ToArray(Frames.Keys);

            int Key;

            //Store the frames at the key

            //Could perform in parallel, would need frames local.
            //System.Linq.ParallelEnumerable.ForAll(keys, () => { });

            //Enumerate an array of contained keys
            for (int i = 0, e = keys.Length; i < e; ++i)
            {
                //Get the key
                Key = keys[i];

                //if removed from the ConcurrentThesaurus
                if (Frames.Remove(ref Key, out System.Collections.Generic.IEnumerable<RtpFrame> frames))
                {
                    //if we need to dispose the frames then Loop the frames contined at the key
                    if (disposeFrames) foreach (RtpFrame frame in frames)
                        {
                            //Set ShouldDispose through the base class.
                            Common.BaseDisposable.SetShouldDispose(frame, true, true);

                            //Dispose the frame (already done with above call)
                            frame.Dispose();
                        }
                }
            }
        }

        #endregion

        public override void Dispose()
        {
            base.Dispose();

            if (ShouldDispose)
            {
                Clear();
            }
        }
    }

    #endregion
}
