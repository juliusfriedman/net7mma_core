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

using System;
using System.Collections.Generic;
using System.Linq;

namespace Media.Rtsp.Server.MediaTypes
{

    /// <summary>
    /// Provides an implementation of <see href="https://tools.ietf.org/html/rfc6184">RFC6184</see> which is used for H.264 Encoded video.
    /// </summary>
    public class RFC6184Media : RFC2435Media //Todo use RtpVideoSink not RFC2435Media
    {
        //Some MP4 Related stuff
        //https://github.com/fyhertz/libstreaming/blob/master/src/net/majorkernelpanic/streaming/mp4/MP4Parser.java

        //C# h264 elementary stream stuff
        //https://bitbucket.org/jerky/rtp-streaming-server

        //C# MP4 and H.264 ES Writer
        //https://github.com/TalAloni/MP4Maker

        /// <summary>
        /// Implements Packetization and Depacketization of packets defined in <see href="https://tools.ietf.org/html/rfc6184">RFC6184</see>.
        /// </summary>
        public class RFC6184Frame : Rtp.RtpFrame
        {
            #region Static

            public static byte[] FullStartSequence = new byte[] { 0x00, 0x00, 0x00, 0x01 };
            private static readonly Common.MemorySegment FullStartSequenceSegment = new(FullStartSequence, 0, 4, false);
            private static readonly Common.MemorySegment ShortStartSequenceSegment = new(FullStartSequence, 1, 3, false);

            public static byte[] CreateSingleTimeAggregationUnit(int? DON = null, params byte[][] nals)
            {

                if (nals is null || nals.Count() is 0) throw new InvalidOperationException("Must have at least one nal");

                //Get the data required which consists of the Length and the nal.
                IEnumerable<byte> data = nals.SelectMany(n => Common.Binary.GetBytes((short)n.Length, Common.Binary.IsLittleEndian).Concat(n));

                //STAP - B has DON at the very beginning
                data = DON.HasValue
                    ? Media.Common.Extensions.Linq.LinqExtensions.Yield(Media.Codecs.Video.H264.NalUnitType.SingleTimeAggregationB).Concat(Common.Binary.GetBytes((short)DON, Common.Binary.IsLittleEndian)).Concat(data)
                    : Media.Common.Extensions.Linq.LinqExtensions.Yield(Media.Codecs.Video.H264.NalUnitType.SingleTimeAggregationA).Concat(data);

                return data.ToArray();
            }

            public static byte[] CreateMultiTimeAggregationUnit(int DON, byte dond, int tsOffset, params byte[][] nals)
            {

                if (nals is null || nals.Count() is 0) throw new InvalidOperationException("Must have at least one nal");

                //Get the data required which consists of the Length and the nal.
                IEnumerable<byte> data = nals.SelectMany(n =>
                {
                    byte[] lengthBytes = new byte[2];
                    Common.Binary.Write16(lengthBytes, 0, Common.Binary.IsLittleEndian, (short)n.Length);

                    //GetBytes

                    //DOND
                    //TS OFFSET

                    byte[] tsOffsetBytes = new byte[3];

                    Common.Binary.Write24(tsOffsetBytes, 0, Common.Binary.IsLittleEndian, tsOffset);

                    return Media.Common.Extensions.Linq.LinqExtensions.Yield(dond).Concat(lengthBytes).Concat(n);
                });

                //MTAP has DON at the very beginning
                data = Media.Common.Extensions.Linq.LinqExtensions.Yield(Media.Codecs.Video.H264.NalUnitType.MultiTimeAggregation16).Concat(Media.Common.Binary.GetBytes((short)DON, Common.Binary.IsLittleEndian)).Concat(data);

                return data.ToArray();
            }

            public static byte[] CreateMultiTimeAggregationUnit(int DON, byte dond, short tsOffset, params byte[][] nals)
            {

                if (nals is null || nals.Count() is 0) throw new InvalidOperationException("Must have at least one nal");

                //Get the data required which consists of the Length and the nal.
                IEnumerable<byte> data = nals.SelectMany(n =>
                {
                    byte[] lengthBytes = new byte[2];
                    Common.Binary.Write16(lengthBytes, 0, Common.Binary.IsLittleEndian, (short)n.Length);

                    //Common.Binary.GetBytes((short)n.Length, Common.Binary.IsLittleEndian);

                    //DOND

                    //TS OFFSET

                    byte[] tsOffsetBytes = new byte[2];

                    Common.Binary.Write16(tsOffsetBytes, 0, Common.Binary.IsLittleEndian, tsOffset);

                    return Media.Common.Extensions.Linq.LinqExtensions.Yield(dond).Concat(tsOffsetBytes).Concat(lengthBytes).Concat(n);
                });

                //MTAP has DON at the very beginning
                data = Media.Common.Extensions.Linq.LinqExtensions.Yield(Media.Codecs.Video.H264.NalUnitType.MultiTimeAggregation24).Concat(Media.Common.Binary.GetBytes((short)DON, Common.Binary.IsLittleEndian)).Concat(data);

                return data.ToArray();
            }

            #endregion

            #region Constructor

            public RFC6184Frame(byte payloadType)
                : base(payloadType)
            {
                m_ContainedNalTypes = [];
            }

