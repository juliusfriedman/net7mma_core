﻿using Media.Common;
using Media.Container;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

//FLAC is a codec not a container.
//https://github.com/filoe/cscore/blob/master/CSCore/Codecs/FLAC/FlacFrameHeader.cs
namespace Media.Codecs.Flac
{
    public class FlacReader : Media.Container.MediaFileStream
    {
        //https://github.com/xVir/FLACTools/tree/master/FLACCodecWin8
        #region Constants
        /// <summary>
        /// ASCII "fLaC"
        /// </summary>
        private static readonly byte[] fLaCBytes = System.Text.Encoding.ASCII.GetBytes("fLaC");

        /// <summary>
        /// The value used to ensure frames are read correctly.
        /// </summary>
        private static readonly uint FrameSync = 0b11111111111110;

        /// <summary>
        /// Used to decode a sampleRate code
        /// </summary>
        public static readonly int[] SampleRateTable =
        {
            -1, 88200, 176400, 192000,
            8000, 16000, 22050, 24000,
            32000, 44100, 48000, 96000,
            -1, -1, -1, -1
        };

        /// <summary>
        /// Used to decode bitsPerSample
        /// </summary>
        public static readonly int[] BitPerSampleTable =
        {
            -1, 8, 12, -1,
            16, 20, 24, -1
        };

        /// <summary>
        /// Used to decode block sizes
        /// </summary>
        public static readonly int[] FlacBlockSizes =
        {
            0, 192, 576, 1152,
            2304, 4608, 0, 0,
            256, 512, 1024, 2048,
            4096, 8192, 16384
        };

        /// <summary>
        /// Used to decode bit values using the <see cref="ReadUnary"/> and <see cref="ReadUnarySigned"/>
        /// </summary>
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

        /// <summary>
        /// Used to determine how many bytes are allocated to read a <see cref=nameof(node)/>
        /// </summary>
        private const int IdentifierSize = 4,
            MinimumReadSize = IdentifierSize;

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

        #region utf8

        /// <summary>
        /// Reads UTF8 Data
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        internal static bool ReadUTF8_64Signed(System.IO.Stream stream, byte[] buffer, ref int offset, out long result)
        {
            var returnValue = ReadUTF8_64(stream, buffer, ref offset, out ulong r);
            result = (long)r;
            return returnValue;
        }

        internal static bool ReadUTF8_64(System.IO.Stream stream, byte[] buffer, ref int offset, out ulong result)
        {
            //Should be ReadBits(8);
            uint x = buffer[++offset] = (byte)stream.ReadByte();
            ulong v;
            int i;

            if ((x & 0x80) is 0)
            {
                v = x;
                i = 0;
            }
            else if ((x & 0xC0) != 0 && (x & 0x20) is 0)
            {
                v = x & 0x1F;
                i = 1;
            }
            else if ((x & 0xE0) != 0 && (x & 0x10) is 0) /* 1110xxxx */
            {
                v = x & 0x0F;
                i = 2;
            }
            else if ((x & 0xF0) != 0 && (x & 0x08) is 0) /* 11110xxx */
            {
                v = x & 0x07;
                i = 3;
            }
            else if ((x & 0xF8) != 0 && (x & 0x04) is 0) /* 111110xx */
            {
                v = x & 0x03;
                i = 4;
            }
            else if ((x & 0xFC) != 0 && (x & 0x02) is 0) /* 1111110x */
            {
                v = x & 0x01;
                i = 5;
            }
            else if ((x & 0xFE) != 0 && (x & 0x01) is 0)
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
                x = (buffer[++offset] = (byte)stream.ReadByte());
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
            var returnValue = ReadUTF8_32(stream, buffer, ref offset, out uint r);
            result = (int)r;
            return returnValue;
        }

