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
using System.Linq;
using System.Text;

namespace Media.Containers.Riff;

/// <summary>
/// Represents the logic necessary to read files in the Resource Interchange File Format Format (.avi)
/// </summary>
/// <notes>
/// <see href="http://www.alexander-noe.com/video/documentation/avi.pdf">Extremely Helpful</see>
/// </notes>
public class RiffReader : MediaFileStream, IMediaContainer
{
    [Flags]
    internal enum MainHeaderFlags : uint
    {
        HasIndex = 0x00000010U,
        MustUseIndex = 0x00000020U,
        IsInterleaved = 0x00000100U,
        TrustChunkType = 0x00000800U,
        WasCaptureFile = 0x00010000U,
        Copyrighted = 0x000200000U,
    }

    //internal enum IndexType : byte
    //{
    //    Indexes = 0x00,
    //    Chunks = 0x01,
    //    Data = 0x80,
    //}

    #region Constants

    internal const int DWORDSIZE = 4, TWODWORDSSIZE = 8;
    public const int MinimumSize = TWODWORDSSIZE, IdentifierSize = DWORDSIZE, LengthSize = DWORDSIZE;

    #endregion

    #region FourCC conversion methods

    //string GetSubType?

    public static string FromFourCC(int FourCC)
    {
        char[] chars = new char[4];
        chars[0] = (char)(FourCC & 0xFF);
        chars[1] = (char)((FourCC >> 8) & 0xFF);
        chars[2] = (char)((FourCC >> 16) & 0xFF);
        chars[3] = (char)((FourCC >> 24) & 0xFF);

        return new string(chars);
    }

    public static int ToFourCC(string FourCC, int offset = 0)
    {
        if (FourCC.Length - offset < 4)
        {
            throw new Exception("FourCC strings with offset must be 4 characters long " + FourCC);
        }

        int result = FourCC[offset + 3] << 24
                    | FourCC[offset + 2] << 16
                    | FourCC[offset + 1] << 8
                    | FourCC[offset + 0];

        return result;
    }

    public static int ToFourCC(char[] FourCC, int offset = 0)
    {
        if (FourCC.Length - offset < 4)
        {
            throw new Exception("FourCC char arrays with offset must contain 4 characters" + new string(FourCC, offset, FourCC.Length - offset));
        }

        int result = FourCC[offset + 3] << 24
                    | FourCC[offset + 2] << 16
                    | FourCC[offset + 1] << 8
                    | FourCC[offset + 0];

        return result;
    }

    public static int ToFourCC(char c0, char c1, char c2, char c3)
    {
        int result = c3 << 24
                    | c2 << 16
                    | c1 << 8
                    | c0;

        return result;
    }

    public static int ToFourCC(byte c0, byte c1, byte c2, byte c3) { return ToFourCC((char)c0, (char)c1, (char)c2, (char)c3); }

    public static bool HasSubType(FourCharacterCode fourCC)
    {
        return RiffReader.ParentChunks.Contains(fourCC);
    }

    public static readonly HashSet<FourCharacterCode> ParentChunks =
    [
        FourCharacterCode.RIFF,
        FourCharacterCode.RIFX,
        FourCharacterCode.RF64,
        FourCharacterCode.ON2,
        FourCharacterCode.odml,
        FourCharacterCode.LIST,
    ];

    public static bool HasSubType(Node chunk)
    {

        if (chunk is null) throw new ArgumentNullException("chunk");

        FourCharacterCode fourCC = (FourCharacterCode)ToFourCC(chunk.Identifier[0], chunk.Identifier[1], chunk.Identifier[2], chunk.Identifier[3]);

        return HasSubType(fourCC);

        //switch(fourCC)
        //{
        //    case FourCharacterCode.RIFF:
        //    case FourCharacterCode.RIFX:
        //    case FourCharacterCode.RF64:
        //    case FourCharacterCode.ON2:
        //    case FourCharacterCode.odml:
        //    case FourCharacterCode.LIST:
        //        return true;
        //    default:
        //        return false;
        //}
    }

    public static bool HasSubType(byte[] chunk, int offset = 0)
    {
        if (chunk is null) throw new ArgumentNullException(nameof(chunk));

        if (chunk.Length - offset < 4) throw new ArgumentOutOfRangeException(nameof(offset));

        FourCharacterCode fourCC = (FourCharacterCode)ToFourCC(chunk[0], chunk[1], chunk[2], chunk[3]);

        return HasSubType(fourCC);
    }