            public RFC6184Frame(Rtp.RtpFrame existing, bool referencePackets = false, bool referenceBuffer = false, bool shouldDispose = true)
                : base(existing, referencePackets, referenceBuffer, shouldDispose)
            {
                m_ContainedNalTypes = [];
            }

            public RFC6184Frame(RFC6184Frame existing, bool referencePackets = false, bool referenceBuffer = false, bool shouldDispose = true)
                : base(existing, referencePackets, referenceBuffer, shouldDispose)
            {
                if (referenceBuffer)
                {
                    m_ContainedNalTypes = existing.m_ContainedNalTypes;
                }
            }

            //AllowMultipleMarkerPackets

            #endregion

            #region Fields

            //May be kept in a state or InformationClass eventually, would allow for other options to be kept also.

            //Should use HashSet? (would not allow counting of types but isn't really needed)
            protected internal readonly List<byte> m_ContainedNalTypes;

            #endregion

            #region Properties

            /// <summary>
            /// Indicates if a NalUnit which corresponds to a SupplementalEncoderInformation is contained.
            /// </summary>
            public bool ContainsSupplementalEncoderInformation
            {
                get
                {
                    return m_ContainedNalTypes.Any(t => t == Media.Codecs.Video.H264.NalUnitType.SupplementalEncoderInformation);
                }
            }

            /// <summary>
            /// Indicates if a NalUnit which corresponds to a SequenceParameterSet is contained.
            /// </summary>
            public bool ContainsSequenceParameterSet
            {
                get
                {
                    return m_ContainedNalTypes.Any(t => t == Media.Codecs.Video.H264.NalUnitType.SequenceParameterSet);
                }
            }

            /// <summary>
            /// Indicates if a NalUnit which corresponds to a PictureParameterSet is contained.
            /// </summary>
            public bool ContainsPictureParameterSet
            {
                get
                {
                    return m_ContainedNalTypes.Any(t => t == Media.Codecs.Video.H264.NalUnitType.PictureParameterSet);
                }
            }

            //bool ContainsInitializationSet return m_ContainedNalTypes.Any(t => t == Media.Codecs.Video.H264.NalUnitType.PictureParameterSet || t == Media.Codecs.Video.H264.NalUnitType.SequenceParameterSet);

            /// <summary>
            /// Indicates if a NalUnit which corresponds to a InstantaneousDecoderRefresh is contained.
            /// </summary>
            public bool ContainsInstantaneousDecoderRefresh
            {
                get
                {
                    return m_ContainedNalTypes.Any(t => t == Media.Codecs.Video.H264.NalUnitType.InstantaneousDecoderRefresh);
                }
            }

            /// <summary>
            /// Indicates if a NalUnit which corresponds to a CodedSlice is contained.
            /// </summary>
            public bool ContainsCodedSlice
            {
                get
                {
                    return m_ContainedNalTypes.Any(t => t == Media.Codecs.Video.H264.NalUnitType.CodedSlice);
                }
            }

            //This is not necessarily in the sorted order of the packets if packets were added out of order.

            /// <summary>
            /// After Packetization or Depacketization, will indicate the types of Nal units contained in the data of the frame.
            /// </summary>
            public IEnumerable<byte> ContainedUnitTypes
            {
                get
                {
                    return m_ContainedNalTypes;
                }
            }

            #endregion