        internal static bool ReadUTF8_32(System.IO.Stream stream, byte[] buffer, ref int offset, out uint result)
        {
            uint v = 0, x;
            int i;
            x = (buffer[++offset] = (byte)stream.ReadByte());
            if (0 == (x & 0x80))
            {
                v = x;
                i = 0;
            }
            else if (0xC0 == (x & 0xE0)) /* 110xxxxx */
            {
                v = x & 0x1F;
                i = 1;
            }
            else if (0xE0 == (x & 0xF0)) /* 1110xxxx */
            {
                v = x & 0x0F;
                i = 2;
            }
            else if (0xF0 == (x & 0xF8)) /* 11110xxx */
            {
                v = x & 0x07;
                i = 3;
            }
            else if (0xF8 == (x & 0xFC)) /* 111110xx */
            {
                v = x & 0x03;
                i = 4;
            }
            else if (0xFC == (x & 0xFE)) /* 1111110x */
            {
                v = x & 0x01;
                i = 5;
            }
            else if (0xFE == x) /* 11111110 */
            {
                v = 0;
                i = 6;
            }
            else
            {
                result = v;
                return false;
            }
            for (; i > 0; i--)
            {
                x = (buffer[++offset] = (byte)stream.ReadByte());
                if (0x80 != (x & 0xC0))  /* 10xxxxxx */
                    throw new Exception("invalid utf8 encoding");
                v <<= 6;
                v |= (x & 0x3F);
            }
            result = v;
            return true;
        }

        #endregion utf8

        public static void VerifyBlockType(Node node, BlockType expected)
        {
            if (node is null) throw new ArgumentNullException(nameof(node));
            BlockType found = GetBlockType(node);
            if (found != expected) throw new InvalidOperationException(string.Format("GetBlockType must indicate {0}. Found {1}", expected, found));
        }

        public static BlockType GetBlockType(Node node)
        {
            if (node is null) throw new ArgumentNullException(nameof(node));
            //return (BlockType)Media.Common.Binary.ReadBitsMSB(node.Identifier, 1, 7);
            return GetBlockType(ref node.Identifier[0]);
        }

        public static bool IsLastBlock(Node node)
        {
            return node is null ? throw new ArgumentNullException(nameof(node)) : IsLastBlock(ref node.Identifier[0]);
        }

        public static bool IsReservedBlock(Node node)
        {
            return node is null ? throw new ArgumentNullException(nameof(node)) : IsReservedBlock(GetBlockType(ref node.Identifier[0]));
        }

        public static bool IsInvalid(Node node)
        {
            return node is null ? throw new ArgumentNullException(nameof(node)) : IsInvalid(ref node.Identifier[0]);
        }

        public static bool IsFrameHeader(Node node)
        {
            return node is null ? throw new ArgumentNullException(nameof(node)) : IsFrameHeader(node.Identifier);
        }

        public static bool IsInvalid(ref byte blockType) { return IsInvalid(GetBlockType(ref blockType)); }

        public static bool IsLastBlock(ref byte blockType) { return (blockType & 0x80) != 0; }

        public static bool IsReservedBlock(BlockType blockType)
        {
            byte byteLockType = (byte)blockType;
            return byteLockType is >= 7 and <= 126;
        }

        public static bool IsInvalid(BlockType blockType) { return blockType == BlockType.Invalid; }

        public static BlockType GetBlockType(ref byte blockType) { return (BlockType)(blockType & 0x7f); }

        public static bool IsFrameHeader(byte[] identifier)
        {
            return identifier is null
                ? throw new ArgumentNullException(nameof(identifier))
                : identifier.Length < 2
                ? throw new ArgumentOutOfRangeException(nameof(identifier))
                : Media.Common.Binary.ReadBitsMSB(identifier, 0, 14) == FrameSync;
        }

        #region Frame Methods

        public static int GetBlockSize(Node node)
        {
            return node is null ? throw new ArgumentNullException(nameof(node)) : GetBlockSize(node.Identifier);
        }

        public static int GetBlockSize(byte[] identifier)
        {
            #region blocksize

            //blocksize
            int val = identifier[2] >> 4, blocksize = -1;

            if (val is 0)
            {
                throw new InvalidOperationException("Invalid Blocksize value: 0");
            }
            blocksize = val == 1
                ? 192
                : val is >= 2 and <= 5
                ? 576 << (val - 2)
                : val is 6 or 7
                ? val
                : val is >= 8 and <= 15 ? 256 << (val - 8) : throw new InvalidOperationException("Invalid Blocksize value: " + val);

            return blocksize;

            #endregion blocksize
        }