    public static FourCharacterCode GetSubType(Node chunk)
    {
        return chunk is null
            ? throw new ArgumentNullException("chunk")
            : (FourCharacterCode)(HasSubType(chunk) ? ToFourCC(chunk.Identifier[4], chunk.Identifier[5], chunk.Identifier[6], chunk.Identifier[7]) : ToFourCC(chunk.Identifier[0], chunk.Identifier[1], chunk.Identifier[2], chunk.Identifier[3]));
    }

    public static FourCharacterCode GetSubType(byte[] chunk, int offset = 0)
    {
        return chunk.Length - offset < 4
            ? throw new ArgumentOutOfRangeException(nameof(offset))
            : (FourCharacterCode)(HasSubType(chunk) ? ToFourCC(chunk[offset + 4], chunk[offset + 5], chunk[offset + 6], chunk[offset + 7]) : ToFourCC(chunk[offset + 0], chunk[offset + 1], chunk[offset + 2], chunk[offset + 3]));
    }

    #endregion        

    public static string ToFourCharacterCode(byte[] identifier, int offset = 0, int count = 4)
    {
        //May have different results on different systems...
        return FromFourCC(ToFourCC(Array.ConvertAll<byte, char>(identifier.Skip(offset).Take(count).ToArray(), Convert.ToChar)));
    }

    public RiffReader(string filename, System.IO.FileAccess access = System.IO.FileAccess.Read) : base(filename, access) { }

    public RiffReader(Uri source, System.IO.FileAccess access = System.IO.FileAccess.Read) : base(source, access) { }

    public RiffReader(System.IO.FileStream source, System.IO.FileAccess access = System.IO.FileAccess.Read) : base(source, access) { }

    public RiffReader(Uri uri, System.IO.Stream source, int bufferSize = 8192) : base(uri, source, null, bufferSize, true) { }

    public IEnumerable<Node> ReadChunks(long offset = 0, params FourCharacterCode[] names)
    {
        return ReadChunks(offset, Array.ConvertAll<FourCharacterCode, int>(names, value => (int)value));
    }

    //Should have count but there is no indication where strh can occur

    public IEnumerable<Node> ReadChunks(long offset = 0, params int[] names)
    {
        long position = Position;

        Position = offset;

        foreach (var chunk in this)
        {
            if (names is null || names.Count() is 0 || names.Contains(Common.Binary.Read32(chunk.Identifier, 0, Media.Common.Binary.IsBigEndian)))
            {
                yield return chunk;
                continue;
            }
        }

        Position = position;

        yield break;
    }

    public Node ReadChunk(string name, long offset = 0) { return ReadChunk((FourCharacterCode)ToFourCC(name), offset); }

    public Node ReadChunk(FourCharacterCode name, long offset = 0)
    {
        long positionStart = Position;

        Node result = ReadChunks(offset, name).FirstOrDefault();

        Position = positionStart;

        return result;
    }

    //Typically found in the ds64 chunk.
    private ulong m_DataSize;

    /// <summary>
    /// Gets the size to use when a node with length == 0xFFFFFFFF is found.
    /// </summary>
    public long DataSize
    {
        get { return (long)m_DataSize; }
        protected internal set { m_DataSize = (ulong)value; }
    }

    //Determined by the first call to ReadNext.
    private bool? m_Needs64BitInfo;

    /// <summary>
    /// Indicates if the file has a header chunk which has additional information about the data contained.
    /// </summary>
    public bool Has64BitHeader
    {
        get
        {
            //Call Root to call ReadNext which sets m_Needs64BitInfo.Value the first time.
            if (false == m_Needs64BitInfo.HasValue) return Root is not null && m_Needs64BitInfo.Value;

            //Return the known value.
            return m_Needs64BitInfo.Value;
        }
        protected internal set
        {
            m_Needs64BitInfo = value;
        }
    }