            //Should be overriden
            /// <summary>
            /// Creates any <see cref="Rtp.RtpPacket"/>'s required for the given nal by copying the data to RtpPacket instances.
            /// </summary>
            /// <param name="nal">The nal</param>
            /// <param name="mtu">The mtu</param>
            /// <param name="DON">The Decoder Ordering Number (timestamp)</param>
            public virtual void Packetize(byte[] nal, int mtu = 1500, int? DON = null) //sequenceNumber
            {
                if (nal is null) return;

                int nalLength = nal.Length;

                int offset = 0;

                if (nalLength >= mtu)
                {
                    //Consume the original header and move the offset into the data
                    byte nalHeader = nal[offset++],
                        nalFNRI = (byte)(nalHeader & 0xE0), //Extract the F and NRI bit fields
                        nalType = (byte)(nalHeader & Common.Binary.FiveBitMaxValue), //Extract the Type
                        fragmentType = DON.HasValue ? Media.Codecs.Video.H264.NalUnitType.FragmentationUnitB : Media.Codecs.Video.H264.NalUnitType.FragmentationUnitA,
                        fragmentIndicator = (byte)(nalFNRI | fragmentType);//Create the Fragment Indicator Octet

                    //Store the nalType contained
                    m_ContainedNalTypes.Add(nalType);

                    //Determine if the marker bit should be set.
                    bool marker = Media.Codecs.Video.H264.NalUnitType.IsAccessUnit(ref nalType);//false; //(nalType == Media.Codecs.Video.H264.NalUnitType.AccessUnitDelimiter);

                    //Get the highest sequence number
                    int highestSequenceNumber = HighestSequenceNumber;

                    //Consume the bytes left in the nal
                    while (offset < nalLength)
                    {
                        //Get the data required which consists of the fragmentIndicator, Constructed Header and the data.
                        IEnumerable<byte> data;

                        //Build the Fragmentation Header

                        //First Packet
                        if (offset == 1)
                        {
                            //FU (A/B) Indicator with F and NRI
                            //Start Bit Set with Original NalType

                            data = Enumerable.Concat(Media.Common.Extensions.Linq.LinqExtensions.Yield(fragmentIndicator), Media.Common.Extensions.Linq.LinqExtensions.Yield(((byte)(0x80 | nalType))));
                        }
                        else if (offset + mtu > nalLength)
                        {
                            //End Bit Set with Original NalType
                            data = Enumerable.Concat(Media.Common.Extensions.Linq.LinqExtensions.Yield(fragmentIndicator), Media.Common.Extensions.Linq.LinqExtensions.Yield(((byte)(0x40 | nalType))));

                            //This should not be set at the nal level for end of nal units.
                            //marker = true;

                        }
                        else//For packets other than the start or end
                        {
                            //No Start, No End
                            data = Enumerable.Concat(Media.Common.Extensions.Linq.LinqExtensions.Yield(fragmentIndicator), Media.Common.Extensions.Linq.LinqExtensions.Yield(nalType));
                        }

                        //FU - B has DON at the very beginning of each 
                        if (fragmentType == Media.Codecs.Video.H264.NalUnitType.FragmentationUnitB)// && Count is 0)// highestSequenceNumber is 0)
                        {
                            //byte[] DONBytes = new byte[2];
                            //Common.Binary.Write16(DONBytes, 0, Common.Binary.IsLittleEndian, (short)DON);

                            data = Enumerable.Concat(Common.Binary.GetBytes((short)DON, Common.Binary.IsLittleEndian), data);
                        }

                        //Add the data the fragment data from the original nal
                        data = Enumerable.Concat(data, nal.Skip(offset).Take(mtu));

                        //Add the packet using the next highest sequence number
                        Add(new Rtp.RtpPacket(2, false, false, marker, PayloadType, 0, SynchronizationSourceIdentifier, ++highestSequenceNumber, 0, data.ToArray()));

                        //Move the offset
                        offset += mtu;
                    }
                } //Should check for first byte to be 1 - 23?
                else Add(new Rtp.RtpPacket(2, false, false, false, PayloadType, 0, SynchronizationSourceIdentifier, HighestSequenceNumber + 1, 0, nal));
            }

            //Needs to ensure api is not confused with above. could also possibly handle in Packetize by searching for 0 0 1
            //public virtual void Packetize(byte[] accessUnit, int mtu = 1500, int? DON = null)
            //{
            //    throw new NotImplementedException();
            //    //Add all data and set marker packet on last packet.
            //    //Add AUD to next packet or the end of this one?
            //}

            //Not needed since ProcessPacket can do this.
            //public void Depacketize(bool ignoreForbiddenZeroBit = true, bool fullStartCodes = false)
            //{
            //    //base.Depacketize();

            //    DisposeBuffer();

            //    m_Buffer = new MemoryStream();

            //    var packets = Packets;

            //    //Todo, check if need to 
            //    //Order by DON / TSOFFSET (if any packets contains MTAP etc)

            //    //Get all packets in the frame and proces them
            //    foreach (Rtp.RtpPacket packet in packets)
            //        ProcessPacket(packet, ignoreForbiddenZeroBit, fullStartCodes);

            //    //Bring the buffer back the start. (This does not have a weird side effect of adding 0xa to the stream)
            //    m_Buffer.Seek(0, SeekOrigin.Begin);

            //    //This has a weird side effect of adding 0xa to the stream
            //    //m_Buffer.Position = 0;
            //}

            /// <summary>
            /// Depacketizes all contained packets and adds start sequences where necessary which can be though of as a H.264 RBSP 
            /// </summary>
            /// <param name="packet"></param>
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public override void Depacketize(Rtp.RtpPacket packet) { ProcessPacket(packet, false, false); }

