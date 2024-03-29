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
using Media.Container;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Media.Containers.BaseMedia
{
    /// <summary>
    /// Represents the logic necessary to read ISO Complaint Base Media Format Files.
    /// <see href="https://en.wikipedia.org/wiki/ISO_base_media_file_format">Wikipedia</see>
    /// Formats include QuickTime (.qt, .mov, .mp4, .m4v, .m4a), 
    /// Microsoft Smooth Streaming (.ismv, .isma, .ismc), 
    /// JPEG2000 (.jp2, .jpf, .jpx), Motion JPEG2000 (.mj2, .mjp2), 
    /// 3GPP/3GPP2 (.3gp, .3g2), Adobe Flash (.f4v, .f4p, .f4a, .f4b) and other conforming format extensions.
    /// Samsung Video (.svi)
    /// </summary>
    //https://dvcs.w3.org/hg/html-media/raw-file/tip/media-source/isobmff-byte-stream-format.html
    public class BaseMediaReader : MediaFileStream
    {
        private static readonly DateTime IsoBaseDateUtc = new(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        //Todo Make Generic.Dictionary and have a ToTextualConvention that tries the Generic.Dictionary first. (KnownParents)        

        //Should be int type instead of string.

        //TryRegisterParentBox
        //Try UnregisterParentBox

        /// <summary>
        /// <see href="http://www.mp4ra.org/atoms.html">MP4REG</see>
        /// </summary>
        public static List<string> ParentBoxes =
        [
            "moof", //movie fragment
            "mfhd", //movie fragment header
            "traf", //track fragment
            //tfhd track fragment header
            //trun track fragment run
            //sbgp sample-to-group 
            //sgpd sample group description 
            //subs sub-sample information 
            //saiz sample auxiliary information sizes 
            //saio sample auxiliary information offsets 
            //tfdt track fragment decode time 
            "mfra", //movie framgment radom access
            //"tfra", //8.8.10 track fragment random access
            //"mfro", //* 8.8.11 movie fragment random access offset 
            "moov",
            "trak",
            "mdia",
            "mdra",
            "rmra",
            //"mdhd",
            //"hdlr",
            "minf",
            "dinf",
            "stbl",
            "edts",
            "stsd",
            //"tkhd", //Track Header
            "tref", //Track Reference Container
            "trgr", //Track Grouping Indicator
            "skip",
            //"udta",
            "mvex", //movie extends box
            //mehd 8.8.2 movie extends header box
            //trex * 8.8.3 track extends defaults
            //leva 8.8.13 level assignment 
            "cprt",
            "strk", //sub track
            //"stri", //sub track information box
            //"strd" //sub track definition box
            "meta",
            "imap",
            "imag",
            "iloc",
            "ipro",
            "sinf",
            //frma 8.12.2 original format box
            //schm 8.12.5 scheme type box
            //schi 8.12.6 scheme information box 
            "fiin", //file delivery item information 
            "paen", //partition entry 
            "segr", //file delivery session group 
            "gitn", //group id to name 
            "meco", //additional metadata container,
            "udta",
            "vnrp",
        ];

        //could add level to node by extending or adding bytes to header.
        //Could also track in reader.

        public const string UserDefined = "uuid";
        private const int BytesPerUUID = 16;

        public static byte[] IsoUUIDTemplate = new byte[] {
                                                            0x00, 0x00, 0x00, 0x00, /*XXXX*/
                                                            0x00, 0x11, 0x00, 0x10,
                                                            0x80, 0x00, 0x00, 0xAA,
                                                            0x00, 0x39, 0x9B, 0x71
                                                           };
        private const int TemplateSize = 12;
        private const int MinimumSize = IdentifierSize + LengthSize, IdentifierSize = 4, LengthSize = IdentifierSize;

        public static string ToUTF8FourCharacterCode(byte[] identifier, int offset = 0, int count = 4)
        {
            return ToEncodedFourCharacterCode(Encoding.UTF8, identifier, offset, count);
        }

        public static string ToEncodedFourCharacterCode(Encoding encoding, byte[] identifier, int offset, int count)
        {
            return encoding is null
                ? throw new ArgumentNullException("encoding")
                : identifier is null
                ? throw new ArgumentNullException("identifier")
                : offset + count > identifier.Length
                ? throw new ArgumentOutOfRangeException("offset and count must relfect a position within identifier.")
                : encoding.GetString(identifier, offset, count);
        }

        public static bool IsUserDefinedNode(BaseMediaReader reader, Node node)
        {
            return IsUserDefinedIdentifier(reader, node.Identifier);
        }

        public static bool IsUserDefinedIdentifier(BaseMediaReader reader, byte[] identifier, int offset = 0, int count = 4)
        {
            return (ToUTF8FourCharacterCode(identifier, offset, count) == UserDefined);
        }

        public static Guid GetUniqueIdentifier(BaseMediaReader reader, Node node)
        {
            //Allocate the 16 bytes for the uuid
            byte[] uuidBytes = new byte[BytesPerUUID];

            //If the node is a UUID type
            if (IsUserDefinedNode(reader, node))
            {
                //Read the UUID from the identifier which comes after the uuid name.
                Array.Copy(node.Identifier, IdentifierSize, uuidBytes, 0, BytesPerUUID);
            }
            else
            {
                //Copy the identifier to the uuid
                Array.Copy(node.Identifier, 0, uuidBytes, 0, IdentifierSize);

                //Copy the template value to the uuid after the identifier
                Array.Copy(IsoUUIDTemplate, IdentifierSize, uuidBytes, IdentifierSize, TemplateSize);
            }

            //Return the result of parsing a Guid from the uuid bytes. The UUID should be in big endian format...
            //BitOrder == LeastSignificant
            return Common.Binary.ReadGuid(uuidBytes, 0, Common.Binary.IsLittleEndian);  //new Guid(uuidBytes);
        }

        public BaseMediaReader(string filename, System.IO.FileAccess access = System.IO.FileAccess.Read) : base(filename, access) { }

        public BaseMediaReader(Uri source, System.IO.FileAccess access = System.IO.FileAccess.Read) : base(source, access) { }

        public BaseMediaReader(System.IO.FileStream source, System.IO.FileAccess access = System.IO.FileAccess.Read) : base(source, access) { }

        public BaseMediaReader(Uri uri, System.IO.Stream source, int bufferSize = 8192) : base(uri, source, null, bufferSize, true) { }

        //int[] names?

        public IEnumerable<Node> ReadBoxes(Node node, params string[] names) => ReadBoxes(node.DataOffset, names);

        public IEnumerable<Node> ReadBoxes(long offset, long count, params string[] names)
        {
            long positionStart = Position;

            Position = offset;

            foreach (var box in this)
            {
                if (names is null || names.Count() is 0 || names.Contains(ToUTF8FourCharacterCode(box.Identifier)))
                {
                    yield return box;
                }

                //Ensure the TotalSize is correctly set subtract from the count
                count -= box.TotalSize > count ? box.TotalSize - box.DataSize : box.TotalSize;

                //If the count approaches 0 then stop
                if (count <= 0 /*&& m_Position >= m_Length*/) break;

            }

            //Seek to the position previous to reading (should be optional?)
            Position = positionStart;

            yield break;
        }

        public IEnumerable<Node> ReadBoxes(long offset = 0, params string[] names) { return ReadBoxes(offset, Length - offset, names); }

        public Node ReadBox(string name, long offset, long count)
        {
            long positionStart = Position;

            Node result = ReadBoxes(offset, count, name).FirstOrDefault();

            Position = positionStart;

            return result;
        }

        public Node ReadBox(string name, long offset = 0) { return ReadBox(name, offset, Length - offset); }

        public static byte[] ReadIdentifier(Stream stream)
        {
            //if (Remaining < IdentifierSize) return null;

            byte[] identifier = new byte[IdentifierSize];

            stream.Read(identifier, 0, IdentifierSize);

            return identifier;
        }

        public static long ReadLength(Stream stream, out int bytesRead, byte[] buffer = null, int offset = 0)
        {
            //4.2 Object Structure 
            bytesRead = 0;

            ulong length = 0;

            //Allocate 8 bytes
            byte[] lengthBytes = buffer ?? new byte[MinimumSize];

            //Try to read the length
            try
            {
                //0 sometimes indicates unknown length or a poorly written box
                //1 means that a 64 bit length follows or at least 32 bits of such...
                do
                {
                    //Read a word and calculate the amount of bytes read
                    bytesRead += stream.Read(lengthBytes, offset, LengthSize);

                    //Calculate the length
                    length = (uint)Common.Binary.Read32(lengthBytes, 0, Common.Binary.IsLittleEndian);

                    //Repeat while 0 or 1 was found
                } while (length <= 1);

                //By 'my' logic when a '1' was read which indicated the length is unknown or first 32 bits are full, we don't need to read 8 more just 4 since that is what would be different...
                //This logic / optomization is not inline with the standard and may be changed later.                
            }
            catch
            {
                //
            }

            //Todo, assert length == bytesRead

            return (long)length;
        }

        public Node ReadNext()
        {
            if (Remaining <= MinimumSize) throw new System.IO.EndOfStreamException();

            //Keep track of how many bytes used in the length

            //Keep the length bytes
            byte[] lengthBytes = new byte[LengthSize];

            //Read the length
            ulong length = (ulong)ReadLength(this, out int lengthBytesRead, lengthBytes, 0);

            //Keep the identifier bytes
            byte[] identifier = new byte[IdentifierSize];

            //Read the box identifier
            int identifierSize = Read(identifier, 0, IdentifierSize);

            //If this is a user defined type, then it must be read to access the length and to property get the data offset.
            //The reason why this is not nested in the check for the extended length is that the identifier should always be kept seperate from the data of the node.
            if (IsUserDefinedIdentifier(this, identifier))
            {
                //Increase the identifier size by 16
                identifierSize += BytesPerUUID;

                //Resize the array to 20 bytes
                Array.Resize(ref identifier, 20);

                //Read the 16 byte uuid which follows
                Read(identifier, IdentifierSize, BytesPerUUID);

                //The length of the node cannot include the uuid since we just read it, however TotalSize will reflect this correctly.                
                length -= BytesPerUUID;
            }

            //If the length is 0 then the size should be limited to the previous parent box length for seeking.
            //Could keep a instance variable m_ParentRemains := Remaining
            //Could decrement it for every node encountered while IsParentBox is false.
            //Otherwise m_ParentRemains := length - MinimumSize

            //If at least 4 bytes were read for the length and the length is int.MaxValue there is another word indicating the last 32 bits of the 64 bit length
            if (lengthBytesRead >= LengthSize && length == int.MaxValue)
            {
                //Read 4 bytes into the lengthBytes
                Read(lengthBytes, 0, LengthSize);

                //These bytes don't count towards the length bytes read depending on who write the file,
                //This also probably doesn't matter as long as the amount which follows is correct.
                lengthBytesRead += Common.Binary.BytesPerInteger;

                //Calculate the length
                length = (int.MaxValue << Common.Binary.BitsPerInteger) | (uint)Common.Binary.Read32(lengthBytes, 0, Common.Binary.IsLittleEndian);
            }

            //Todo, validity? (also unsigned overlaods)

            //Return the node, the Position is the position of the data in the node.
            //IdentitiferSize could be obtained from segment to identifier?
            return new Node(this, new Common.MemorySegment(identifier), identifierSize, lengthBytesRead, Position, (long)(length - MinimumSize), //The length does not include the 8 bytes used for identifier and length
                identifierSize + lengthBytesRead >= MinimumSize && length <= (ulong)Remaining);//Complete if enough bytes were read and end is within file size...
        }

        public override IEnumerator<Node> GetEnumerator()
        {
            while (Remaining > MinimumSize)
            {
                Node next = ReadNext();

                if (next is null) yield break;

                yield return next;

                //Parent boxes contain other boxes so do not skip them, parse right into their data
                //Could handle long size here also by keeping track of the amount of data in next.
                if (ParentBoxes.Contains(ToUTF8FourCharacterCode(next.Identifier))) continue;

                //The length field of the node includes the identifier and the 4 bytes indicating the length of the data
                //The length field itself may actually have more than 4 bytes but IT SEEMS the dataSize is never calulcated using more than 4 bytes of the length, need more example files to verify
                //This implies that the DataSize can be trusted since the length has been altered by ReadNext() to account for this.                

                ulong dataSize = (ulong)(next.DataSize);

                //Keep track of how much was skipped
                ulong skipped = 0, toSkip = 0;

                //Skip the data
                while (skipped < dataSize)
                {
                    //Todo use unsafe conversion
                    toSkip = Common.Binary.Clamp(dataSize, (ulong)0, (ulong)long.MaxValue);

                    //Todo unsigned overloads.
                    Skip((long)toSkip);

                    skipped += toSkip;
                }
            }
        }

        /// <summary>
        /// Reads an Element with the given name which occurs at the current Position or later
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public Node this[string name]
        {
            get
            {
                return ReadBoxes(Position, name).FirstOrDefault();
            }
        }

        public override Node Root { get { return ReadBox("ftyp", 0); } }

        public override string ToTextualConvention(Node node)
        {
            return node.Master.Equals(this) ? BaseMediaReader.ToUTF8FourCharacterCode(node.Identifier) : base.ToTextualConvention(node);
        }

        public bool HasProtection
        {
            //pssh/cenc
            //TrackLevel Encryption = tenc
            get { return ReadBoxes(Root.DataOffset, "ipro", "sinf").Count() >= 1; }
        }

        private DateTime? m_Created, m_Modified;

        public DateTime Created
        {
            get
            {
                if (false == m_Created.HasValue) ParseMovieHeader();
                return m_Created.Value;
            }
        }

        public DateTime Modified
        {
            get
            {
                if (false == m_Modified.HasValue) ParseMovieHeader();
                return m_Modified.Value;
            }
        }

        private ulong? m_TimeScale;
        private TimeSpan? m_Duration;

        public TimeSpan Duration
        {
            get
            {
                if (false == m_Duration.HasValue) ParseMovieHeader();
                return m_Duration.Value;
            }
        }

        private float? m_PlayRate, m_Volume;

        public float PlayRate
        {
            get
            {
                if (false == m_PlayRate.HasValue) ParseMovieHeader();
                return m_PlayRate.Value;
            }
        }

        public float Volume
        {
            get
            {
                if (false == m_Volume.HasValue) ParseMovieHeader();
                return m_Volume.Value;
            }
        }

        private byte[] m_Matrix;

        public byte[] Matrix
        {
            get
            {
                if (m_Matrix is null) ParseMovieHeader();
                return m_Matrix;
            }
        }

        private int? m_NextTrackId;

        public int NextTrackId
        {
            get
            {
                if (false == m_NextTrackId.HasValue) ParseMovieHeader();
                return m_NextTrackId.Value;
            }
        }

        protected void ParseMovieHeader()
        {
            ulong duration;

            //Obtain the timeScale and duration from the LAST? mdhd box, can do but is more latent if the file is large...
            using (var mediaHeader = ReadBox("mvhd", Root.Offset)) // ReadBoxes(Root.Offset, "mvhd").LastOrDefault())
            {
                if (mediaHeader is null) throw new InvalidOperationException("Cannot find 'mvhd' box.");

                int offset = 0;

                int versionAndFlags = Common.Binary.Read32(mediaHeader.Data, ref offset, Common.Binary.IsLittleEndian), version = versionAndFlags >> 24 & 0xff;

                ulong created = 0, modified = 0;

                switch (version)
                {
                    case 0:
                        {
                            created = Common.Binary.ReadU32(mediaHeader.Data, ref offset, Common.Binary.IsLittleEndian);

                            modified = Common.Binary.ReadU32(mediaHeader.Data, ref offset, Common.Binary.IsLittleEndian);

                            m_TimeScale = Common.Binary.ReadU32(mediaHeader.Data, ref offset, Common.Binary.IsLittleEndian);

                            duration = Common.Binary.ReadU32(mediaHeader.Data, ref offset, Common.Binary.IsLittleEndian);

                            break;
                        }

                    case 1:
                        {
                            created = Common.Binary.ReadU64(mediaHeader.Data, ref offset, Common.Binary.IsLittleEndian);

                            modified = Common.Binary.ReadU64(mediaHeader.Data, ref offset, Common.Binary.IsLittleEndian);

                            m_TimeScale = Common.Binary.ReadU32(mediaHeader.Data, ref offset, Common.Binary.IsLittleEndian);

                            duration = Common.Binary.ReadU64(mediaHeader.Data, ref offset, Common.Binary.IsLittleEndian);

                            break;
                        }
                    default: throw new NotSupportedException("Only Version 0 and 1 are defined.");
                }

                //Rate Volume NextTrack

                m_PlayRate = Common.Binary.Read32(mediaHeader.Data, ref offset, Common.Binary.IsLittleEndian) / 65536f;

                m_Volume = Common.Binary.ReadU16(mediaHeader.Data, ref offset, Common.Binary.IsLittleEndian) / 256f;

                m_Matrix = mediaHeader.Data.Skip(offset).Take(36).ToArray();

                offset += 36;

                offset += 28;

                m_NextTrackId = Common.Binary.Read32(mediaHeader.Data, ref offset, Common.Binary.IsLittleEndian);

                m_Created = IsoBaseDateUtc.AddMilliseconds(created * Media.Common.Extensions.TimeSpan.TimeSpanExtensions.MicrosecondsPerMillisecond);

                m_Modified = IsoBaseDateUtc.AddMilliseconds(modified * Media.Common.Extensions.TimeSpan.TimeSpanExtensions.MicrosecondsPerMillisecond);

                m_Duration = TimeSpan.FromSeconds(duration / (double)m_TimeScale.Value);
            }
        }

        //Should be a better box... (meta ,moov, mfra?)?
        public override Node TableOfContents
        {
            get { return ReadBoxes(Root.Offset, "stco", "co64").FirstOrDefault(); }
        }

        private List<Track> m_Tracks;

        public override IEnumerable<Track> GetTracks() //bool enabled tracks only?
        {

            if (m_Tracks is not null)
            {
                foreach (Track track in m_Tracks) yield return track;
                yield break;
            }

            var tracks = new List<Track>();

            long position = Position;

            //Get Duration from mdhd, some files have more then one mdhd.
            if (false == m_Duration.HasValue) ParseMovieHeader();

            //traf should also be supported.
            foreach (var trakBox in ReadBoxes(Root.Offset, "trak").ToArray())
            {
                //Define variables which need to be reset for every track.

                int trackId = 0;

                ulong created = 0, modified = 0, duration = 0;

                int width = 0, height = 0;

                bool enabled = false, inMovie, inPreview;

                byte[] codecIndication = Media.Common.MemorySegment.EmptyBytes;

                float volume = m_Volume.Value;

                int offset = 0, version = 0, flags = 0;

                byte[] rawData;

                ulong trackTimeScale = m_TimeScale.Value, trackDuration = duration;

                DateTime trackCreated = m_Created.Value, trackModified = m_Modified.Value;

                Sdp.MediaType mediaType = Sdp.MediaType.unknown;

                string name = string.Empty;

                byte channels = 0, bitDepth = 0;

                double rate = 0;

                List<Tuple<long, long>> sttsEntries = [];

                List<long> stOffsets = [];

                List<int> stSizes = [];

                TimeSpan startTime = TimeSpan.Zero;

                //MAKE ONLY A SINGLE PASS HERE TO REDUCE IO
                using (var stream = trakBox.DataStream)
                {
                    int bytesRead = 0;

                    long length = 0, streamPosition = stream.Position, streamLength = stream.Length;

                    byte[] identifier;

                    //Note could use RawData from trakBox
                    //Would just need a way to ReadLength and Identifier from byte[] rather than Stream.

                    //This would also work but it would cause a seek
                    //foreach(var node in ReadBoxes(trakBox.DataOffset, trakBox.DataSize, null))                    

                    //While there is data in the stream
                    while (streamPosition < streamLength)
                    {
                        //Read the length
                        length = ReadLength(stream, out bytesRead);

                        streamPosition += bytesRead;

                        //Read the identifier
                        identifier = ReadIdentifier(stream);

                        bytesRead += IdentifierSize;

                        streamPosition += IdentifierSize;

                        length -= MinimumSize;

                        offset = 0;

                        string boxName = ToUTF8FourCharacterCode(identifier);

                        //Determine what to do
                        switch (boxName)
                        {
                            // Next Node has data
                            case "trak": continue;
                            case "elst":
                                {
                                    rawData = new byte[length];

                                    stream.Read(rawData, 0, (int)length);

                                    List<Tuple<int, int, float>> edits = [];

                                    //Skip Flags and Version
                                    offset = LengthSize;

                                    int entryCount = Common.Binary.Read32(rawData, offset, Common.Binary.IsLittleEndian);

                                    offset += 4;

                                    for (int i = 0; i < entryCount && offset < length; ++i)
                                    {
                                        //Edit Duration, MediaTime, Rate
                                        edits.Add(new Tuple<int, int, float>(Common.Binary.Read32(rawData, ref offset, Common.Binary.IsLittleEndian),
                                            Common.Binary.Read32(rawData, ref offset, Common.Binary.IsLittleEndian),
                                            Common.Binary.Read32(rawData, ref offset, Common.Binary.IsLittleEndian) / ushort.MaxValue));
                                    }

                                    if (edits.Count > 0 && edits[0].Item2 > 0)
                                    {
                                        startTime = TimeSpan.FromMilliseconds(edits[0].Item2);
                                    }

                                    offset = (int)length;

                                    goto default;

                                }
                            case "tkhd"://tfhd
                                {

                                    rawData = new byte[length];

                                    stream.Read(rawData, 0, (int)length);

                                    version = rawData[offset++];

                                    flags = Common.Binary.Read24(rawData, offset, Common.Binary.IsLittleEndian);

                                    offset += 3;

                                    enabled = ((flags & 1) == flags);

                                    inMovie = ((flags & 2) == flags);

                                    inPreview = ((flags & 3) == flags);

                                    if (version is 0)
                                    {
                                        created = Common.Binary.ReadU32(rawData, ref offset, Common.Binary.IsLittleEndian);

                                        modified = Common.Binary.ReadU32(rawData, ref offset, Common.Binary.IsLittleEndian);
                                    }
                                    else
                                    {
                                        created = Common.Binary.ReadU64(rawData, ref offset, Common.Binary.IsLittleEndian);

                                        modified = Common.Binary.ReadU64(rawData, ref offset, Common.Binary.IsLittleEndian);
                                    }

                                    trackId = Common.Binary.Read32(rawData, ref offset, Common.Binary.IsLittleEndian);

                                    //Skip if not the first active track in the moov header..
                                    //if (trackId < NextTrackId) continue;

                                    //Skip
                                    offset += 4;

                                    //Get Duration
                                    duration = version is 0
                                        ? Common.Binary.ReadU32(rawData, ref offset, Common.Binary.IsLittleEndian)
                                        : Common.Binary.ReadU64(rawData, ref offset, Common.Binary.IsLittleEndian);

                                    if (duration == 4294967295L) duration = ulong.MaxValue;

                                    //Reserved
                                    offset += 8;

                                    //int layer = Common.Binary.ReadU16(rawData, ref offset, Common.Binary.IsLittleEndian);

                                    //int altGroup = Common.Binary.ReadU16(rawData, ref offset, Common.Binary.IsLittleEndian);

                                    offset += 4;

                                    volume = Common.Binary.ReadU16(rawData, ref offset, Common.Binary.IsLittleEndian) / 256;

                                    //Skip int and Matrix
                                    offset += 38;

                                    //Width
                                    width = Common.Binary.Read32(rawData, ref offset, Common.Binary.IsLittleEndian) / ushort.MaxValue;

                                    //Height
                                    height = Common.Binary.Read32(rawData, ref offset, Common.Binary.IsLittleEndian) / ushort.MaxValue;

                                    offset = (int)length;

                                    goto default;
                                }
                            case "mdhd":
                                {
                                    rawData = new byte[length];

                                    stream.Read(rawData, 0, (int)length);

                                    version = rawData[offset++];

                                    flags = Common.Binary.Read24(rawData, ref offset, Common.Binary.IsLittleEndian);

                                    ulong mediaCreated, mediaModified, timescale, mediaduration;

                                    if (version is 0)
                                    {

                                        mediaCreated = Common.Binary.ReadU32(rawData, ref offset, Common.Binary.IsLittleEndian);

                                        mediaModified = Common.Binary.ReadU32(rawData, ref offset, Common.Binary.IsLittleEndian);

                                        timescale = Common.Binary.ReadU32(rawData, ref offset, Common.Binary.IsLittleEndian);

                                        mediaduration = Common.Binary.ReadU32(rawData, ref offset, Common.Binary.IsLittleEndian);
                                    }
                                    else
                                    {
                                        mediaCreated = Common.Binary.ReadU64(rawData, ref offset, Common.Binary.IsLittleEndian);

                                        mediaModified = Common.Binary.ReadU64(rawData, ref offset, Common.Binary.IsLittleEndian);

                                        timescale = Common.Binary.ReadU32(rawData, ref offset, Common.Binary.IsLittleEndian);

                                        mediaduration = Common.Binary.ReadU64(rawData, ref offset, Common.Binary.IsLittleEndian);
                                    }

                                    trackTimeScale = timescale;

                                    trackDuration = mediaduration;

                                    trackCreated = IsoBaseDateUtc.AddMilliseconds(mediaCreated * Media.Common.Extensions.TimeSpan.TimeSpanExtensions.MicrosecondsPerMillisecond);

                                    trackModified = IsoBaseDateUtc.AddMilliseconds(mediaModified * Media.Common.Extensions.TimeSpan.TimeSpanExtensions.MicrosecondsPerMillisecond);

                                    offset = (int)length;

                                    goto default;
                                }
                            case "stsd":
                                {
                                    //H264
                                    // stsd/avc1/avcC contains a field 'lengthSizeMinusOne' specifying the length. But the default is 4.

                                    rawData = new byte[length];

                                    stream.Read(rawData, 0, (int)length);

                                    int sampleDescriptionCount = length > 0 ? Common.Binary.Read32(rawData, LengthSize, Common.Binary.IsLittleEndian) : 0;

                                    offset = MinimumSize;

                                    if (sampleDescriptionCount > 0)
                                    {
                                        for (int i = 0; i < sampleDescriptionCount; ++i)
                                        {
                                            int len = Common.Binary.Read32(rawData, ref offset, Common.Binary.IsLittleEndian) - 4;

                                            var sampleEntry = rawData.Skip(offset).Take(len);
                                            offset += len;

                                            switch (mediaType)
                                            {
                                                case Sdp.MediaType.audio:
                                                    {
                                                        //Maybe == mp4a
                                                        codecIndication = sampleEntry.Take(4).ToArray();

                                                        //32, 16, 16 (dref index)
                                                        version = Common.Binary.Read16(sampleEntry, 8, Common.Binary.IsLittleEndian);

                                                        //Revision 16, Vendor 32

                                                        //ChannelCount 16
                                                        channels = (byte)Common.Binary.ReadU16(sampleEntry, 20, Common.Binary.IsLittleEndian);

                                                        //SampleSize 16 (A 16-bit integer that specifies the number of bits in each uncompressed sound sample. Allowable values are 8 or 16. Formats using more than 16 bits per sample set this field to 16 and use sound description version 1.)
                                                        bitDepth = (byte)Common.Binary.ReadU16(sampleEntry, 22, Common.Binary.IsLittleEndian);

                                                        //CompressionId 16
                                                        var compressionId = sampleEntry.Skip(24).Take(2);

                                                        //Decode to a WaveFormatID (16 bit)
                                                        int waveFormatId = Common.Binary.Read16(compressionId, 0, Common.Binary.IsLittleEndian);

                                                        //The compression ID is set to -2 and redefined sample tables are used (see “Redefined Sample Tables”).
                                                        if (-2 == waveFormatId)
                                                        {
                                                            //var waveAtom = ReadBox("wave", sampleDescriptionBox.Offset);
                                                            //if (waveAtom is not null)
                                                            //{
                                                            //    flags = Common.Binary.Read24(waveAtom.Raw, 9, Common.Binary.IsLittleEndian);
                                                            //    //Extrack from flags?
                                                            //}
                                                        }//If the formatId is known then use it
                                                        else if (waveFormatId > 0) codecIndication = compressionId.ToArray();

                                                        //@ 26

                                                        //PktSize 16

                                                        //sr 32

                                                        rate = (double)Common.Binary.ReadU32(sampleEntry, 28, Common.Binary.IsLittleEndian) / 65536F;

                                                        //@ 32

                                                        if (version > 1)
                                                        {

                                                            //36 total

                                                            rate = BitConverter.Int64BitsToDouble(Common.Binary.Read64(sampleEntry, 32, Common.Binary.IsLittleEndian));
                                                            channels = (byte)Common.Binary.ReadU32(sampleEntry, 40, Common.Binary.IsLittleEndian);

                                                            //24 More Bytes
                                                        }

                                                        //else 16 more if version == 1
                                                        //else 2 more if version is 0

                                                        //@ esds for mp4a

                                                        //http://www.mp4ra.org/object.html
                                                        // @ +4 +4 +11 == ObjectTypeIndication

                                                        break;
                                                    }
                                                case Sdp.MediaType.video:
                                                    {
                                                        codecIndication = sampleEntry.Take(4).ToArray();

                                                        //SampleEntry overhead = 8
                                                        //Version, Revision, Vendor, TemporalQUal, SpacialQual, Width, Height, hRes,vRes, reversed, FrameCount, compressorName, depth, clrTbl, (extensions)

                                                        //Width @ 28
                                                        width = Common.Binary.ReadU16(sampleEntry, 28, Common.Binary.IsLittleEndian);
                                                        //Height @ 30
                                                        height = Common.Binary.ReadU16(sampleEntry, 30, Common.Binary.IsLittleEndian);

                                                        //hres, vres, reserved = 12

                                                        //FrameCount @ 44 (A 16-bit integer that indicates how many frames of compressed data are stored in each sample. Usually set to 1.)

                                                        //@46

                                                        //30 bytes compressor name (1 byte length) + 1

                                                        //@78

                                                        bitDepth = (byte)Common.Binary.ReadU16(sampleEntry, 78, Common.Binary.IsLittleEndian);

                                                        //esds box for codec specific data.

                                                        break;
                                                    }
                                            }

                                            continue;

                                        }

                                    }

                                    offset = (int)length;

                                    goto default;
                                }
                            case "stts":
                                {
                                    rawData = new byte[length];

                                    stream.Read(rawData, 0, (int)length);

                                    //Skip Flags and Version
                                    offset = LengthSize;

                                    int entryCount = Common.Binary.Read32(rawData, ref offset, Common.Binary.IsLittleEndian);

                                    for (int i = 0; i < entryCount && offset < length; ++i)
                                    {
                                        //Sample Count Sample Duration
                                        sttsEntries.Add(new Tuple<long, long>(Common.Binary.Read32(rawData, ref offset, Common.Binary.IsLittleEndian),
                                            Common.Binary.Read32(rawData, ref offset, Common.Binary.IsLittleEndian)));
                                    }

                                    offset = (int)length;

                                    goto default;
                                }
                            case "stsz":
                                {
                                    rawData = new byte[length];

                                    stream.Read(rawData, 0, (int)length);

                                    //Skip Flags and Version
                                    offset = MinimumSize;

                                    int defaultSize = Common.Binary.Read32(rawData, ref offset, Common.Binary.IsLittleEndian);

                                    int count = Common.Binary.Read32(rawData, ref offset, Common.Binary.IsLittleEndian);

                                    if (defaultSize is 0)
                                    {
                                        for (int i = 0; i < count && offset < length; ++i)
                                        {
                                            stSizes.Add(Common.Binary.Read32(rawData, ref offset, Common.Binary.IsLittleEndian));
                                        }
                                    }
                                    else
                                    {
                                        stSizes.Add(defaultSize);
                                    }

                                    offset = (int)length;

                                    goto default;
                                }
                            case "stco":
                                {
                                    rawData = new byte[length];

                                    stream.Read(rawData, 0, (int)length);

                                    //Skip Flags and Version
                                    offset = LengthSize;

                                    int chunkCount = Common.Binary.Read32(rawData, ref offset, Common.Binary.IsLittleEndian);

                                    for (int i = 0; i < chunkCount && offset < length; ++i)
                                    {
                                        stOffsets.Add(Common.Binary.Read32(rawData, ref offset, Common.Binary.IsLittleEndian));
                                    }

                                    offset = (int)length;

                                    goto default;
                                }
                            case "st64":
                                {
                                    //Could have Array return but would require cast...
                                    //Memory segment and then read from would be best..
                                    //bool TryRead(, out MemorySegment) but then how do you know the size etc?
                                    //Could have a new type SizedArray which does this for you... (e.g. Array<T>)
                                    //Todo, ReadSizedArrayAndElements(int elementSize, bool reverse = false)
                                    //Todo, ReadSizedArrayAndElements(int elementSize, offset, bool reverse = false)
                                    //Todo, ReadSizedArrayAndElements(int elementSize, offset, count, bool reverse = false)
                                    //Todo, ReadSizedArrayElements(int elementSize, bool reverse = false)

                                    rawData = new byte[length];

                                    stream.Read(rawData, 0, (int)length);

                                    //Skip Flags and Version
                                    offset = MinimumSize;

                                    int chunkCount = Common.Binary.Read32(rawData, ref offset, Common.Binary.IsLittleEndian);

                                    for (int i = 0; i < chunkCount && offset < length; ++i)
                                    {
                                        stOffsets.Add(Common.Binary.Read64(rawData, ref offset, Common.Binary.IsLittleEndian));
                                    }

                                    //should asserte///

                                    offset = (int)length;

                                    goto default;
                                }
                            case "hdlr":
                                {
                                    rawData = new byte[length];

                                    stream.Read(rawData, 0, (int)length);

                                    string comp = ToUTF8FourCharacterCode(rawData, LengthSize), sub = ToUTF8FourCharacterCode(rawData, LengthSize * 2);

                                    switch (sub)
                                    {
                                        case "vide": mediaType = Sdp.MediaType.video; break;
                                        case "soun": mediaType = Sdp.MediaType.audio; break;
                                        case "text": mediaType = Sdp.MediaType.text; break;
                                        case "tmcd": mediaType = Sdp.MediaType.timing; break;
                                        default: break;
                                    }

                                    offset = (int)length;

                                    goto default;
                                }
                            case "name":
                                {
                                    rawData = new byte[length];

                                    stream.Read(rawData, 0, (int)length);

                                    name = Encoding.UTF8.GetString(rawData);

                                    offset = (int)length;

                                    goto default;
                                }
                            default:
                                {
                                    //If the box was a parent continue parsing
                                    if (BaseMediaReader.ParentBoxes.Contains(boxName)) continue;

                                    //Determine how much to skip
                                    long toMove = length - offset;

                                    //Skip anything which remains if the offset was moved or the entire node if nothing was read.
                                    if (toMove > 0) streamPosition = stream.Position += toMove;
                                    else streamPosition += length;

                                    continue;
                                }
                        }
                    }
                }

                TimeSpan calculatedDuration = TimeSpan.FromSeconds(trackDuration / (double)trackTimeScale);

                ulong sampleCount = (ulong)stSizes.Count;

                //If there are no samples defined and there are entries in the sample to time atom
                if (sampleCount is 0 && sttsEntries.Count > 0)
                {
                    //Use the count of entries from the sample to time atom as the count of samples.
                    sampleCount = (ulong)sttsEntries.Count;

                    //If there is only one entry use the value in the entry
                    if (sampleCount == 1) sampleCount = (ulong)sttsEntries[0].Item1;
                }

                //TOdo calc methods in BaseMedia class with base times etc.. (will help with writers)

                rate = mediaType == Sdp.MediaType.audio ? trackTimeScale : (double)(sampleCount / ((double)trackDuration / trackTimeScale));

                Track createdTrack = new(trakBox, name, trackId, trackCreated, trackModified, (long)sampleCount, width, height, startTime, calculatedDuration, rate, mediaType, codecIndication, channels, bitDepth, enabled);

                //Useful to support GetSample
                var dataDictionary = new Dictionary<string, object>();

                createdTrack.UserData = dataDictionary;

                dataDictionary.Add("StOffsets", stOffsets);

                dataDictionary.Add("StSizes", stSizes);

                dataDictionary.Add("SttsEntries", sttsEntries);

                //Useful in GetSample

                dataDictionary.Add("Timescale", trackTimeScale);

                dataDictionary.Add("SampleIndex", 0);

                createdTrack.Volume = volume;

                tracks.Add(createdTrack);

                yield return createdTrack;
            }

            m_Tracks = tracks;

            Position = position;
        }

        public override Common.SegmentStream GetSample(Track track, out TimeSpan sampleDuration)
        {
            if (track is null)
                throw new ArgumentNullException(nameof(track));

            if (track.SampleCount is 0)
            {
                sampleDuration = TimeSpan.Zero;
                return null;
            }

            var dataDictionary = track.UserData as Dictionary<string, object>;

            // Get the sample sizes and offsets for the track
            IList<int> sampleSizes = dataDictionary["StSizes"] as List<int>;
            IList<long> sampleOffsets = dataDictionary["StOffsets"] as List<long>;
            IList<Tuple<long, long>> sttsEntries = dataDictionary["SttsEntries"] as List<Tuple<long, long>>;

            // Find the sample at the current position
            int sampleIndex = 0;
            long totalSamples = 0;
            long timescale = 0;
            long currentTimescale = 0;

            //This could be a bottleneck, should probably add SampleIndex to dictionary or keep on track.
            foreach (var entry in sttsEntries)
            {
                totalSamples += entry.Item1;
                timescale = entry.Item2;
                double entryDurationInSeconds = 1.0 * entry.Item2 / timescale;

                if (TimeSpan.FromSeconds(entryDurationInSeconds * totalSamples) > track.Position)
                {
                    currentTimescale = timescale;
                    break;
                }

                sampleIndex++;
            }

            if (currentTimescale is 0)
            {
                // Track has reached the end, set duration to zero and return null
                sampleDuration = TimeSpan.Zero;
                return null;
            }

            // Calculate the sample duration based on the timescale
            sampleDuration = TimeSpan.FromSeconds(1.0 * currentTimescale / timescale);

            // Get the sample data based on the sample size and position
            int sampleSize = sampleIndex >= sampleSizes.Count ? sampleSizes[0] : sampleSizes[sampleIndex];
            long position = sampleIndex > 0 ? sampleOffsets[sampleIndex] : 0;

            // Seek to the sample data position in the input stream
            Seek(position, SeekOrigin.Begin);

            // Read the sample data into a byte array
            byte[] sampleData = new byte[sampleSize];
            Read(sampleData, 0, sampleSize);

            // Advance the track position to the end of the current sample
            track.Position += sampleDuration;

            //Add the sample to the dataStream (could just return this one segment in a stream)
            track.DataStream.AddMemory(new Common.MemorySegment(sampleData));

            // Return the SegmentStream to the sample data which is contained within
            return track.DataStream;
        }
    }
}
