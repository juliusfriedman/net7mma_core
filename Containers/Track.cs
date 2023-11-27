#region Copyright
/*
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
#endregion

#region Using Statements
using System;
#endregion

namespace Media.Container
{
    /// <summary>
    /// A Track describes the information related to samples within a MediaFileStream
    /// </summary>
    public class Track : Common.CommonDisposable
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="header"></param>
        /// <param name="name"></param>
        /// <param name="id"></param>
        /// <param name="created"></param>
        /// <param name="modified"></param>
        /// <param name="sampleCount"></param>
        /// <param name="height"></param>
        /// <param name="width"></param>
        /// <param name="position"></param>
        /// <param name="duration"></param>
        /// <param name="frameRate"></param>
        /// <param name="mediaType">This could be a type defined either here or in Common to reduce the need to have SDP as a reference</param>
        /// <param name="codecIndication">There needs to be either a method on each IMediaContainer to get a 4cc or a common mapping.</param>
        /// May not always be present...
        /// <param name="channels"></param>
        /// <param name="bitDepth"></param>
        public Track(Node header, string name, int id, DateTime created, DateTime modified, long sampleCount, int height, int width, TimeSpan position, TimeSpan duration, double frameRate, Sdp.MediaType mediaType, byte[] codecIndication, byte channels = 0, byte bitDepth = 0, bool enabled = true)
        {
            this.Header = header;
            this.Width = width;
            this.Height = height;
            this.Id = id;
            this.Created = created;
            this.Modified = modified;
            this.Position = position;
            this.Duration = duration;
            this.Rate = frameRate;
            this.MediaType = mediaType;
            this.Name = name;
            this.SampleCount = sampleCount;
            this.CodecIndication = codecIndication;
            this.Channels = channels;
            this.BitDepth = bitDepth;
            this.Enabled = enabled;
            //this.Volume = volume;
        }

        #region Fields

        //EncryptedTrack... or IsEncrypted...

        public Node Header { get; protected internal set; }

        public long Offset { get; protected internal set; }

        public int Id { get; protected internal set; }

        public string Name { get; protected internal set; }

        //public readonly string Language; //Useful?

        //FourCC?
        public byte[] CodecIndication { get; protected internal set; }

        public double Rate { get; protected internal set; }

        public int Width { get; protected internal set; }

        public int Height { get; protected internal set; }

        //Todo, use common type not Sdp?

        public Sdp.MediaType MediaType { get; protected internal set; }

        public TimeSpan Duration { get; protected internal set; }

        public DateTime Created { get; protected internal set; }

        public DateTime Modified { get; protected internal set; }

        public long SampleCount { get; protected internal set; }

        public byte Channels { get; protected internal set; }

        public byte BitDepth { get; protected internal set; }

        public bool Enabled { get; protected internal set; }

        /// <summary>
        /// Used to adjust the sample which is retrieved next.
        /// </summary>
        public TimeSpan Position;

        /// <summary>
        /// Used to adjust the volume of the samples when passed to a decoder.
        /// </summary>
        public float Volume;

        //SampleReader / SampleWriter = implemenation details typically a container such as Base Media etc.

        /// <summary>
        /// Used to store data segments which are realted to the track (such as undecoded bit stream data or raw samples) and assist with buffering.
        /// This stream is typically populated by the <see cref="Node.Master"/> after a call to <see cref="IMediaContainer.GetSample(Track, out TimeSpan)"/>.        
        /// </summary>
        public Common.SegmentStream DataStream = new();

        //Possibly add time to sample table implementation

        /// <summary>
        /// Reserved for use within the application
        /// </summary>
        public object UserData;

        #endregion

        #region Properties

        public TimeSpan Remaining { get { return Duration - Position; } }

        #endregion

        #region Overrides

        protected override void Dispose(bool disposing)
        {
            if (disposing is false) return;

            base.Dispose(ShouldDispose);

            if (IsDisposed is false) return;

            //Dispose the stream.
            DataStream.Dispose();
        }

        #endregion
    }

    //VideoTrack

    //AudioTrack

    //TextTrack

}