            //Could be called Depacketize
            //Virtual because the RFC6190 logic defers to this method for non SVC nal types.
            /// <summary>
            /// Depacketizes a single packet.
            /// </summary>
            /// <param name="packet"></param>
            /// <param name="containsSps"></param>
            /// <param name="containsPps"></param>
            /// <param name="containsSei"></param>
            /// <param name="containsSlice"></param>
            /// <param name="isIdr"></param>
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            protected internal virtual void ProcessPacket(Rtp.RtpPacket packet, bool ignoreForbiddenZeroBit = true, bool fullStartCodes = false)
            {
                //If the packet is null or disposed then do not process it.
                if (Common.IDisposedExtensions.IsNullOrDisposed(packet)) return;

                //(May need to handle re-ordering)
                //In such cases this step needs to place the packets into a seperate collection for sorting on DON / TSOFFSET before writing to the buffer.

                ///From the beginning of the data in the actual payload
                int headerOctets = packet.HeaderOctets,
                   offset = packet.Payload.Offset + headerOctets,
                   padding = packet.PaddingOctets,
                   count = packet.Payload.Count - (padding + headerOctets);

                //int offset = 0;

                //Obtain the data of the packet (without source list or padding)
                //byte[] packetData = packet.PayloadData.ToArray();

                //Cache the length
                //int count = packetData.Length;                

                //Just put the packets into Depacketized at the end for most cases.
                int packetKey = Depacketized.Count; //packet.SequenceNumber;               

                ///Obtain the data of the packet with respect to extensions and csrcs present.
                byte[] packetData = packet.Payload.Array;

                if (packet.Extension)
                {
                    //This is probably a new sps pps set                    

                    //Determine the amount of extension octets including the flags and length in words...
                    int extensionOctets = packet.ExtensionOctets;

                    //Position at the start of the extension? (I would think this would be after the flags...)
                    //It appears that the ExtensionFlags indicates the nal type?
                    offset -= extensionOctets + Rtp.RtpExtension.MinimumSize;

                    //Add the extension data to the count..
                    count += extensionOctets + Rtp.RtpExtension.MinimumSize;
                }

                if (false.Equals(packet.PayloadType.Equals(PayloadType)))
                {
                    if (AllowsMultiplePayloadTypes is false) return;
                }

                ///Must have at least 2 bytes (When nalUnitType is a FragmentUnit.. 3)
                ///If such cases as AUD or otherwise there is only 1 byte of data and it's usually the stop bit.
                if (count <= 2) return;

                //Determine if the forbidden bit is set and the type of nal from the first byte
                byte firstByte = packetData[offset];

                //Should never be set... (unless decoding errors are present)
                //e,g, a FU was missed.
                /*
                 A receiver in an endpoint or in a MANE MAY aggregate the first n-1
                fragments of a NAL unit to an (incomplete) NAL unit, even if fragment
                n of that NAL unit is not received.  In this case, the
                forbidden_zero_bit of the NAL unit MUST be set to one to indicate a
                syntax violation.
                */
                if (ignoreForbiddenZeroBit is false && false.Equals(0.Equals(((firstByte & 0x80) >> 7))))
                {
                    //would need additional state to ensure all packets now have this bit.

                    return; //throw new Exception("forbiddenZeroBit");
                }

                ////The packets are not stored by SequenceNumber in Depacketized, they are stored in whatever Decoder Order is necessary.
                //if (Depacketized.ContainsKey(packetKey))
                //{
                //    //Todo, this should be ordered correctly using the PaD
                //    //packetKey = Depacketized.Count + packetKey;

                //    double newKey = (packetKey - 1) + 0.101;

                //    while (Depacketized.ContainsKey(newKey))
                //    {
                //        newKey -= 0.001;
                //    }

                //    System.Console.WriteLine("KeyChnge : " + packetKey + "," + newKey);

                //    //--packetKey;

                //    //packetKey += 0.1;

                //    //packetKey = newKey;
                //}
                //else packetKey += Depacketized.Count;

                byte nalUnitType = (byte)(firstByte & Common.Binary.FiveBitMaxValue);

                //RFC6184 @ Page 20
                //o  The F bit MUST be cleared if all F bits of the aggregated NAL units are zero; otherwise, it MUST be set.
                //if (forbiddenZeroBit && nalUnitType <= 23 && nalUnitType > 29) throw new InvalidOperationException("Forbidden Zero Bit is Set.");

                //Needs other state to check if previously F was set or not

                //Media.Codecs.Video.H264.NalUnitPriority priority = (Media.Codecs.Video.H264.NalUnitPriority)((firstByte & 0x60) >> 5);

                //Determine what to do
                switch (nalUnitType)
                {
                    //Reserved - Ignore
                    case Media.Codecs.Video.H264.NalUnitType.Unknown:
                    case Media.Codecs.Video.H264.NalUnitType.PayloadContentScalabilityInformation:
                    case Media.Codecs.Video.H264.NalUnitType.Reserved:
                        {
                            //May have 4 byte NAL header.
                            //Do not handle
                            return;
                        }
                    case Media.Codecs.Video.H264.NalUnitType.SingleTimeAggregationA: //STAP - A
                    case Media.Codecs.Video.H264.NalUnitType.SingleTimeAggregationB: //STAP - B
                    case Media.Codecs.Video.H264.NalUnitType.MultiTimeAggregation16: //MTAP - 16
                    case Media.Codecs.Video.H264.NalUnitType.MultiTimeAggregation24: //MTAP - 24
                        {
                            //Move to Nal Data
                            ++offset;

                            --count;

                            //Todo Determine if need to Order by DON first.
                            //EAT DON for ALL BUT STAP - A
                            if (nalUnitType != Media.Codecs.Video.H264.NalUnitType.SingleTimeAggregationA && count >= 2)
                            {
                                //Should check for 2 bytes.

                                //Read the DecoderOrderingNumber and add the value from the index.
                                /*packetKey += 0.101 * */
                                Common.Binary.ReadU16(packetData, ref offset, Common.Binary.IsLittleEndian);

                                //If the number was already observed skip this packet.
                                //if (Depacketized.ContainsKey(packetKey)) return;

                                count -= 2;

                            }

                            //Should check for 2 bytes.

                            int tmp_nal_size = 0;

                            //Consume the rest of the data from the packet
                            while (count >= 2)
                            {
                                //Determine the nal unit size which does not include the nal header
                                tmp_nal_size = Common.Binary.Read16(packetData, ref offset, Common.Binary.IsLittleEndian);

                                count -= 2;

                                //Should check for tmp_nal_size > 0
                                //If the nal had data and that data is in this packet then write it
                                if (tmp_nal_size > 0)
                                {
                                    //For DOND and TSOFFSET
                                    switch (nalUnitType)
                                    {
                                        case Media.Codecs.Video.H264.NalUnitType.MultiTimeAggregation16:// MTAP - 16 (May require re-ordering)
                                            {

                                                //Should check for 3 bytes.

                                                //DOND 1 byte

                                                //Read DOND and TSOFFSET, combine the values with the existing index
                                                if (count < 3) return;

                                                Common.Binary.ReadU24(packetData, ref offset, Common.Binary.IsLittleEndian);

                                                //If the number was already observed skip this packet.
                                                //if (Depacketized.ContainsKey(packetKey)) return;

                                                count -= 3;

                                                tmp_nal_size -= 3;

                                                goto default;
                                            }
                                        case Media.Codecs.Video.H264.NalUnitType.MultiTimeAggregation24:// MTAP - 24 (May require re-ordering)
                                            {
                                                //Should check for 4 bytes.

                                                //DOND 2 bytes

                                                //Read DOND and TSOFFSET , combine the values with the existing index
                                                /*packetKey += 0.101 * */
                                                if (count < 4) return;

                                                Common.Binary.ReadU32(packetData, ref offset, Common.Binary.IsLittleEndian);

                                                //If the number was already observed skip this packet.
                                                //if (Depacketized.ContainsKey(packetKey)) return;

                                                count -= 4;

                                                tmp_nal_size -= 4;


                                                goto default;
                                            }
                                        default:
                                            {
                                                //Ensure the entire nal is written and that it is within the buffer...
                                                tmp_nal_size = Common.Binary.Min(ref tmp_nal_size, ref count);

                                                //Could check for extra bytes or emulation prevention
                                                //https://github.com/raspberrypi/userland/blob/master/containers/rtp/rtp_h264.c

                                                //(Stores the nalType) Write the start code
                                                DepacketizeStartCode(ref packetKey, ref packetData[offset], fullStartCodes);

                                                //Ensure the size is within the count.
                                                //When tmp_nal_size is 0 packetData which is referenced by this segment which will have a 0 count.
                                                //If there was at least 1 byte then write it out.
                                                //Add the depacketized data
                                                Depacketized.Add(packetKey++, new Common.MemorySegment(packetData, offset, tmp_nal_size));

                                                //Move the offset past the nal
                                                offset += tmp_nal_size;

                                                count -= tmp_nal_size;

                                                continue;
                                            }
                                    }
                                }
                            }

                            //No more data in packet.
                            return;
                        }
                    case Media.Codecs.Video.H264.NalUnitType.FragmentationUnitA: //FU - A
                    case Media.Codecs.Video.H264.NalUnitType.FragmentationUnitB: //FU - B (May require re-ordering)
                        {
                            /*
                             Informative note: When an FU-A occurs in interleaved mode, it
                             always follows an FU-B, which sets its DON.
                             * Informative note: If a transmitter wants to encapsulate a single
                              NAL unit per packet and transmit packets out of their decoding
                              order, STAP-B packet type can be used.
                             */
                            //Needs atleast 2 bytes to reconstruct... 
                            //3 bytes for any valid data to follow after the header.
                            if (count >= 2)
                            {
                                //Offset still at the firstByte (FU Indicator) move to and read FU Header
                                byte FUHeader = packetData[++offset];

                                bool Start = ((FUHeader & 0x80) >> 7) > 0;

                                //https://tools.ietf.org/html/rfc6184 page 31...

                                //A fragmented NAL unit MUST NOT be transmitted in one FU; that is, the
                                //Start bit and End bit MUST NOT both be set to one in the same FU
                                //header.

                                //bool End = ((FUHeader & 0x40) >> 6) > 0;

                                //ignoreReservedBit

                                //bool Reserved = (FUHeader & 0x20) != 0;

                                //Should not be set 
                                //if (Reserved) throw new InvalidOperationException("Reserved Bit Set");

                                //Move to data (Just read the FU Header)
                                ++offset;

                                //Adjust count
                                count -= 2;

                                //packet.SequenceNumber - packet.Timestamp;

                                //Store the DecoderingOrderNumber we will derive from the timestamp and sequence number.
                                //int DecodingOrderNumber = packetKey;

                                //DON Present in FU - B, add the DON to the DecodingOrderNumber
                                if (nalUnitType == Media.Codecs.Video.H264.NalUnitType.FragmentationUnitB && count >= 2)
                                {
                                    //Needs 2 more bytes...
                                    /*packetKey += 0.101 * */
                                    Common.Binary.ReadU16(packetData, ref offset, Common.Binary.IsLittleEndian);

                                    //Adjust count
                                    count -= 2;
                                }

                                //Should verify count... just consumed 1 - 3 bytes and only required 2.

                                //Determine the fragment size by what remains
                                int fragment_size = count; // = - (offset - packet.Payload.Offset);

                                //Should be optional
                                //Don't emit empty fragments
                                //if (fragment_size is 0) return;

                                //Reconstruct the nal header
                                //Use the first 3 bits of the first byte and last 5 bites of the FU Header
                                byte nalHeader = (byte)((firstByte & 0xE0) | (FUHeader & Common.Binary.FiveBitMaxValue));

                                //If the start bit was set
                                if (Start)
                                {
                                    //ignoreEndBit
                                    //if (End) throw new InvalidOperationException("Start and End Bit Set in same FU");

                                    //Reuse the data we don't need which was previously used to indicate the type of fragmentation unit or length
                                    packetData[offset - 1] = nalHeader;

                                    //(Stores the nal) Write the start code
                                    DepacketizeStartCode(ref packetKey, ref nalHeader, fullStartCodes);

                                    //Wasteful but there is no other way to provide this byte since it is constructor from two values in the header
                                    //Unless of course a FragmentHeader : MemorySegment was created, which could have a NalType property ...
                                    //Could also just have an overload which writes the NalHeader
                                    //Would need a CreateNalSegment static method with option for full (4 + 1) or short code ( 3 + 1)/

                                    //The nalHeader was written @ offset - 1 and is 1 byte
                                    Depacketized.Add(packetKey++, new Common.MemorySegment(packetData, offset - 1, 1));
                                }

                                //Add the depacketized data but only if there is data in the fragment.
                                Depacketized.Add(packetKey++, new Common.MemorySegment(packetData, offset, fragment_size));

                                //else Depacketized.Add(packetKey++, Common.MemorySegment.Empty);
                                //Allow If End to Write End Sequence?
                                //Should only be done if last byte is 0?
                                //if (End) Buffer.WriteByte(Media.Codecs.Video.H264.NalUnitType.EndOfSequence);                                
                            }

                            //No more data?
                            return;
                        }
                    default: //Any other type excluding PayloadContentScalabilityInformation(30) and Reserved(31)
                        {
                            //(Stores the nalUnitType) Write the start code
                            DepacketizeStartCode(ref packetKey, ref nalUnitType, fullStartCodes);

                            //Add the depacketized data
                            Depacketized.Add(packetKey++, new Common.MemorySegment(packetData, offset, count));

                            return;
                        }
                }
            }

