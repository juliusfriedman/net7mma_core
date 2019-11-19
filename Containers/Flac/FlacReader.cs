using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Media.Common;
using Media.Container;

//FLAC is a codec not a container.
//https://github.com/filoe/cscore/blob/master/CSCore/Codecs/FLAC/FlacFrameHeader.cs
namespace Media.Containers.Flac
{
    public class FlacReader : Media.Container.MediaFileStream
    {

        #region Constants

        static byte[] VorbisBytes = System.Text.Encoding.UTF8.GetBytes("vorbis");
        static byte[] fLaCBytes = System.Text.Encoding.ASCII.GetBytes("fLaC");
        public static readonly int[] SampleRateTable =
        {
            -1, 88200, 176400, 192000,
            8000, 16000, 22050, 24000,
            32000, 44100, 48000, 96000,
            -1, -1, -1, -1
        };

        public static readonly int[] BitPerSampleTable =
        {
            -1, 8, 12, -1,
            16, 20, 24, -1
        };

        public static readonly int[] FlacBlockSizes =
        {
            0, 192, 576, 1152,
            2304, 4608, 0, 0,
            256, 512, 1024, 2048,
            4096, 8192, 16384
        };

        public const int FrameHeaderSize = 16;
        const int MaximumPageSize = 65307, IdentifierSize = 4, MinimumSize = IdentifierSize, MinimumReadSize = IdentifierSize;

        #endregion

        #region Nested Types        

        /// <summary>
        /// Describes the types of blocks; <see href="https://xiph.org/flac/format.html#frame_header_notes">Frame Header Notes</see>
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

        /// <summary>
        /// Defines the blocking strategy of the a flac frame.
        /// </summary>
        public enum BlockingStrategy
        {
            /// <summary>
            /// The <see cref="FlacFrameHeader.BlockSize"/> of flac frames is variable.
            /// </summary>
            VariableBlockSize,
            /// <summary>
            /// Each flac frame uses the same <see cref="FlacFrameHeader.BlockSize"/>.
            /// </summary>
            FixedBlockSize
        }

        /// <summary>
        /// Defines the channel assignments.
        /// </summary>
        public enum ChannelAssignment
        {
            /// <summary>
            /// Independent assignment. 
            /// </summary>
            Independent = 0,
            /// <summary>
            /// Left/side stereo. Channel 0 becomes the left channel while channel 1 becomes the side channel.
            /// </summary>
            LeftSide = 1,
            /// <summary>
            /// Right/side stereo. Channel 0 becomes the right channel while channel 1 becomes the side channel.
            /// </summary>
            RightSide = 2,
            /// <summary>
            /// Mid/side stereo. Channel 0 becomes the mid channel while channel 1 becomes the side channel. 
            /// </summary>
            MidSide = 3,
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

        #region utf8

        internal static bool ReadUTF8_64Signed(System.IO.Stream stream, byte[] buffer, ref int offset, out long result)
        {
            ulong r;
            var returnValue = ReadUTF8_64(stream, buffer, ref offset, out r);
            result = (long)r;
            return returnValue;
        }

