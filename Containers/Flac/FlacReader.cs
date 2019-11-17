using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Media.Common;
using Media.Container;

namespace Media.Containers.Flac
{
    public class FlacReader : Media.Container.MediaFileStream
    {

        #region Constants

        static byte[] VorbisBytes = System.Text.Encoding.UTF8.GetBytes("vorbis");
        static byte[] fLaCBytes = System.Text.Encoding.ASCII.GetBytes("fLaC");

        const int MaximumPageSize = 65307, IdentifierSize = 4, MinimumSize = IdentifierSize, MinimumReadSize = IdentifierSize;

        #endregion

        #region Nested Types        

        /// <summary>
        /// Describes the types of blocks; <see href="https://xiph.org/flac/format.html#frame_header_notes">See Also</see>
        /// </summary>
        public enum BlockType : byte
        {
            StreamInfo = 0,
            Padding = 1,
            Application = 2,
            SeekTable = 3,
            VorbisComment = 4,
            CueSheet = 5,
            Picture = 6,            
            //Reserved = 7, //-126
            Invalid = 127
        }


        #endregion

        #region Statics

        internal static readonly byte[] UnaryTable =
        {
            8, 7, 6, 6, 5, 5, 5, 5, 4, 4, 4, 4, 4, 4, 4, 4, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
        };

        public static void VerifyBlockType(Node node, BlockType expected)
        {
            if (node == null) throw new ArgumentNullException("node");
            BlockType found = GetBlockType(node);
            if (found != expected) throw new InvalidOperationException(string.Format("GetBlockType must indicate {0} to parse. Found {1}", expected, found));
        }

        public static BlockType GetBlockType(Node node)
        {
            if (node == null) throw new ArgumentNullException("node");
            //return (BlockType)Media.Common.Binary.ReadBitsMSB(node.Identifier, 1, 7);
            return GetBlockType(ref node.Identifier[0]);
        }

        public static bool IsLastBlock(Node node)
        {
            if (node == null) throw new ArgumentNullException("node");
            return IsLastBlock(ref node.Identifier[0]);
        }

        public static bool IsReserved(Node node)
        {
            if (node == null) throw new ArgumentNullException("node");
            return IsReserved(GetBlockType(ref node.Identifier[0]));
        }

        public static bool IsInvalid(Node node)
        {
            if (node == null) throw new ArgumentNullException("node");
            return IsInvalid(ref node.Identifier[0]);
        }

        public static bool IsInvalid(ref byte blockType) { return IsInvalid(GetBlockType(ref blockType)); }

        public static bool IsLastBlock(ref byte blockType) { return (blockType & 0x80) != 0; }


        public static bool IsReserved(BlockType blockType)
        {
            byte byteLockType = (byte)blockType;
            return byteLockType >= 7 && byteLockType <= 126;
        }

        public static bool IsInvalid(BlockType blockType) { return blockType == BlockType.Invalid; }

        public static BlockType GetBlockType(ref byte blockType) { return (BlockType)(blockType & 0x7f); }

        #endregion

        #region Fields

        /// <summary>
        /// Where the <see cref="fLaCBytes"/> were found in the stream.
        /// </summary>
        long m_FlacPosition = -1;

        readonly List<Track> m_Tracks = new List<Track>();

        #endregion

        #region Properties
        
        /// <summary>
        /// Get's the position of the bytes which identity this stream as FLAC.
        /// </summary>
        public long StreamMarkerPosition { get { ReadfLaC(); return m_FlacPosition; } }

        /// <summary>
        /// Gets the <see cref="Media.Container.Node"/> which represents the first block after the fLaC marker in the stream and contains the mandatatory <see cref="BlockType.StreamInfo"/>.
        /// </summary>
        public override Node Root
        {
            get
            {
                long position = Position;
                Media.Container.Node result = ReadBlocks(0, Length, BlockType.StreamInfo).FirstOrDefault();
                Position = position;
                return result;
            }
        }