    public Node ReadNext()
    {
        if (Remaining <= MinimumSize) throw new System.IO.EndOfStreamException();

        byte[] identifier = new byte[IdentifierSize];

        byte[] lengthBytes = new byte[LengthSize];

        int read = Read(identifier, 0, IdentifierSize);

        read += Read(lengthBytes, 0, LengthSize);

        ulong length = (ulong)Common.Binary.Read32(lengthBytes, 0, Media.Common.Binary.IsBigEndian);

        int identifierSize = IdentifierSize;

        //Get the fourCC of the node
        FourCharacterCode fourCC = (FourCharacterCode)Common.Binary.Read32(identifier, 0, Media.Common.Binary.IsBigEndian);

        //Store the first
        if (false == m_Type.HasValue) m_Type = fourCC;

        //Determine if 64 bit support is needed by inspecting the first node encountered.
        if (false == m_Needs64BitInfo.HasValue)
        {
            //There may be other nodes to account for also...
            m_Needs64BitInfo = fourCC == FourCharacterCode.RF64;
        }

        //Determine if an identifier follows
        if (RiffReader.HasSubType(fourCC))
        {
            //Resize the identifier to make room for the sub type
            Array.Resize(ref identifier, MinimumSize);

            //Read the sub type
            read += Read(identifier, IdentifierSize, IdentifierSize);

            //Not usually supposed to read the identifier
            length -= IdentifierSize;

            //Adjust for the bytes read.
            identifierSize += IdentifierSize;

            //Store the SubType if needed.
            if (false == m_SubType.HasValue) m_SubType = GetSubType(identifier);
        }

        //If this is a 64 bit entry
        if (length == uint.MaxValue)
        {
            //use the dataSize (0 for the first node, otherwise whatever was found)
            length = m_DataSize;

            //There are so may ways to handle this it's not funny, this seems to the most documented but probably one of the ugliest.
            //Not to mention this doesn't really give you compatiblity and doesn't contain a failsafe.

            //If files can be found which still don't work I will adjust this logic as necessary.
        }

        //return a new node,                                             Calculate length as padded size (to word boundary)
        return new Node(this, new Common.MemorySegment(identifier), identifierSize, LengthSize, Position, (long)(0 != (length & 1) ? ++length : length),
            read >= MinimumSize && length <= (ulong)Remaining); //determine Complete
    }


    public override IEnumerator<Node> GetEnumerator()
    {
        while (Remaining > TWODWORDSSIZE)
        {
            Node next = ReadNext();

            if (next is null) yield break;

            yield return next;

            if (m_Needs64BitInfo.Value && //If the file needs information from the ds64 node
                                          //The value must not have been read before and not found to be 0
                m_DataSize is 0 &&
                //There must be at least 28 bytes in a junk / ds64 chunk
                next.DataSize >= 28 &&
                //This is the ds64 chunk
                FourCharacterCode.ds64 == (FourCharacterCode)Common.Binary.Read32(next.Identifier, 0, Media.Common.Binary.IsBigEndian))
            {

                m_DataSize = (ulong)Common.Binary.Read64(next.Data, MinimumSize, Media.Common.Binary.IsBigEndian);

                //if this is found to be is 0 then what?

                /*
                 struct DataSize64Chunk // declare DataSize64Chunk structure
                {
                 * next.Identifier[0]
                char chunkId[4]; // ‘ds64’
                 * Not stored
                unsigned int32 chunkSize; // 4 byte size of the ‘ds64’ chunk
                 * next.Data[0]
                unsigned int32 riffSizeLow; // low 4 byte size of RF64 block
                unsigned int32 riffSizeHigh; // high 4 byte size of RF64 block
                unsigned int32 dataSizeLow; // low 4 byte size of data chunk
                unsigned int32 dataSizeHigh; // high 4 byte size of data chunk
                unsigned int32 sampleCountLow; // low 4 byte sample count of fact chunk
                unsigned int32 sampleCountHigh; // high 4 byte sample count of fact chunk
                unsigned int32 tableLength; // number of valid entries in array “table”
                chunkSize64 table[ ];
                };
                 */
            }

            //If this is a list parse into the list
            if (HasSubType(next)) continue;
            //Otherwise skip the data of the chunk
            else Skip(next.DataSize);
        }
    }

    public override Node Root
    {
        get
        {
            long position = Position;

            Node root = ReadChunks(0, FourCharacterCode.RIFF, FourCharacterCode.RIFX, FourCharacterCode.RF64, FourCharacterCode.ON2, FourCharacterCode.odml).FirstOrDefault();

            Position = position;

            return root;
        }
    }

    public override string ToTextualConvention(Container.Node node)
    {
        return node.Master.Equals(this) ? RiffReader.ToFourCharacterCode(node.Identifier) : base.ToTextualConvention(node);
    }

    private DateTime? m_Created, m_Modified;

    public DateTime Created
    {
        get
        {
            if (false == m_Created.HasValue) ParseIdentity();
            return m_Created.Value;
        }
    }

    public DateTime Modified
    {
        get
        {
            if (false == m_Modified.HasValue) ParseIdentity();
            return m_Modified.Value;
        }
    }