        internal static bool ReadUTF8_64(System.IO.Stream stream, byte[] buffer, ref int offset, out ulong result)
        {
            //Should be ReadBits(8);
            uint x = (uint)(buffer[offset++] = (byte)stream.ReadByte());
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
                x = (buffer[offset++] = (byte)stream.ReadByte());
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

        internal static bool ReadUTF8_32Signed(System.IO.Stream stream, byte[] buffer, ref int offset, out int result)
        {
            uint r;
            var returnValue = ReadUTF8_32(stream, buffer, ref offset, out r);
            result = (int)r;
            return returnValue;
        }

        internal static bool ReadUTF8_32(System.IO.Stream stream, byte[] buffer, ref int offset, out uint result)
        {
            uint v, x;
            int i;
            //Should be ReadBits(8);
            x = (uint)(buffer[offset++] = (byte)stream.ReadByte());
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
                x = (uint)(buffer[offset++] = (byte)stream.ReadByte());
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

        public static bool IsReservedBlock(Node node)
        {
            if (node == null) throw new ArgumentNullException("node");
            return IsReservedBlock(GetBlockType(ref node.Identifier[0]));
        }

        public static bool IsInvalid(Node node)
        {
            if (node == null) throw new ArgumentNullException("node");
            return IsInvalid(ref node.Identifier[0]);
        }

        public static bool IsInvalid(ref byte blockType) { return IsInvalid(GetBlockType(ref blockType)); }

        public static bool IsLastBlock(ref byte blockType) { return (blockType & 0x80) != 0; }


        public static bool IsReservedBlock(BlockType blockType)
        {
            byte byteLockType = (byte)blockType;
            return byteLockType >= 7 && byteLockType <= 126;
        }

        public static bool IsInvalid(BlockType blockType) { return blockType == BlockType.Invalid; }

        public static BlockType GetBlockType(ref byte blockType) { return (BlockType)(blockType & 0x7f); }

        public static bool IsFrameHeader(byte[] identifier)
        {
            return Media.Common.Binary.ReadBitsMSB(identifier, 0, 14) == 0b11111111111110;
        }

        #region Frame Methods

        public static int GetBlockSize(Node node)
        {
            if (node == null) throw new ArgumentNullException("node");
            return GetBlockSize(node.Identifier);
        }

        public static int GetBlockSize(byte[] identifier)
        {
            #region blocksize

            //blocksize
            int val = identifier[2] >> 4, blocksize = -1;

            if (val == 0)
            {
                throw new InvalidOperationException("Invalid Blocksize value: 0");
            }
            if (val == 1)
                blocksize = 192;
            else if (val >= 2 && val <= 5)
                blocksize = 576 << (val - 2);
            else if (val == 6 || val == 7)
                blocksize = val;
            else if (val >= 8 && val <= 15)
                blocksize = 256 << (val - 8);
            else
            {
                throw new InvalidOperationException("Invalid Blocksize value: " + val);
            }

            return blocksize;

            #endregion blocksize
        }

        public static int GetSampleRate(Node node)
        {
            if (node == null) throw new ArgumentNullException("node");
            return GetSampleRate(node.Identifier);
        }

        public static int GetSampleRate(byte[] identifier)
        {
            #region samplerate

            //samplerate
            int sampleRate = identifier[2] & 0x0F;
            if (sampleRate >= 1 && sampleRate <= 11)
                sampleRate = SampleRateTable[sampleRate];           
            else
            {
                throw new InvalidOperationException("Invalid SampleRate value: " + sampleRate);
            }
            return sampleRate;
            #endregion samplerate
        }

        public static int GetChannels(Node node, out ChannelAssignment channelAssignment)
        {
            if (node == null) throw new ArgumentNullException("node");
            return GetChannels(node.Identifier, out channelAssignment);
        }

        public static int GetChannels(byte[] identifier, out ChannelAssignment channelAssignment)
        {
            #region channels

            int val = identifier[3] >> 4; //cc: unsigned
            int channels;
            if ((val & 8) != 0)
            {
                channels = 2;
                if ((val & 7) > 2 || (val & 7) < 0)
                {
                    throw new InvalidOperationException("Invalid ChannelAssignment");
                    //return false;
                }
                channelAssignment = (ChannelAssignment)((val & 7) + 1);
            }
            else
            {
                channels = val + 1;
                channelAssignment = ChannelAssignment.Independent;
            }
            return channels;
            #endregion channels
        }

        /// <summary>
        /// A value indicating the bits per sample, a value of 0 indicates to see the <see cref="Root"/> for more information.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public static int GetBitsPerSample(Node node) {
            if (node == null) throw new ArgumentNullException("node");
            return GetBitsPerSample(node.Identifier);
        }

        public static int GetBitsPerSample(byte[] identififer)
        {
            #region bitspersample

            int bitsPerSample = (identififer[3] & 0x0E) >> 1;
            if (bitsPerSample == 3 || bitsPerSample >= 7 || bitsPerSample < 0)
            {
                throw new InvalidOperationException("Invalid BitsPerSampleIndex");
            }
            else
                bitsPerSample = BitPerSampleTable[bitsPerSample];
            return bitsPerSample;
            #endregion bitspersample
        }

        #endregion

        #endregion

        #region Fields

        /// <summary>
        /// Where the <see cref="fLaCBytes"/> were found in the stream.
        /// </summary>
        long m_FlacPosition = -1;

        List<Track> m_Tracks = null;

        int? m_MinBlockSize = null, m_MaxBlockSize = null;

        ulong? m_MinFrameSize = null, m_MaxFrameSize = null,
        m_SampleRate = null, m_Channels = null, m_BitsPerSample = null, m_TotalSamples = null;

        string m_Md5 = string.Empty, m_Title = string.Empty;
        #endregion

        #region Properties

        public int MinBlockSize
        {
            get
            {
                if (m_MinBlockSize.HasValue) return (int)m_MinBlockSize.Value;
                ParseStreamInfo(Root);
                return (int)m_MinBlockSize.GetValueOrDefault();
            }
        }
        public int MaxBlockSize
        {
            get
            {
                if (m_MaxBlockSize.HasValue) return (int)m_MaxBlockSize.Value;
                ParseStreamInfo(Root);
                return (int)m_MaxBlockSize.GetValueOrDefault();
            }
        }

        public int MinFrameSize
        {
            get
            {
                if (m_MinFrameSize.HasValue) return (int)m_MinFrameSize.Value;
                ParseStreamInfo(Root);
                return (int)m_MinFrameSize.GetValueOrDefault();
            }
        }
        
        public int MaxFrameSize
        {
            get
            {
                if (m_MaxFrameSize.HasValue) return (int)m_MaxFrameSize.Value;
                ParseStreamInfo();
                return (int)m_MaxFrameSize.GetValueOrDefault();
            }
        }

        public DateTime Created
        {
            get
            {
                return FileInfo.CreationTimeUtc;
            }
        }

        public DateTime Modified
        {
            get
            {
                return FileInfo.LastWriteTimeUtc;
            }
        }

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

            int lengthSize = IdentifierSize;

            do
            {
                //Read the meta block header from the stream
                Read(identifier, 0, IdentifierSize);

                //Maybe a frame check for syncword 11111111111110
                if (IsFrameHeader(identifier))
                {
                    //CrC
                    lengthSize++;

                    if ((identifier[3] & 0x01) != 0) // reserved bit -> 0
                    {
                        throw new Exception("Invalid FlacFrame. Reservedbit_1 is 1");
                    }

                    int val = 0, pos = IdentifierSize - 1;

                    int blocksize = GetBlockSize(identifier);

                    int sampleRate = GetSampleRate(identifier);

                    //long position = ReadU

                    #region FrameHeader

                    lengthSize += blocksize == 7 ? 2 : 1;

                    lengthSize += sampleRate != 12 ? 2 : 1;

                    //variable blocksize
                    if ((identifier[1] & 0x01) != 0 ||
                        m_MinBlockSize != m_MaxBlockSize)
                    {
                        lengthSize += 8;
                        Array.Resize(ref identifier, lengthSize + 1);
                        ulong samplenumber;
                        if (false == ReadUTF8_64(this, identifier, ref pos, out samplenumber) && samplenumber != ulong.MaxValue)
                        {
                            //BlockingStrategy = BlockingStrategy.VariableBlockSize;
                            //SampleNumber = (long)samplenumber;
                            throw new Exception("Invalid UTF8 Samplenumber coding.");
                            
                        }
                    }
                    else //fixed blocksize
                    {
                        lengthSize += 4;
                        Array.Resize(ref identifier, lengthSize + 1);
                        uint framenumber;
                        if (false == ReadUTF8_32(this, identifier, ref pos, out framenumber) && framenumber != uint.MaxValue)
                        {
                            throw new Exception("Invalid UTF8 Framenumber coding.");
                            //BlockingStrategy = BlockingStrategy.FixedBlockSize;
                            //FrameNumber = (int)framenumber;
                        }
                    }

                    //blocksize am ende des frameheaders
                    if (blocksize != 0)
                    {
                        val = (identifier[++pos] = (byte)ReadByte());
                        if (blocksize == 7)
                        {
                            val = (val << 8) | (identifier[++pos] = (byte)ReadByte());
                        }
                        blocksize = val + 1;
                    }

                    //samplerate
                    if (sampleRate != 0)
                    {
                        val = (identifier[++pos] = (byte)ReadByte());
                        identifier[++pos] = (byte)val;
                        if (sampleRate != 12)
                        {
                            val = (val << 8) | (identifier[++pos] = (byte)ReadByte());
                        }
                        if (sampleRate == 12)
                            sampleRate = val * 1000;
                        else if (sampleRate == 13)
                            sampleRate = val;
                        else
                            sampleRate = val * 10;
                    }

                    #endregion read hints

                    //CRC
                    identifier[pos++] = (byte)ReadByte();

                    //FrameFooter
                    length = blocksize + 2;
                }
                else
                {
                    //Decode the legnth of the data and the frame footer
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
        internal protected void ParseStreamInfo(Container.Node node = null)
        {
            node = node ?? Root;
            VerifyBlockType(node, BlockType.StreamInfo);
            //METADATA_BLOCK_STREAMINFO
            using (System.IO.BinaryReader bi = new System.IO.BinaryReader(node.DataStream))
            {
                using (BitReader br = new BitReader(bi.BaseStream, Binary.BitOrder.MostSignificant, IdentifierSize * 2, true))
                {
                    m_MinBlockSize = bi.ReadInt16();
                    m_MaxBlockSize = bi.ReadInt16();
                    //Should verify there are 4 more bytes in the stream
                    m_MinFrameSize = br.ReadBits(24);
                    m_MaxFrameSize = br.ReadBits(24);
                    m_SampleRate = br.ReadBits(20);
                    m_Channels = 1 + br.ReadBits(3);
                    m_BitsPerSample = 1 + br.ReadBits(5);
                    m_TotalSamples = br.ReadBits(36);
                    m_Md5 = new string(bi.ReadChars(16));
                }
            }
        }

        /// <summary>
        /// Parses a <see cref="BlockType.VorbisComment"/> <see cref="Media.Container.Node"/>
        /// </summary>
        /// <param name="node">The <see cref="Media.Container.Node"/> to parse</param>
        internal protected List<KeyValuePair<string, string>> ParseVorbisComment(Media.Container.Node node)
        {
            VerifyBlockType(node, BlockType.VorbisComment);

            List<KeyValuePair<string, string>> results = new List<KeyValuePair<string, string>>();

            int offset = 0;
            //https://www.xiph.org/vorbis/doc/v-comment.html
            //Read Vendor Length
            int vendorLength = Common.Binary.Read32(node.Data, offset, Media.Common.Binary.IsBigEndian);

            offset += 4;

            offset += vendorLength;

            //Determine if there is a comment list
            if (vendorLength > 0 && offset + 4 < node.DataSize)
            {
                //Read User Comment List
                int userCommentListLength = Common.Binary.Read32(node.Data, offset, Media.Common.Binary.IsBigEndian);

                //Move the offset
                offset += 4;

                //Read User Comment List if available
                if (userCommentListLength > 0)
                {
                    //While there is data to consume
                    while (offset + 4 < node.DataSize)
                    {

                        //Read the item length
                        int itemLength = Common.Binary.Read32(node.Data, offset, Media.Common.Binary.IsBigEndian);

                        //Move the offset
                        offset += 4;

                        //Invalid entry.
                        if (itemLength < 0 || itemLength + offset > node.DataSize) continue;

                        //Get the string
                        string item = System.Text.Encoding.UTF8.GetString(node.Data, offset, itemLength);

                        //Split it
                        string[] parts = item.Split((char)Common.ASCII.EqualsSign);

                        //If there are 2 parts decide what to do.
                        if (parts.Length > 1)
                        {
                            //Add the key and value
                            results.Add(new KeyValuePair<string, string>(parts[0], parts[1]));

                            switch (parts[0].ToLowerInvariant())
                            {
                                //case "date": timereference "158760000"
                                //    {
                                //        //2016-04-12
                                //        break;
                                //    }
                                case "title":
                                    {
                                        m_Title = parts[1];
                                        break;
                                    }
                                default: break;
                            }
                        }

                        //Move the offset
                        offset += itemLength;
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Parses a <see cref="BlockType.SeekTable"/> <see cref="Media.Container.Node"/>
        /// </summary>
        /// <param name="node">The <see cref="Media.Container.Node"/> to parse</param>
        internal protected List<Tuple<long, long, short>> ParseSeekTable(Media.Container.Node node)
        {
            VerifyBlockType(node, BlockType.SeekTable);
            ///	NOTE
            /// The number of seek points is implied by the metadata header 'length' field, i.e.equal to length / 18.
            List <Tuple<long, long, short>> seekPoints = new List<Tuple<long, long, short>>((int)(node == null ? 0 : node.DataSize / 18));
            if (node != null) for (int i = 0, e = (int)node.DataSize; i < e; i+=12)
            {
                //Add the decoded seekpoint
                seekPoints.Add(new Tuple<long, long, short>(Common.Binary.Read64(node.Data, i, BitConverter.IsLittleEndian),
                    Common.Binary.Read64(node.Data, i + 8, BitConverter.IsLittleEndian), 
                    Common.Binary.Read16(node.Data, i + 10, BitConverter.IsLittleEndian)));
            }
            return seekPoints;
        }

        internal protected uint ReadUnary()
        {
            uint result = 0;
            using(Common.BitReader br = new BitReader(this, Binary.BitOrder.MostSignificant, 32, true))
            {
                uint unaryindicator = (uint)(br.Peek24() >> 24);

                while (unaryindicator == 0)
                {
                    ReadByte();
                    result += 8;
                    unaryindicator = (uint)(br.Peek24() >> 24);
                }

                result += UnaryTable[unaryindicator];
                br.SeekBits((int)(result & 7) + 1);
                return result;
            }
        }

        internal protected int ReadUnarySigned()
        {
            var value = ReadUnary();
            return (int)(value >> 1 ^ -((int)(value & 1)));
        }

       

        //Read method for frames and then indexer to use method. Apply from GetSample

        #endregion

        #region Overloads

        public override string ToTextualConvention(Node block)
        {
            if (block.Master.Equals(this))
            {
                if (IsFrameHeader(block.Identifier)) return "FrameHeader";
                return GetBlockType(block).ToString();
            }

            return base.ToTextualConvention(block);
        }

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

            m_Tracks = new List<Track>();

            //Loop for K StreamInfo blocks and the single VorbisComment.
            foreach(var block in ReadBlocks(0, Length, BlockType.StreamInfo, BlockType.VorbisComment))
            {

                List<KeyValuePair<string, string>> vorbisInfo;

                //Determine the action based on the BlockType of the Node returned.
                switch (GetBlockType(block))
                {
                    //One per file
                    case BlockType.VorbisComment:
                        {
                            vorbisInfo = ParseVorbisComment(block);

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

                            //DateTime.Parse(vorbisInfo["date"]);

                            lastTrack = new Track(block, m_Title, m_Tracks.Count, DateTime.Now, DateTime.Now, (long)m_TotalSamples, 0, 0, TimeSpan.Zero, TimeSpan.Zero, (long)m_SampleRate, Media.Sdp.MediaType.audio, fLaCBytes, 0, (byte)m_BitsPerSample, true);

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