            //internal protected void WriteStartCode(ref byte nalHeader, bool fullStartCodes = false)
            //{
            //    int addIndex = Depacketized.Count;

            //    DepacketizeStartCode(ref addIndex, ref nalHeader, fullStartCodes);
            //}

            //internal static Common.MemorySegment CreateStartCode(ref byte nalType)
            //{

            //}

            protected internal void DepacketizeStartCode(ref int addIndex, ref byte nalHeader, bool fullStartCodes = false)
            {
                //Determine the type of Nal
                byte nalType = (byte)(nalHeader & Common.Binary.FiveBitMaxValue);

                //Store the nalType contained (this is possibly not in sorted order of which they occur)
                m_ContainedNalTypes.Add(nalType);

                if (fullStartCodes)
                {
                    Depacketized.Add(addIndex++, FullStartSequenceSegment);

                    return;
                }

                //Determine the type of start code prefix required.
                switch (nalType)
                {
                    ////Should technically only be written for first iframe in au and only when not precceded by sps and pps
                    //case Media.Codecs.Video.H264.NalUnitType.InstantaneousDecoderRefresh://5
                    //case Media.Codecs.Video.H264.NalUnitType.SequenceParameterSetSubset:// 15 (6190)
                    //    {
                    //        //Check if first nal in Access Unit m_ContainedNalTypes[0] == Media.Codecs.Video.H264.NalUnitType.SequenceParameterSetSubset;
                    //        if (m_Buffer.Position is 0) goto case Media.Codecs.Video.H264.NalUnitType.SequenceParameterSet;

                    //        //Handle without extra byte
                    //        goto default;
                    //    }
                    case Media.Codecs.Video.H264.NalUnitType.SupplementalEncoderInformation://6:                    
                    case Media.Codecs.Video.H264.NalUnitType.SequenceParameterSet://7:
                    case Media.Codecs.Video.H264.NalUnitType.PictureParameterSet://8:
                    case Media.Codecs.Video.H264.NalUnitType.AccessUnitDelimiter://9                                        
                        {
                            //Use the FullStartSequence
                            Depacketized.Add(addIndex++, FullStartSequenceSegment);

                            return;
                        }
                    // See: [ITU-T H.264] 7.4.1.2.4 Detection of the first VCL NAL unit of a primary coded picture
                    //case Media.Codecs.Video.H264.NalUnitType.CodedSlice:1  (6190)
                    //case Media.Codecs.Video.H264.NalUnitType.SliceExtension:20  (6190)
                    //    {
                    //        //Write the extra 0 byte to the Buffer (could also check for a contained slice header to eliminate the possibility of needing to check?)
                    //        if (m_Buffer.Position is 0 && isFirstVclInPrimaryCodedPicture()) goto case Media.Codecs.Video.H264.NalUnitType.AccessUnitDelimiter;

                    //        //Handle as normal
                    //        goto default;
                    //    }
                    default:
                        {
                            #region Notes

                            /* 7.1 NAL Unit Semantics
                             
                            1) The first byte of the RBSP contains the (most significant, left-most) eight bits of the SODB; the next byte of
                            the RBSP shall contain the next eight bits of the SODB, etc., until fewer than eight bits of the SODB remain.
                            
                            2) rbsp_trailing_bits( ) are present after the SODB as follows:
                            i) The first (most significant, left-most) bits of the final RBSP byte contains the remaining bits of the SODB,
                            (if any)
                            ii) The next bit consists of a single rbsp_stop_one_bit equal to 1, and
                            iii) When the rbsp_stop_one_bit is not the last bit of a byte-aligned byte, one or more
                            rbsp_alignment_zero_bit is present to result in byte alignment.
                            
                            3) One or more cabac_zero_word 16-bit syntax elements equal to 0x0000 may be present in some RBSPs after
                            the rbsp_trailing_bits( ) at the end of the RBSP.
                            Syntax structures having these RBSP properties are denoted in the syntax tables using an "_rbsp" suffix. These
                            structures shall be carried within NAL units as the content of the rbsp_byte[ i ] data bytes. The association of the RBSP
                            syntax structures to the NAL units shall be as specified in Table 7-1.
                            NOTE - When the boundaries of the RBSP are known, the decoder can extract the SODB from the RBSP by concatenating the bits
                            of the bytes of the RBSP and discarding the rbsp_stop_one_bit, which is the last (least significant, right-most) bit equal to 1, and
                            discarding any following (less significant, farther to the right) bits that follow it, which are equal to 0. The data necessary for the
                            decoding process is contained in the SODB part of the RBSP.
                            emulation_prevention_three_byte is a byte equal to 0x03. When an emulation_prevention_three_byte is present in the
                            NAL unit, it shall be discarded by the decoding process.
                            The last byte of the NAL unit shall not be equal to 0x00.
                            Within the NAL unit, the following three-byte sequences shall not occur at any byte-aligned position:
                            – 0x000000
                            – 0x000001
                            – 0x000002
                            Within the NAL unit, any four-byte sequence that starts with 0x000003 other than the following sequences shall not
                            occur at any byte-aligned position:
                            – 0x00000300
                            – 0x00000301
                            – 0x00000302
                            – 0x00000303 
                             
                             */

                            //Could also check last byte(s) in buffer to ensure no 0...

                            //FFMPEG changed to always emit full start codes.
                            //https://ffmpeg.org/doxygen/trunk/rtpdec__h264_8c_source.html

                            #endregion

                            //Add the short start sequence
                            Depacketized.Add(addIndex++, ShortStartSequenceSegment);

                            //Done
                            return;
                        }
                }
            }