    private void ParseIdentity()
    {
        using (var iditChunk = ReadChunk(FourCharacterCode.IDIT, Root.Offset))
        {
            if (iditChunk is not null)
            {
                //Store the creation time.
                DateTime createdDateTime = FileInfo.CreationTimeUtc;

                int day = 0, year = 0;

                TimeSpan time = TimeSpan.Zero;

                //parts of the date in string form
                var parts = Encoding.UTF8.GetString(iditChunk.Data.Array).Split((char)Common.ASCII.Space);

                //cache the split length
                int partsLength = parts.Length;

                //If there are parts
                if (partsLength > 0)
                {
                    //Thanks bartmeirens!

                    //try parsing with current culture should specify all parts (m d time year)
                    if (false == DateTime.TryParseExact(parts[1], "MMM", System.Globalization.CultureInfo.CurrentCulture,
                            System.Globalization.DateTimeStyles.AssumeUniversal, out createdDateTime))
                    {
                        //: parse using invariant (en-US)
                        if (false == DateTime.TryParseExact(parts[1], "MMM", System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AssumeUniversal, out createdDateTime))
                        {
                            //The month portion of the result contains the data, the rest is blank
                            createdDateTime = FileInfo.CreationTimeUtc;
                        }
                    }

                    day = partsLength > 1 ? int.Parse(parts[2]) : FileInfo.CreationTimeUtc.Day;

                    if (partsLength > 2)
                    {
                        if (false == TimeSpan.TryParse(parts[3], out time))
                        {
                            time = FileInfo.CreationTimeUtc.TimeOfDay;
                        }
                    }
                    else time = FileInfo.CreationTimeUtc.TimeOfDay;

                    year = partsLength > 4 ? int.Parse(parts[4]) : FileInfo.CreationTimeUtc.Year;

                    m_Created = new DateTime(year, createdDateTime.Month, day, time.Hours, time.Minutes, time.Seconds, DateTimeKind.Utc);
                }
                else m_Created = FileInfo.CreationTimeUtc;
            }
            else m_Created = FileInfo.CreationTimeUtc;
        }

        m_Modified = FileInfo.LastWriteTimeUtc;
    }

    private int? m_MicroSecPerFrame, m_Format, m_SampleRate, m_BitsPerSample, m_NumChannels, m_MaxBytesPerSec, m_PaddingGranularity, m_Flags, m_TotalFrames, m_InitialFrames, m_Streams, m_SuggestedBufferSize, m_Width, m_Height, m_Reserved;
    private FourCharacterCode? m_Type, m_SubType;

    public FourCharacterCode? Type
    {
        get
        {
            if (false == m_Type.HasValue) ParseRoot();
            return m_Type.Value;
        }
    }

    public FourCharacterCode? SubType
    {
        get
        {
            if (false == m_SubType.HasValue) ParseRoot();
            return m_SubType.Value;
        }
    }

    public int SampleRate
    {
        get
        {
            if (SubType != FourCharacterCode.WAVE) return Common.Binary.Zero;
            if (false == m_SampleRate.HasValue) ParseFmt();
            return m_SampleRate.Value;
        }
    }

    public int BlockAlign
    {
        get
        {
            if (SubType != FourCharacterCode.WAVE) return Common.Binary.Zero;
            if (false == m_NumChannels.HasValue) ParseFmt();
            return m_NumChannels.Value * m_BitsPerSample.Value / Common.Binary.BitsPerByte;
        }
    }

    public int BitsPerSample
    {
        get
        {
            if (SubType != FourCharacterCode.WAVE) return Common.Binary.Zero;
            if (false == m_BitsPerSample.HasValue) ParseFmt();
            return m_BitsPerSample.Value;
        }
    }

    public int Channels
    {
        get
        {
            if (SubType != FourCharacterCode.WAVE) return Common.Binary.Zero;
            if (false == m_NumChannels.HasValue) ParseFmt();
            return m_NumChannels.Value;
        }
    }

    public int MicrosecondsPerFrame
    {
        get
        {
            if (SubType == FourCharacterCode.WAVE) return Common.Binary.Zero;
            if (false == m_MicroSecPerFrame.HasValue) ParseAviHeader();
            return m_MicroSecPerFrame.Value;
        }
    }

    /// <summary>
    /// The ByteRate for WAVE or the MaxBytesPerSecond for avi compliant files.
    /// </summary>
    public int MaxBytesPerSecond
    {
        get
        {
            switch (SubType)
            {
                case FourCharacterCode.WAVE:
                    return SampleRate * Channels * BitsPerSample / Common.Binary.BitsPerByte;
                default:
                    if (false == m_MaxBytesPerSec.HasValue) ParseAviHeader();
                    return m_MaxBytesPerSec.Value;
            }
        }
    }