        /// <summary>
        /// Gets the <see cref="Media.Container.Node"/> which contains the <see cref="BlockType.SeekTable"/>.
        /// </summary>
        public override Node TableOfContents
        {
            get
            {
                using (var root = Root)
                {
                    long position = Position;
                    Media.Container.Node result = ReadBlocks(root.DataOffset + root.DataSize, Length - root.TotalSize, BlockType.SeekTable).FirstOrDefault();
                    Position = position;
                    return result;
                }
            }
        }

        #endregion

        #region Methods
        
        /// <summary>
        /// Reads the "fLaC" stream marker as defined by <see cref="fLaCBytes"/> and stores the position of the occurance in <see cref="m_FlacPosition"/>.
        /// Throws <see cref="InvalidOperationException"/> when not enough bytes are present OR the "fLaC" marker cannot be found.
        /// </summary>
        internal void ReadfLaC()
        {
            if (m_FlacPosition > 0) return;
            while (Position + IdentifierSize < Length)
            {
                Loop:
                    for (int i = 0; i < fLaCBytes.Length; ++i)
                        if (ReadByte() != fLaCBytes[i]) goto Loop;
                m_FlacPosition = Position - fLaCBytes.Length;
                return;
                
            }
            throw new InvalidOperationException("Cannot find fLaC marker.");
        }

        /// <summary>
        /// Reads blocks as <see cref="Media.Container.Node"/> where the types may be filtered as requested. 
        /// Use <see cref="BlockType.Invalid"/> to Find Frames...
        /// </summary>
        /// <param name="offset">The starting position</param>
        /// <param name="count">The amount of bytes to locate the block in</param>
        /// <param name="types">The optional <see cref="BlockType"/> array which are allowed to be returned</param>
        /// <returns></returns>
        public IEnumerable<Node> ReadBlocks(long offset, long count, params BlockType[] types)
        {
            long position = Position;

            Position = offset;

            foreach (Media.Container.Node block in this) //GetEnumerator()
            {
                //Get the BlockType from the header
                BlockType found = GetBlockType(block);

                //Determine if we can filter by the BlockType
                if (types != null && false == types.Contains(found)) goto Continue;

                //If contained the found or the unmasked found then return the page
                yield return block;
                
            Continue:
                count -= block.TotalSize;

                if (count <= 0) yield break;

                continue;
            }

            Position = position;
        }      

        /// <summary>
        /// Reads the next block from the underlying.
        /// </summary>
        /// <returns>The <see cref="Media.Container.Node"/> which corresponds to the block</returns>
        public Node ReadNext()
        {
            long offset = Position, length = 0;

            if (Position <= 0) ReadfLaC();

            //Allocate 4 bytes
            byte[] identifier = new byte[IdentifierSize];

            int lengthSize = 3;

            do
            {
                //Read the meta block header from the stream
                Read(identifier, 0, IdentifierSize);

                //Maybe a frame check for syncword 11111111111110
                if (Media.Common.Binary.ReadBitsMSB(identifier, 0, 14) == 0b11111111111110)
                {
                    bool variableSize = Media.Common.Binary.ReadBitsMSB(identifier, 0, 1) == 1;
                    //FrameHeader 
                    //Handle variable size.
                    int frameHeaderSize = 0; // identifier[2] >> 4;
                    //blockSize etc..

                    //SubFrame
                    //SubFrameHeader
                    //Padding
                    //FrameFooter
                    Array.Resize(ref identifier, 5 + frameHeaderSize + 2);
                    //Read the rest of the header and CRC8 and FrameFooter CRC16
                    Read(identifier, IdentifierSize, 1 + frameHeaderSize + 2);
                }
                else
                {
                    //Decode the legnth of the data
                    length = Media.Common.Binary.Read24(identifier, 1, BitConverter.IsLittleEndian);
                }
            }
            while (Position - offset < IdentifierSize && false == IsLastBlock(ref identifier[0])); //While it was not found within the IdentiferSize and is not the last block

            return new Node(this, identifier, lengthSize, Position, length, length <= Remaining);
        }

