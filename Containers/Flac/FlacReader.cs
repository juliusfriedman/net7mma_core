using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Media.Common;
using Media.Container;

namespace Container.Flac
{
    public class FlacReader : Media.Container.MediaFileStream
    {

        #region Constants

        static byte[] VorbisBytes = System.Text.Encoding.UTF8.GetBytes("vorbis");
        static byte[] fLaCBytes = System.Text.Encoding.ASCII.GetBytes("fLaC");

        const int MaximumPageSize = 65307, IdentifierSize = 4, MinimumSize = IdentifierSize, MinimumReadSize = IdentifierSize;

        #endregion

        #region Statics

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

        #endregion

        #region Nested Types

        //https://xiph.org/flac/format.html#frame_header_notes see also

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

        public static bool IsReserved(BlockType blockType)
        {
            byte byteLockType = (byte)blockType;
            return byteLockType >= 7 && byteLockType <= 126;
        }

        public static bool IsInvalid(BlockType blockType) { return blockType == BlockType.Invalid; }

        public static bool IsInvalid(ref byte blockType) { return IsInvalid(GetBlockType(ref blockType)); }

        public static bool IsLastBlock(ref byte blockType)
        {
            return (blockType & 0x80) != 0;
        }

        public static BlockType GetBlockType(ref byte blockType)
        {
            return (BlockType)(blockType & 0x7f);
        }

        #endregion

        List<Track> m_Tracks;

        #region Properties

        /// <summary>
        /// Gets the <see cref="Media.Container.Node"/> which represents the first block after the fLaC marker in the stream and contains the mandatatory <see cref="BlockType.StreamInfo"/>.
        /// </summary>
        public override Node Root
        {
            get
            {
                long position = Position;
                Media.Container.Node result = ReadBlocks(0, Length).FirstOrDefault();
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
                    var result = ReadBlocks(root.DataOffset + root.DataSize, Length - root.TotalSize, BlockType.SeekTable).FirstOrDefault();
                    Position = position;
                    return result;
                }
            }
        }

        #endregion

        #region Methods

        long m_FlacPosition = -1;
        internal void ReadfLaC()
        {
            if (m_FlacPosition > 0) return;
            while (Position < Length)
            {
                Loop:
                    for (int i = 0; i < fLaCBytes.Length; ++i)
                        if (ReadByte() != fLaCBytes[i]) goto Loop;
                m_FlacPosition = Position;
                return;
                
            }
            throw new InvalidOperationException("Cannot find fLaC marker.");
        }

        public IEnumerable<Node> ReadBlocks(long offset, long count, params BlockType[] types)
        {
            long position = Position;

            Position = offset;

            foreach (var block in this)
            {
                //Get the BlockType from the header
                BlockType found = GetBlockType(block);

                //Determine if we can filter by the BlockType
                if (types != null && false == types.Contains(found)) continue;

                //If contained the found or the unmasked found then return the page
                yield return block;
                
                count -= block.TotalSize;

                if (count <= 0) break;

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

            //Allocate 27 bytes
            byte[] identifier = new byte[IdentifierSize];

            int lengthSize = 0;

            do
            {
                //Read the meta block header from the stream
                Read(identifier, 0, IdentifierSize);

                //Decode the legnth of the data
                lengthSize = Media.Common.Binary.Read24(identifier, 1, BitConverter.IsLittleEndian);

                //Maybe a frame check for syncword 11111111111110

                //if (Media.Common.Binary.ReadBitsMSB(identifier, 0, 14) == 0b11111111111110)
                //{
                //    //Handle variable size...
                //}
            }
            while (Position - offset < IdentifierSize);// && false == IsLastBlock(ref identifier[0])); //While it was not found within the IdentiferSize and is not the last block

            return new Node(this, identifier, lengthSize, Position, length, length <= Remaining);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="node"></param>
        protected void ParseStreamInfo(Media.Container.Node node)
        {
            if (node == null) throw new ArgumentNullException("node");
            BlockType blockType = GetBlockType(node);
            if (blockType != BlockType.StreamInfo) throw new InvalidOperationException("GetBlockType must indicate StreamInfo to parse.");
            //METADATA_BLOCK_STREAMINFO
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="node"></param>
        protected void ParseVorbisComment(Media.Container.Node node)
        {
            if (node == null) throw new ArgumentNullException("node");
            BlockType blockType = GetBlockType(node);
            if (blockType != BlockType.VorbisComment) throw new InvalidOperationException("GetBlockType must indicate VorbisComment to parse.");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="node"></param>
        protected void ParseSeekTable(Media.Container.Node node)
        {
            if (node == null) throw new ArgumentNullException("node");
            BlockType blockType = GetBlockType(node);
            if (blockType != BlockType.SeekTable) throw new InvalidOperationException("GetBlockType must indicate SeekTable to parse.");
        }

        //Read method for frames and then indexer to use method.

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

            foreach(var block in ReadBlocks(0, Length, BlockType.StreamInfo, BlockType.VorbisComment))
            {
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