    public int PaddingGranularity
    {
        get
        {
            if (SubType == FourCharacterCode.WAVE) return Common.Binary.Zero;
            if (false == m_PaddingGranularity.HasValue) ParseAviHeader();
            return m_PaddingGranularity.Value;
        }
    }

    public int Flags
    {
        get
        {
            if (SubType == FourCharacterCode.WAVE) return Common.Binary.Zero;
            if (false == m_Flags.HasValue) ParseAviHeader();
            return m_Flags.Value;
        }
    }

    public bool HasIndex { get { return ((MainHeaderFlags)Flags).HasFlag(MainHeaderFlags.HasIndex); } }

    public bool MustUseIndex { get { return ((MainHeaderFlags)Flags).HasFlag(MainHeaderFlags.MustUseIndex); } }

    public bool IsInterleaved { get { return ((MainHeaderFlags)Flags).HasFlag(MainHeaderFlags.IsInterleaved); } }

    public bool TrustChunkType { get { return ((MainHeaderFlags)Flags).HasFlag(MainHeaderFlags.TrustChunkType); } }

    public bool WasCaptureFile { get { return ((MainHeaderFlags)Flags).HasFlag(MainHeaderFlags.WasCaptureFile); } }

    public bool Copyrighted { get { return ((MainHeaderFlags)Flags).HasFlag(MainHeaderFlags.Copyrighted); } }

    public int TotalFrames
    {
        get
        {
            if (SubType == FourCharacterCode.WAVE) return Common.Binary.Zero;
            if (false == m_TotalFrames.HasValue) ParseAviHeader();
            return m_TotalFrames.Value;
        }
    }

    public int InitialFrames
    {
        get
        {
            if (SubType == FourCharacterCode.WAVE) return Common.Binary.Zero;
            if (false == m_InitialFrames.HasValue) ParseAviHeader();
            return m_InitialFrames.Value;
        }
    }

    public int SuggestedBufferSize
    {
        get
        {
            if (SubType == FourCharacterCode.WAVE) return Common.Binary.Zero;
            if (false == m_SuggestedBufferSize.HasValue) ParseAviHeader();
            return m_SuggestedBufferSize.Value;
        }
    }

    public int Streams
    {
        get
        {
            if (SubType == FourCharacterCode.WAVE) return Common.Binary.Zero;
            if (false == m_Streams.HasValue) ParseAviHeader();
            return m_Streams.Value;
        }
    }

    public int Width
    {
        get
        {
            if (SubType == FourCharacterCode.WAVE) return Common.Binary.Zero;
            if (false == m_Width.HasValue) ParseAviHeader();
            return m_Width.Value;
        }
    }

    public int Height
    {
        get
        {
            if (SubType == FourCharacterCode.WAVE) return Common.Binary.Zero;
            if (false == m_Height.HasValue) ParseAviHeader();
            return m_Height.Value;
        }
    }

    public TimeSpan Duration
    {
        get
        {
            switch (SubType)
            {
                case FourCharacterCode.WAVE:
                    {
                        if (false == m_BitsPerSample.HasValue) ParseFmt();

                        return TimeSpan.Zero;
                    }
                default:
                    {
                        if (false == m_TotalFrames.HasValue) ParseAviHeader();
                        return TimeSpan.FromMilliseconds((double)m_TotalFrames.Value * m_MicroSecPerFrame.Value / Common.Extensions.TimeSpan.TimeSpanExtensions.MicrosecondsPerMillisecond);
                    }
            }
        }
    }

    public int Reserved
    {
        get
        {
            if (SubType == FourCharacterCode.WAVE) return Common.Binary.Zero;
            if (false == m_Reserved.HasValue) ParseAviHeader();
            return m_Reserved.Value;
        }
    }