            //Removing a packet effects m_ContainedNalTypes

            //protected internal override void RemoveAt(int index, bool disposeBuffer = true)
            //{
            //    base.RemoveAt(index, disposeBuffer);
            //}

            //internal protected override void DisposeBuffer()
            //{
            //    //The nals are definetely still contained when the buffer is disposed...
            //    m_ContainedNalTypes.Clear();

            //    base.DisposeBuffer();
            //}


            //The references to the list control it's disposition.
            //The property is marked as readonly and is not exposed anyway so clearing it is more work than is necessary.
            //public override void Dispose()
            //{
            //    base.Dispose();

            //    if (ShouldDispose) m_ContainedNalTypes.Clear();
            //}


            //To go to an Image...
            //Look for a SliceHeader in the Buffer
            //Decode Macroblocks in Slice
            //Convert Yuv to Rgb
        }

        #region Fields

        //Should be created dynamically

        //https://www.cardinalpeak.com/blog/the-h-264-sequence-parameter-set

        //TODO, Use a better starting point e.g. https://github.com/jordicenzano/h264simpleCoder/blob/master/src/CJOCh264encoder.h or the OpenH264 stuff @ https://github.com/cisco/openh264

        //https://stackoverflow.com/questions/6394874/fetching-the-dimensions-of-a-h264video-stream