        /// <summary>
        /// Parses a <see cref="BlockType.StreamInfo"/> <see cref="Media.Container.Node"/>
        /// </summary>
        /// <param name="node">The <see cref="Media.Container.Node"/> to parse</param>
        internal protected void ParseStreamInfo(Media.Container.Node node)
        {
            VerifyBlockType(node, BlockType.StreamInfo);
            //METADATA_BLOCK_STREAMINFO
            using(System.IO.BinaryReader br = new System.IO.BinaryReader(node.DataStream))
            {
                short MinBlockSize = br.ReadInt16();
                short MaxBlockSize = br.ReadInt16();
                using (BitReader b = new BitReader(this, IdentifierSize * 2, true))
                {
                    ulong MinFrameSize = b.ReadBits(24);
                    ulong MaxFrameSize = b.ReadBits(24);
                    ulong SampleRate = b.ReadBits(20);
                    ulong Channels = 1 + b.ReadBits(3);
                    ulong BitsPerSample = 1 + b.ReadBits(5);
                    ulong TotalSamples = b.ReadBits(36);
                    string Md5 = new string(br.ReadChars(16));
                }
            }
        }

        /// <summary>
        /// Parses a <see cref="BlockType.VorbisComment"/> <see cref="Media.Container.Node"/>
        /// </summary>
        /// <param name="node">The <see cref="Media.Container.Node"/> to parse</param>
        internal protected void ParseVorbisComment(Media.Container.Node node)
        {
            VerifyBlockType(node, BlockType.VorbisComment);
        }

        /// <summary>
        /// Parses a <see cref="BlockType.SeekTable"/> <see cref="Media.Container.Node"/>
        /// </summary>
        /// <param name="node">The <see cref="Media.Container.Node"/> to parse</param>
        internal protected void ParseSeekTable(Media.Container.Node node)
        {
            VerifyBlockType(node, BlockType.SeekTable);
        }

        //Needs a BitReader or to use the BitReader for each Frame.

        //internal protected uint ReadUnary()
        //{
        //    uint result = 0;
        //    uint unaryindicator = Cache >> 24;

        //    while (unaryindicator == 0)
        //    {
        //        ReadByte();
        //        result += 8;
        //        unaryindicator = Cache >> 24;
        //    }

        //    result += UnaryTable[unaryindicator];
        //    SeekBits((int)(result & 7) + 1);
        //    return result;
        //}

        //internal protected int ReadUnarySigned()
        //{
        //    var value = ReadUnary();
        //    return (int)(value >> 1 ^ -((int)(value & 1)));
        //}

        #region utf8

        internal protected bool ReadUTF8_64Signed(out long result)
        {
            ulong r;
            var returnValue = ReadUTF8_64(out r);
            result = (long)r;
            return returnValue;
        }

        internal protected bool ReadUTF8_64(out ulong result)
        {
            //Should be ReadBits(8);
            uint x = (uint)ReadByte();
            ulong v;
            int i;

            if ((x & 0x80) == 0)
            {
                v = x;
                i = 0;
            }
            else if ((x & 0xC0) != 0 && (x & 0x20) == 0)
            {
                v = x & 0x1F;
                i = 1;
            }
            else if ((x & 0xE0) != 0 && (x & 0x10) == 0) /* 1110xxxx */
            {
                v = x & 0x0F;
                i = 2;
            }
            else if ((x & 0xF0) != 0 && (x & 0x08) == 0) /* 11110xxx */
            {
                v = x & 0x07;
                i = 3;
            }
            else if ((x & 0xF8) != 0 && (x & 0x04) == 0) /* 111110xx */
            {
                v = x & 0x03;
                i = 4;
            }
            else if ((x & 0xFC) != 0 && (x & 0x02) == 0) /* 1111110x */
            {
                v = x & 0x01;
                i = 5;
            }
            else if ((x & 0xFE) != 0 && (x & 0x01) == 0)
            {
                v = 0;
                i = 6;
            }
            else
            {
                result = ulong.MaxValue;
                return false;
            }

            for (; i != 0; i--)
            {
                //Should be ReadBits(8);
                x = (uint)ReadByte();
                if ((x & 0xC0) != 0x80)
                {
                    result = ulong.MaxValue;
                    return false;
                }

                v <<= 6;
                v |= (x & 0x3F);
            }

            result = v;
            return true;
        }