    protected internal void ParseFmt()
    {
        /*
        The "WAVE" format consists of two subchunks: "fmt " and "data":
        The "fmt " subchunk describes the sound data's format:

        12        4   Subchunk1ID      Contains the letters "fmt "
                                       (0x666d7420 big-endian form).
        16        4   Subchunk1Size    16 for PCM.  This is the size of the
                                       rest of the Subchunk which follows this number.
        20        2   AudioFormat      PCM = 1 (i.e. Linear quantization)
                                       Values other than 1 indicate some 
                                       form of compression.
        22        2   NumChannels      Mono = 1, Stereo = 2, etc.
        24        4   SampleRate       8000, 44100, etc.
        28        4   ByteRate         == SampleRate * NumChannels * BitsPerSample/8
        32        2   BlockAlign       == NumChannels * BitsPerSample/8
                                       The number of bytes for one sample including
                                       all channels. I wonder what happens when
                                       this number isn't an integer?
        34        2   BitsPerSample    8 bits = 8, 16 bits = 16, etc.
                  2   ExtraParamSize   if PCM, then doesn't exist
                  X   ExtraParams      space for extra parameters
        */

        using (var chunk = ReadChunk(FourCharacterCode.fmt, Root.Offset))
        {
            if (chunk is null) throw new InvalidOperationException("no 'fmt' Chunk found");

            m_Format = Common.Binary.Read16(chunk.Data, 0, Common.Binary.IsBigEndian);
            m_NumChannels = Common.Binary.Read16(chunk.Data, 2, Common.Binary.IsBigEndian);
            m_SampleRate = Common.Binary.Read32(chunk.Data, 4, Common.Binary.IsBigEndian);
            m_MaxBytesPerSec = Common.Binary.Read32(chunk.Data, 8, Common.Binary.IsBigEndian);
            short blockAlign = Common.Binary.Read16(chunk.Data, 12, Common.Binary.IsBigEndian);
            m_BitsPerSample = Common.Binary.Read16(chunk.Data, 14, Common.Binary.IsBigEndian);
            if (blockAlign != BlockAlign) throw new InvalidOperationException("BlockAlign");
            if (m_MaxBytesPerSec != MaxBytesPerSecond) throw new InvalidOperationException("MaxBytesPerSecond");
            //If len > 16
            // 2 bytes size for extra data
            // extra data bytes for codec.
        }


    }

    protected internal void ParseData(Container.Node node)
    {
        /*
        The "data" subchunk contains the size of the data and the actual sound:

        36        4   Subchunk2ID      Contains the letters "data"
                                       (0x64617461 big-endian form).
        40        4   Subchunk2Size    == NumSamples * NumChannels * BitsPerSample/8
                                       This is the number of bytes in the data.
                                       You can also think of this as the size
                                       of the read of the subchunk following this 
                                       number.
        44        *   Data             The actual sound data.
        */

        //using (var chunk = ReadChunk(FourCharacterCode.data, Root.Offset))
        //{
        //}
    }

    //void ParseInformation()
    //{

    //    //Should take a Node infoChunk and return a String[]?

    //    //Parse INFO (Should allow list filtering in read chunk)
    //    using (var chunk = ReadChunk(FourCharacterCode.INFO, Root.Offset))
    //    {
    //        //Two ways, either get a Chunk of info and look the stream of get the tags you want using ReadChunk
    //        using (var stream = chunk.Data)
    //        {
    //            //ISFT - Software
    //            //INAM - Title
    //            //ISTR - Performers 
    //            //IART - AlbumArtists 
    //            //IWRI - Composers 
    //            //ICMT - Comment
    //            //IGRN - Geners
    //            //ICRD - Year
    //            //IPRT - Track
    //            //IFRM - TrackCount
    //            //ICOP - Copyright 
    //        }
    //    }
    //}

    //void ParseMoiveId()
    //{
    //    //MID - MOVIE ID
    //    //Parse IART - Performers
    //    //Parse TITL - Title
    //    //Parse COMM - Comment 
    //    //Parse GENR - Genres  
    //    //Parse PRT1 - Track  
    //    //Parse PRT2 - TrackCount
    //}

    internal void ParseRoot()
    {
        using (Node root = Root)
        {
            m_Type = (FourCharacterCode)Common.Binary.Read32(root.Identifier, 0, Media.Common.Binary.IsBigEndian);
            if (Media.Containers.Riff.RiffReader.HasSubType(root)) m_SubType = Media.Containers.Riff.RiffReader.GetSubType(root);
        }
    }

    //void ParseOdmlHeader() { /*Total Number of Frames in File?*/ }