        //The sps should be changed to reflect the correct amount of macro blocks for the width and height specified as well as color depth.

        //profile_idc, profile_iop, level_idc

        protected internal SimpleH264Encoder encoder;

        #endregion

        #region Constructor

        public RFC6184Media(int width, int height, string name, string directory = null, bool watch = true)
            : base(name, directory, watch, width, height, false, 99)
        {
            Width = width;
            Height = height;
            Width += Width % 8;
            Height += Height % 8;
            ClockRate = 9;
            encoder = new SimpleH264Encoder((uint)width, (uint)height, 60);
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
            var mediaDescription = new Sdp.MediaDescription(Sdp.MediaType.video, Rtp.RtpClient.RtpAvpProfileIdentifier, 96, 0);
            SessionDescription.Add(mediaDescription); //This is the payload description, it is defined in the profile

            //Add the control line and media attributes to the Media Description
            mediaDescription.Add(new Sdp.SessionDescriptionLine("a=control:trackID=video")); //<- this is the id for this track which playback control is required, if there is more than 1 video track this should be unique to it

            mediaDescription.Add(new Sdp.SessionDescriptionLine("a=rtpmap:96 H264/90000")); //<- 96 must match the Payload description given above

            // Make the profile-level-id
            // Eg a string of profile-level-id=42A01E is
            // a Profile eg Constrained Baseline, Baseline, Extended, Main, High. This defines which features in H264 are used
            // a Level eg 1,2,3 or 4. This defines a max resoution for the video. 2=up to SD, 3=upto 1080p. Decoders can then reserve sufficient RAM for frame buffers
            int profile_idc = 77; // Main Profile
            int profile_iop = 0; // bit 7 (msb) is 0 so constrained_flag is false
            int level = 42; // Level 4.2


            string profile_level_id_str = profile_idc.ToString("X2") // convert to hex, padded to 2 characters
                                        + profile_iop.ToString("X2")
                                        + level.ToString("X2");


            //Prepare the profile information which is useful for receivers decoding the data, in this profile the Id, Sps and Pps are given in a base64 string.
            SessionDescription.MediaDescriptions.First().Add(new Sdp.SessionDescriptionLine("a=fmtp:96 profile-level-id=" + profile_level_id_str + ";sprop-parameter-sets=" + Convert.ToBase64String(encoder.GetRawPPS()) + ',' + Convert.ToBase64String(encoder.GetRawPPS())));

            //Create a context
            RtpClient.TryAddContext(new Rtp.RtpClient.TransportContext(0, 1,  //data and control channel id's (can be any number and should not overlap but can...)
                RFC3550.Random32(96), //A randomId which was alredy generated 
                SessionDescription.MediaDescriptions.First(), //This is the media description we just created.
                false, //Don't enable Rtcp reports because this source doesn't communicate with any clients
                RFC3550.Random32(96), // This context is not in discovery
                0)); //This context is always valid from the first rtp packet received
        }