        internal protected bool ReadUTF8_32Signed(out int result)
        {
            uint r;
            var returnValue = ReadUTF8_32(out r);
            result = (int)r;
            return returnValue;
        }

        internal protected bool ReadUTF8_32(out uint result)
        {
            uint v, x;
            int i;
            //Should be ReadBits(8);
            x = (uint)ReadByte();
            if ((x & 0x80) == 0)
            {
                v = x;
                i = 0;
            }
            else if ((x & 0xC0) != 0 && (x & 0x20) == 0)
            {
                v = x & 0x1F;
                i = 1;
            }
            else if ((x & 0xE0) != 0 && (x & 0x10) == 0) /* 1110xxxx */
            {
                v = x & 0x0F;
                i = 2;
            }
            else if ((x & 0xF0) != 0 && (x & 0x08) == 0) /* 11110xxx */
            {
                v = x & 0x07;
                i = 3;
            }
            else if ((x & 0xF8) != 0 && (x & 0x04) == 0) /* 111110xx */
            {
                v = x & 0x03;
                i = 4;
            }
            else if ((x & 0xFC) != 0 && (x & 0x02) == 0) /* 1111110x */
            {
                v = x & 0x01;
                i = 5;
            }
            else
            {
                result = uint.MaxValue;
                return false;
            }

            for (; i != 0; i--)
            {
                //Should be ReadBits(8);
                x = (uint)ReadByte();
                if ((x & 0xC0) != 0x80)
                {
                    result = uint.MaxValue;
                    return false;
                }

                v <<= 6;
                v |= (x & 0x3F);
            }

            result = v;
            return true;
        }

        #endregion utf8

        //Read method for frames and then indexer to use method. Apply from GetSample

        #endregion

        #region Overloads

        public override IEnumerator<Node> GetEnumerator()
        {
            while (Remaining >= MinimumReadSize)
            {
                Node next = ReadNext();

                if (next == null) yield break;

                yield return next;

                //Have a CheckCRC 

                //if true then crc check

                Skip(next.DataSize);
            }
        }

        public override SegmentStream GetSample(Track track, out TimeSpan duration)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<Track> GetTracks()
        {
            if (m_Tracks != null)
            {
                foreach (Track track in m_Tracks) yield return track;
                yield break;
            }

            Track lastTrack = null;

            //Loop for K StreamInfo blocks and the single VorbisComment.
            foreach(var block in ReadBlocks(0, Length, BlockType.StreamInfo, BlockType.VorbisComment))
            {
                //Determine the action based on the BlockType of the Node returned.
                switch (GetBlockType(block))
                {
                    //One per file
                    case BlockType.VorbisComment:
                        {
                            ParseVorbisComment(block);

                            continue;
                        }
                    //One per stream
                    case BlockType.StreamInfo:
                        {
                            //If there was a previous track then yield it now.
                            if (lastTrack != null)
                            {
                                m_Tracks.Add(lastTrack);

                                yield return lastTrack;
                            }

                            ParseStreamInfo(block);

                            lastTrack = new Track(block, "", 0, DateTime.Now, DateTime.Now, 0, 0, 0, TimeSpan.Zero, TimeSpan.Zero, 0, Media.Sdp.MediaType.audio, null, 0, 0, true);

                            continue;
                        }
                }                
            }

            m_Tracks.Add(lastTrack);

            yield return lastTrack;
        }

        #endregion

        #region Constructor

        public FlacReader(string filename, System.IO.FileAccess access = System.IO.FileAccess.Read) : base(filename, access) { }

        public FlacReader(Uri source, System.IO.FileAccess access = System.IO.FileAccess.Read) : base(source, access) { }

        public FlacReader(System.IO.FileStream source, System.IO.FileAccess access = System.IO.FileAccess.Read) : base(source, access) { }

        public FlacReader(Uri uri, System.IO.Stream source, int bufferSize = 8192) : base(uri, source, null, bufferSize, true) { }

        #endregion
    }
}