    private void ParseAviHeader()
    {
        //Must be present!
        using (var headerChunk = ReadChunk(FourCharacterCode.avih, Root.Offset))
        {
            if (headerChunk is null) throw new InvalidOperationException("no 'avih' Chunk found");

            int offset = 0;

            m_MicroSecPerFrame = Common.Binary.Read32(headerChunk.Data, ref offset, Media.Common.Binary.IsBigEndian);

            m_MaxBytesPerSec = Common.Binary.Read32(headerChunk.Data, ref offset, Media.Common.Binary.IsBigEndian);

            m_PaddingGranularity = Common.Binary.Read32(headerChunk.Data, ref offset, Media.Common.Binary.IsBigEndian);

            m_Flags = Common.Binary.Read32(headerChunk.Data, ref offset, Media.Common.Binary.IsBigEndian);

            m_TotalFrames = Common.Binary.Read32(headerChunk.Data, ref offset, Media.Common.Binary.IsBigEndian);

            m_InitialFrames = Common.Binary.Read32(headerChunk.Data, ref offset, Media.Common.Binary.IsBigEndian);

            m_Streams = Common.Binary.Read32(headerChunk.Data, ref offset, Media.Common.Binary.IsBigEndian);

            m_SuggestedBufferSize = Common.Binary.Read32(headerChunk.Data, ref offset, Media.Common.Binary.IsBigEndian);

            m_Width = Common.Binary.Read32(headerChunk.Data, ref offset, Media.Common.Binary.IsBigEndian);

            m_Height = Common.Binary.Read32(headerChunk.Data, ref offset, Media.Common.Binary.IsBigEndian);

            m_Reserved = Common.Binary.Read32(headerChunk.Data, ref offset, Media.Common.Binary.IsBigEndian);
        }
    }

    /// <summary>
    /// If <see cref="HasIndex"/> then either the 'idx1' or 'indx' chunk, otherwise the 'avhi', 'dmlh' or 'ds64' chunk.
    /// </summary>
    public override Node TableOfContents
    {
        get { return HasIndex ? ReadChunks(Root.Offset, FourCharacterCode.idx1, FourCharacterCode.indx).FirstOrDefault() : ReadChunks(Root.Offset, FourCharacterCode.avih, FourCharacterCode.dmlh, FourCharacterCode.ds64).FirstOrDefault(); }
    }

    //Index1Entry
    //Bool isKeyFram
    //Index--
    //Offset 
    //Size

    private IEnumerable<Track> m_Tracks;

