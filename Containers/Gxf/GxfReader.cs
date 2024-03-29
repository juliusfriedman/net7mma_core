﻿using Media.Container;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Media.Containers.Gxf
{
    //Namespace SMPTE ?
    // SMPTE 360M-2001: General Exchange Format (GXF)
    //https://tech.ebu.ch/docs/techreview/trev_291-edge.pdf
    //https://wiki.multimedia.cx/index.php?title=GXF
    public class GxfReader : MediaFileStream, IMediaContainer
    {
        public enum Identifier : byte
        {
            Unknown = 0,
            //Usually 
            //6) 4 bytes 0, 1, Code
            //4) Length
            //6) 4 bytes 0, e1, e2
            PacketStartA = 0xe1,
            PacketStartB = 0xe2,
            //public enum PacketType
            //{
            Map = 0xbc,
            Media = 0xbf,
            EndOfStream = 0xfb,
            Flt = 0xfc,
            Umf = 0xfd,
            //}

            //public enum Tag
            //{
            TagName = 0x40,
            FirstField = 0x41,
            LastField = 0x42,
            MarkIn = 0x43,
            MarkOut = 0x44,
            Size = 0x45,
            //}

            //public enum TrackTag
            //{
            TrackName = 0x4c,
            Aux = 0x4d,
            Version = 0x4e,
            MPEGAux = 0x4f,
            FramesPerSeconds = 0x50,
            TrackLines = 0x51,
            FPF = 0x52
            //}

        }

        #region Constants

        private const int IdentifierParts = 6, IdentiferSize = IdentifierParts * 2, LengthSize = 4, MinimumSize = IdentiferSize + LengthSize, VersionMajor = 0, VersionMinor = 1;

        #endregion

        #region Statics

        public static string ToTextualConvention(byte[] identifier)
        {
            return identifier is null ? Media.Common.Extensions.String.StringExtensions.UnknownString : ((Identifier)identifier[0]).ToString();
        }

        #endregion

        public GxfReader(string filename, System.IO.FileAccess access = System.IO.FileAccess.Read) : base(filename, access) { }

        public GxfReader(Uri source, System.IO.FileAccess access = System.IO.FileAccess.Read) : base(source, access) { }

        public GxfReader(System.IO.FileStream source, System.IO.FileAccess access = System.IO.FileAccess.Read) : base(source, access) { }

        public IEnumerable<Node> ReadElements(long offset, long count, params Identifier[] identifiers)
        {
            long position = Position;

            Position = offset;

            foreach (var element in this)
            {
                if (identifiers is null || identifiers.Count() is 0 || identifiers.Contains((Identifier)element.Identifier[0])) yield return element;

                count -= element.DataSize;

                if (element is null || count <= 0) break;
            }

            Position = position;
        }

        public Node ReadElement(Identifier identifier, long offset, long count)
        {
            long position = Position;
            Node result = ReadElements(offset, count, identifier).FirstOrDefault();
            Position = position;
            return result;
        }

        public Node ReadNext()
        {
            //4 bytes all 0

            //byte 1

            //type byte

            byte[] identifier = new byte[IdentiferSize];
            Read(identifier, 0, IdentifierParts);

            if (Common.Binary.ReadU32(identifier, 0, Common.Binary.IsLittleEndian) > VersionMajor
                ||
                identifier[5] != VersionMinor) throw new InvalidOperationException("Invalid Packet Header");

            //len 32
            byte[] lengthBytes = new byte[LengthSize];
            Read(lengthBytes, 0, LengthSize);

            long length = Common.Binary.ReadU32(lengthBytes, 0, Common.Binary.IsLittleEndian);

            //4 byte all 0

            //0xe1

            //0xe2

            Read(identifier, IdentifierParts, IdentifierParts);

            //Length includes identifier size and length
            length -= MinimumSize;

            if (length >> 24 > 0 || length < MinimumSize) length = 0;

            return Common.Binary.ReadU32(identifier, IdentifierParts, Common.Binary.IsLittleEndian) > VersionMajor
                ||
                identifier[IdentifierParts + 1] != (byte)Identifier.PacketStartA && identifier[IdentifierParts + 2] != (byte)Identifier.PacketStartB
                ? throw new InvalidOperationException("Invalid Packet Header")
                : new Node(this, identifier, LengthSize, Position, length, length <= Remaining);
        }
        public override IEnumerator<Node> GetEnumerator()
        {
            while (Remaining >= MinimumSize)
            {
                Node next = ReadNext();

                if (next is null) yield break;

                yield return next;

                Skip(next.DataSize);
            }
        }

        //Parse Map, Material Packet

        private List<Track> m_Tracks;

        public override IEnumerable<Track> GetTracks()
        {

            if (m_Tracks is not null)
            {
                foreach (Track track in m_Tracks) yield return track;
                yield break;
            }

            var tracks = new List<Track>();

            long position = Position;

            using (var root = Root)
            {
                //Parse tags
                //
                switch (root.Identifier[0])
                {
                    default: break;
                }

                Track created = null;

                yield return created;

                tracks.Add(created);
            }

            Position = position;

            m_Tracks = tracks;

            throw new NotImplementedException();
        }

        public override Common.SegmentStream GetSample(Track track, out TimeSpan duration)
        {
            throw new NotImplementedException();
        }

        public override Node Root
        {
            get { return ReadElement(Identifier.Map, 0, MinimumSize); }
        }

        public override Node TableOfContents
        {
            get { return Root; }
        }

    }
}