        public static int GetSampleRate(Node node)
        {
            return node is null ? throw new ArgumentNullException(nameof(node)) : GetSampleRate(node.Identifier);
        }

        public static int GetSampleRate(byte[] identifier)
        {
            #region samplerate

            //samplerate
            int sampleRate = identifier[2] & 0x0F;
            sampleRate = sampleRate is >= 1 and <= 11
                ? SampleRateTable[sampleRate]
                : throw new InvalidOperationException("Invalid SampleRate value: " + sampleRate);
            return sampleRate;
            #endregion samplerate
        }

        public static int GetChannels(Node node, out ChannelAssignment channelAssignment)
        {
            return node is null ? throw new ArgumentNullException(nameof(node)) : GetChannels(node.Identifier, out channelAssignment);
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
        /// <param name=nameof(node)></param>
        /// <returns></returns>
        public static int GetBitsPerSample(Node node)
        {
            return node is null ? throw new ArgumentNullException(nameof(node)) : GetBitsPerSample(node.Identifier);
        }

        public static int GetBitsPerSample(byte[] identififer)
        {
            #region bitspersample

            int bitsPerSample = (identififer[3] & 0x0E) >> 1;
            bitsPerSample = bitsPerSample is 3 or >= 7 or < 0
                ? throw new InvalidOperationException("Invalid BitsPerSampleIndex")
                : BitPerSampleTable[bitsPerSample];
            return bitsPerSample;
            #endregion bitspersample
        }

        #endregion

        #endregion

        #region Fields

        /// <summary>
        /// Where the <see cref="fLaCBytes"/> were found in the stream.
        /// </summary>
        private long m_FlacPosition = -1;

        /// <summary>
        /// Any <see cref="Track"/> instances, Typically only 1 per file / stream.
        /// </summary>
        private List<Track> m_Tracks = null;
        private int? m_MinBlockSize = 0, m_MaxBlockSize = 0,
             m_MinFrameSize = 0, m_MaxFrameSize = 0,
             m_SampleRate = 0, m_Channels = 0,
             m_BitsPerSample = 0;
        private ulong? m_TotalSamples = 0;
        private string m_Md5 = string.Empty, m_Title = string.Empty;
        #endregion

        #region Properties

        public int MinBlockSize
        {
            get
            {
                if (m_MinBlockSize.HasValue) return m_MinBlockSize.Value;
                ParseStreamInfo(Root);
                return m_MinBlockSize.GetValueOrDefault();
            }
        }
        public int MaxBlockSize
        {
            get
            {
                if (m_MaxBlockSize.HasValue) return m_MaxBlockSize.Value;
                ParseStreamInfo(Root);
                return m_MaxBlockSize.GetValueOrDefault();
            }
        }

        public int MinFrameSize
        {
            get
            {
                if (m_MinFrameSize.HasValue) return m_MinFrameSize.Value;
                ParseStreamInfo(Root);
                return m_MinFrameSize.GetValueOrDefault();
            }
        }

        public int MaxFrameSize
        {
            get
            {
                if (m_MaxFrameSize.HasValue) return m_MaxFrameSize.Value;
                ParseStreamInfo();
                return m_MaxFrameSize.GetValueOrDefault();
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
        /// Get's the position of the <see cref="fLaCBytes"/>.
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
                if (types is not null && false == types.Contains(found)) goto Continue;

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

                    length = 0;

                    if ((identifier[3] & 0x01) != 0) // reserved bit -> 0
                    {
                        throw new Exception("Invalid FlacFrame. Reservedbit_1 is 1");
                    }

                    int val = 0, pos = IdentifierSize - 1;

                    int blocksizeCode = GetBlockSize(identifier);

                    int sampleRateCode = GetSampleRate(identifier);

                    //long position = ReadU

                    #region FrameHeader

                    lengthSize += blocksizeCode == 7 ? 2 : 1;

                    lengthSize += sampleRateCode != 12 ? 2 : 1;

                    length = blocksizeCode;

                    //variable blocksize
                    if ((identifier[1] & 0x01) != 0 ||
                        m_MinBlockSize != m_MaxBlockSize)
                    {
                        lengthSize += 8;
                        Array.Resize(ref identifier, lengthSize + 1);
                        if (false == ReadUTF8_64(this, identifier, ref pos, out ulong samplenumber) && samplenumber != ulong.MaxValue)
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
                        if (false == ReadUTF8_32(this, identifier, ref pos, out uint framenumber) && framenumber != uint.MaxValue)
                        {
                            throw new Exception("Invalid UTF8 Framenumber coding.");
                            //BlockingStrategy = BlockingStrategy.FixedBlockSize;
                            //FrameNumber = (int)framenumber;
                        }
                    }

                    //blocksize am ende des frameheaders
                    if (blocksizeCode != 0)
                    {
                        val = (identifier[++pos] = (byte)ReadByte());
                        if (blocksizeCode == 7)
                        {
                            val = (val << 8) | (identifier[++pos] = (byte)ReadByte());
                        }
                        blocksizeCode = val + 1;
                        length = blocksizeCode;
                    }

                    //samplerate
                    if (sampleRateCode != 0)
                    {
                        val = (identifier[++pos] = (byte)ReadByte());
                        identifier[++pos] = (byte)val;
                        if (sampleRateCode != 12)
                        {
                            val = (val << 8) | (identifier[++pos] = (byte)ReadByte());
                        }
                        sampleRateCode = sampleRateCode == 12 ? val * 1000 : sampleRateCode == 13 ? val : val * 10;
                    }

                    #endregion read hints

                    //CRC
                    identifier[pos++] = (byte)ReadByte();

                    //FrameFooter
                    length += 2;
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
        /// <param name=nameof(node)>The <see cref="Media.Container.Node"/> to parse</param>
        protected internal void ParseStreamInfo(Container.Node node = null)
        {
            node ??= Root;
            VerifyBlockType(node, BlockType.StreamInfo);
            //METADATA_BLOCK_STREAMINFO
            using (System.IO.BinaryReader bi = new(node.DataStream))
            {
                using (BitReader br = new(bi.BaseStream, Binary.BitOrder.MostSignificant, IdentifierSize * 2, true))
                {
                    m_MinBlockSize = Common.Binary.ReadU16(node.Data, 0, BitConverter.IsLittleEndian); //br.Read16();

                    m_MaxBlockSize = Common.Binary.ReadU16(node.Data, 2, BitConverter.IsLittleEndian);

                    m_MinFrameSize = (int)Common.Binary.ReadU24(node.Data, 4, BitConverter.IsLittleEndian);

                    m_MaxFrameSize = (int)Common.Binary.ReadU24(node.Data, 7, BitConverter.IsLittleEndian);

                    m_SampleRate = (int)Common.Binary.ReadUInt64MSB(node.Data.Array, 10, 20, 0); //e.g. Common.Binary.ReadBitsMSB(node.Data, Common.Binary.BytesToBits(10), 20)

                    m_Channels = 1 + (int)Common.Binary.ReadUInt64MSB(node.Data.Array, 12, 3, 4);

                    m_BitsPerSample = 1 + (int)Common.Binary.ReadUInt64MSB(node.Data.Array, 12, 5, 7);

                    m_TotalSamples = Common.Binary.ReadUInt64MSB(node.Data.Array, 13, 36, 4);

                    m_Md5 = new string(Encoding.ASCII.GetChars(node.Data.Array, 8, 16));
                }
            }
        }

        /// <summary>
        /// Parses a <see cref="BlockType.VorbisComment"/> <see cref="Media.Container.Node"/>
        /// </summary>
        /// <param name=nameof(node)>The <see cref="Media.Container.Node"/> to parse</param>
        protected internal List<KeyValuePair<string, string>> ParseVorbisComment(Media.Container.Node node)
        {
            VerifyBlockType(node, BlockType.VorbisComment);

            List<KeyValuePair<string, string>> results = [];

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
                        string item = System.Text.Encoding.UTF8.GetString(node.Data.Array, offset, itemLength);

                        //Split it
                        string[] parts = item.Split((char)Common.ASCII.EqualsSign);

                        //If there are 2 parts decide what to do.
                        if (parts.Length > 1)
                        {
                            //Add the key and value
                            results.Add(new KeyValuePair<string, string>(parts[0], parts[1]));

                            //Handle any parts needed such as title, date or timereference..
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
                        //else
                        //{
                        //    results.Add(new KeyValuePair<string, string>(parts[0], string.Empty));
                        //}

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
        /// <param name=nameof(node)>The <see cref="Media.Container.Node"/> to parse</param>
        protected internal List<Tuple<long, long, short>> ParseSeekTable(Media.Container.Node node)
        {
            VerifyBlockType(node, BlockType.SeekTable);
            ///	NOTE
            /// The number of seek points is implied by the metadata header 'length' field, i.e.equal to length / 18.
            List<Tuple<long, long, short>> seekPoints = new((int)(node is null ? 0 : node.DataSize / 18));
            if (node is not null) for (int i = 0, e = (int)node.DataSize; i < e; i += 18)
                {
                    //Add the decoded seekpoint
                    seekPoints.Add(new Tuple<long, long, short>(Common.Binary.Read64(node.Data, i, BitConverter.IsLittleEndian),
                        Common.Binary.Read64(node.Data, i + 8, BitConverter.IsLittleEndian),
                        Common.Binary.Read16(node.Data, i + 10, BitConverter.IsLittleEndian)));
                }
            return seekPoints;
        }

        protected internal uint ReadUnary()
        {

            //int i;
            //for (i = 0; i < len && get_bits1(gb) != stop; i++) ;
            //return i;

            uint result = 0;
            using (Common.BitReader br = new(this, Binary.BitOrder.MostSignificant, 32, true))
            {
                uint unaryindicator = (uint)(br.Read24() >> 24);

                while (unaryindicator is 0)
                {
                    ReadByte();
                    result += 8;
                    unaryindicator = (uint)(br.Read24() >> 24);
                }

                result += UnaryTable[unaryindicator];
                br.SeekBits((int)(result & 7) + 1);
                return result;
            }
        }

        protected internal int ReadUnarySigned()
        {
            var value = ReadUnary();
            return (int)(value >> 1 ^ -((int)(value & 1)));
        }

        //Read method for frames and then indexer to use method. Apply from GetSample

        #endregion

        #region Overloads

        public override string ToTextualConvention(Node block)
        {
            return block.Master.Equals(this)
                ? IsFrameHeader(block.Identifier) ? "FrameHeader" : GetBlockType(block).ToString()
                : base.ToTextualConvention(block);
        }

        public override IEnumerator<Node> GetEnumerator()
        {
            while (Remaining >= MinimumReadSize)
            {
                Node next = ReadNext();

                if (next is null) yield break;

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
            if (m_Tracks is not null)
            {
                foreach (Track track in m_Tracks) yield return track;
                yield break;
            }

            Track lastTrack = null;

            m_Tracks = [];

            //Loop for K StreamInfo blocks and the single VorbisComment.
            foreach (var block in ReadBlocks(0, Length, BlockType.StreamInfo, BlockType.VorbisComment))
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
                            if (lastTrack is not null)
                            {
                                m_Tracks.Add(lastTrack);

                                yield return lastTrack;
                            }

                            ParseStreamInfo(block);

                            //DateTime.Parse(vorbisInfo["date"]);

                            lastTrack = new Track(block, m_Title, m_Tracks.Count, DateTime.Now, DateTime.Now, (long)m_TotalSamples, 0, 0, TimeSpan.Zero, TimeSpan.FromSeconds((double)((double)m_TotalSamples / m_SampleRate)), (long)m_SampleRate, Media.Sdp.MediaType.audio, fLaCBytes, (byte)m_Channels, (byte)m_BitsPerSample, true);

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