    public override IEnumerable<Track> GetTracks()
    {

        if (m_Tracks is not null)
        {
            foreach (Track track in m_Tracks) yield return track;
            yield break;
        }

        long position = Position;

        var tracks = new List<Track>();

        int trackId = 0;

        if (SubType == FourCharacterCode.WAVE)
        {
            if (false == m_SampleRate.HasValue) ParseFmt();
            Track track = new(ReadChunk(FourCharacterCode.data), string.Empty, (int)(Length / BlockAlign), FileInfo.CreationTimeUtc, FileInfo.LastWriteTimeUtc,
                BlockAlign, 0, 0, TimeSpan.Zero, TimeSpan.FromSeconds(Length / (SampleRate * Channels * BitsPerSample / Common.Binary.BitsPerByte)),
                m_SampleRate.Value, Sdp.MediaType.audio, Common.Binary.GetBytes(m_Format.Value, Common.Binary.IsBigEndian), (byte)m_NumChannels.Value, (byte)m_BitsPerSample.Value,
                true);
            tracks.Add(track);
            yield return track;
        }
        else
        {
            //strh has all track level info, strn has stream name..
            foreach (var strhChunk in ReadChunks(Root.Offset, FourCharacterCode.strh).ToArray())
            {
                int offset = 0, sampleCount = TotalFrames, startTime = 0, timeScale = 0, duration = (int)Duration.TotalMilliseconds, width = Width, height = Height, rate = MicrosecondsPerFrame;

                string trackName = string.Empty;

                Sdp.MediaType mediaType = Sdp.MediaType.unknown;

                byte[] codecIndication = Media.Common.MemorySegment.EmptyBytes;

                byte channels = 0, bitDepth = 0;

                //Expect 56 Bytes

                FourCharacterCode fccType = (FourCharacterCode)Common.Binary.Read32(strhChunk.Data, offset, Media.Common.Binary.IsBigEndian);

                offset += 4;

                switch (fccType)
                {
                    case FourCharacterCode.iavs:
                        {
                            //Interleaved Audio and Video
                            //Should be audio and video samples together....?
                            //Things like this need a Special TrackType, MediaType doens't really cut it.
                            break;
                        }
                    case FourCharacterCode.vids:
                        {
                            //avg_frame_rate = timebase
                            mediaType = Sdp.MediaType.video;

                            sampleCount = ReadChunks(Root.Offset, ToFourCC(trackId.ToString("D2") + FourCharacterCode.dc.ToString()),
                                                                  ToFourCC(trackId.ToString("D2") + FourCharacterCode.db.ToString())).Count();
                            break;
                        }
                    case FourCharacterCode.mids: //Midi
                    case FourCharacterCode.auds:
                        {
                            mediaType = Sdp.MediaType.audio;

                            sampleCount = ReadChunks(Root.Offset, ToFourCC(trackId.ToString("D2") + FourCharacterCode.wb.ToString())).Count();

                            break;
                        }
                    case FourCharacterCode.txts:
                        {
                            sampleCount = ReadChunks(Root.Offset, ToFourCC(trackId.ToString("D2") + FourCharacterCode.tx.ToString())).Count();
                            mediaType = Sdp.MediaType.text; break;
                        }
                    case FourCharacterCode.data:
                        {
                            mediaType = Sdp.MediaType.data; break;
                        }
                    default: break;
                }

                //fccHandler
                codecIndication = strhChunk.Data.Skip(offset).Take(4).ToArray();

                offset += 4 + (DWORDSIZE * 3);

                //Scale
                timeScale = Common.Binary.Read32(strhChunk.Data, offset, Media.Common.Binary.IsBigEndian);

                offset += 4;

                //Rate
                rate = Common.Binary.Read32(strhChunk.Data, offset, Media.Common.Binary.IsBigEndian);

                offset += 4;

                //Defaults??? Should not be hard coded....
                if (false == (timeScale > 0 && rate > 0))
                {
                    rate = 25;
                    timeScale = 1;
                }

                //Start
                startTime = Common.Binary.Read32(strhChunk.Data, offset, Media.Common.Binary.IsBigEndian);

                offset += 4;

                //Length of stream (as defined in rate and timeScale above)
                duration = Common.Binary.Read32(strhChunk.Data, offset, Media.Common.Binary.IsBigEndian);

                offset += 4;

                //SuggestedBufferSize

                //Quality

                //SampleSize

                //RECT rcFrame (ushort left, top, right, bottom)

                //Get strf for additional info.

                switch (mediaType)
                {
                    case Sdp.MediaType.video:
                        {
                            using (var strf = ReadChunk(FourCharacterCode.strf, strhChunk.Offset))
                            {
                                if (strf is not null)
                                {
                                    //BitmapInfoHeader
                                    //Read 32 Width
                                    width = (int)Common.Binary.ReadU32(strf.Data, 4, Media.Common.Binary.IsBigEndian);

                                    //Read 32 Height
                                    height = (int)Common.Binary.ReadU32(strf.Data, 8, Media.Common.Binary.IsBigEndian);

                                    //Maybe...
                                    //Read 16 panes 

                                    //Read 16 BitDepth
                                    bitDepth = (byte)(int)Common.Binary.ReadU16(strf.Data, 14, Media.Common.Binary.IsBigEndian);

                                    //Read codec
                                    codecIndication = strf.Data.Skip(16).Take(4).ToArray();
                                }
                            }

                            break;
                        }
                    case Sdp.MediaType.audio:
                        {
                            //Expand Codec Indication based on iD?

                            using (var strf = ReadChunk(FourCharacterCode.strf, strhChunk.Offset))
                            {
                                if (strf is not null)
                                {
                                    //WaveFormat (EX) 
                                    codecIndication = strf.Data.Take(2).ToArray();
                                    channels = (byte)Common.Binary.ReadU16(strf.Data, 2, Media.Common.Binary.IsBigEndian);
                                    bitDepth = (byte)Common.Binary.ReadU16(strf.Data, 4, Media.Common.Binary.IsBigEndian);
                                }
                            }


                            break;
                        }
                    //text format....
                    default: break;
                }

                using (var strn = ReadChunk(FourCharacterCode.strn, strhChunk.Offset))
                {
                    if (strn is not null) trackName = Encoding.UTF8.GetString(strn.Data.Array, 8, (int)(strn.DataSize - 8));

                    //Variable BitRate must also take into account the size of each chunk / nBlockAlign * duration per frame.

                    Track created = new(strhChunk, trackName, ++trackId, Created, Modified, sampleCount, height, width,
                        TimeSpan.FromMilliseconds(startTime / timeScale),
                        mediaType == Sdp.MediaType.audio ?
                            TimeSpan.FromSeconds(duration / (double)rate) :
                            TimeSpan.FromMilliseconds((double)duration * m_MicroSecPerFrame.Value / Common.Extensions.TimeSpan.TimeSpanExtensions.MicrosecondsPerMillisecond),
                        rate / timeScale, mediaType, codecIndication, channels, bitDepth);

                    yield return created;

                    tracks.Add(created);
                }
            }
        }

        m_Tracks = tracks;

        Position = position;
    }

    public override Common.SegmentStream GetSample(Track track, out TimeSpan duration)
    {
        throw new NotImplementedException();
    }
}
