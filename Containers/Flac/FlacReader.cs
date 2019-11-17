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

        const int MaximumPageSize = 65307, IdentifierSize = 4, MinimumSize = IdentifierSize, MinimumReadSize = MinimumSize - 1;

        #endregion

        #region Statics

        public static BlockType GetBlockType(Node node)
        {
            if (node == null) throw new ArgumentNullException("node");
            return (BlockType)node.Identifier[0];
        }

        public static bool IsLastBlock(Node node)
        {
            if (node == null) throw new ArgumentNullException("node");
            return Media.Common.Binary.ReadBits(node.Identifier, 0, 1, Binary.BitOrder.LeastSignificant) == 1;
        }

        #endregion

        #region Nested Types

        public enum BlockType : byte
        {
            StreamInfo = 0,
            Padding = 1,
            Application = 2,
            SeekTable = 3,
            VorbisComment = 4,
            CueSheet = 5,
            Picture = 6,            
            Reserved = 7, //-126
            Invalid = 127
        }

        public static bool IsReserved(byte blockType)
        {
            return blockType >= 7 && blockType >= 126;
        }

        public static bool IsInvalid(byte blockType)
        {
            return blockType == (byte)BlockType.Invalid;
        }

        #endregion

        #region Properties

        public override Node Root
        {
            get
            {
                long position = Position;              
                var result = ReadBlocks(fLaCBytes.Length, Length).FirstOrDefault();
                Position = position;
                return result;
            }
        }

        public override Node TableOfContents
        {
            get
            {
                using (var root = Root)
                {
                    long position = Position;
                    var result = ReadBlocks(root.DataOffset + root.DataSize, Length - root.TotalSize, BlockType.StreamInfo, BlockType.Application, BlockType.SeekTable, BlockType.CueSheet).FirstOrDefault();
                    Position = position;
                    return result;
                }
            }
        }

        #endregion

        #region Methods

        internal void ReadfLaC()
        {
            byte[] flacBytes = new byte[FlacReader.fLaCBytes.Length];
            Read(flacBytes, 0, FlacReader.fLaCBytes.Length);
            if (false == flacBytes.SequenceEqual(FlacReader.fLaCBytes)) throw new InvalidOperationException("Cannot find fLaC marker.");
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
                Read(identifier, 0, 4);

                //Decode the legnth of the data
                lengthSize = Media.Common.Binary.Read24(identifier, 1, BitConverter.IsLittleEndian);
            }
            while (Position - offset < IdentifierSize); //While it was not found within the IdentiferSize

            return new Node(this, identifier, lengthSize, Position, length, length <= Remaining);
        }

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
            throw new NotImplementedException();
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