        //Ported from https://cardinalpeak.com/downloads/hello264.c

        //Move to Codec/h264
        //H264Encoder

        /// <summary>
        /// Packetize's an Image for Sending
        /// </summary>
        /// <param name="image">The Image to Encode and Send</param>
        public override void Packetize(System.Drawing.Image image)
        {
            //If the dimensions are different
            var disposeImage = image.Width != Width || image.Height != Height;

            //Make the width and height correct by getting a resized version
            var _image = image.Width != Width || image.Height != Height ? image.GetThumbnailImage(Width, Height, null, IntPtr.Zero) : image;

            //Create a new frame
            var newFrame = new RFC6184Frame(96)
            {
                SynchronizationSourceIdentifier = SourceId,
                MaxPackets = 4096,
            };

            var bmp = (System.Drawing.Bitmap)_image;

            //Get RGB Stride
            System.Drawing.Imaging.BitmapData data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, _image.Width, _image.Height),
                        System.Drawing.Imaging.ImageLockMode.ReadOnly, _image.PixelFormat);

            //Convert to YUV
            var yuvData = !Media.Common.Binary.IsBigEndian ? Media.Codecs.Image.ColorConversions.ARGB2YUV420Managed(_image.Width, _image.Height, data.Scan0) : Media.Codecs.Image.ColorConversions.BGRA2YUV420Managed(_image.Width, _image.Height, data.Scan0);

            //SPS and PPS should be included here if key frame only
            newFrame.Packetize(encoder.GetRawSPS());
            newFrame.Packetize(encoder.GetRawPPS());

            //Packetize the data according to the MTU
            newFrame.Packetize(encoder.CompressFrame(yuvData));

            //Done with RGB
            bmp.UnlockBits(data);

            //Add the frame
            AddFrame(newFrame);

            //If we need to dispose
            if (disposeImage)
                _image.Dispose();
        }

        public override void Dispose()
        {
            if (encoder is not null)
            {
                encoder.Dispose();
                encoder = null;
            }

            base.Dispose();
        }

        #endregion
    }

    public static class RFC6184FrameExtensions
    {
        public static bool Contains(this Media.Rtsp.Server.MediaTypes.RFC6184Media.RFC6184Frame frame, params byte[] nalTypes)
        {
            return !Common.IDisposedExtensions.IsNullOrDisposed(frame)
&& !Common.Extensions.Array.ArrayExtensions.IsNullOrEmpty(nalTypes)
&& frame.m_ContainedNalTypes.Any(n => nalTypes.Contains(n));
        }

        public static bool IsKeyFrame(this Media.Rtsp.Server.MediaTypes.RFC6184Media.RFC6184Frame frame)
        {
            if (Common.IDisposedExtensions.IsNullOrDisposed(frame)) return false;

            foreach (byte type in frame.m_ContainedNalTypes)
            {
                if (type == Media.Codecs.Video.H264.NalUnitType.InstantaneousDecoderRefresh) return true;

                byte nalType = type;

                //Check for the SliceType or IDR
                if (Media.Codecs.Video.H264.NalUnitType.IsSlice(ref nalType))
                {
                    //Todo, 
                    //Get type slice type from the slice header.

                    //This logic is also useful for reading the frame number which is needed to determine full or short start codes

                    /* https://code.mythtv.org/doxygen/H264Parser_8cpp_source.html
                    slice_type specifies the coding type of the slice according to
                    Table 7-6.   e.g. P, B, I, SP, SI
 
                    When nal_unit_type is equal to 5 (IDR picture), slice_type shall
                    be equal to 2, 4, 7, or 9 (I or SI)
                    */

                    //Should come from the payload

                    //FirstMbInSLice
                    //SliceType
                    //Pps ID

                    byte sliceType = nalType; // = get_ue_golomb_31(gb);

                    if (Media.Codecs.Video.H264.SliceType.IsIntra(ref sliceType)) return true;
                }
            }

            return false;
        }
    }
}