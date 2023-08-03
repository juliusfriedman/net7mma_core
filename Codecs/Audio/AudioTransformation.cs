#region Copyright
/*
This file came from Managed Media Aggregation, You can always find the latest version @ https://net7mma.codeplex.com/
  
 Julius.Friedman@gmail.com / (SR. Software Engineer ASTI Transportation Inc. http://www.asti-trans.com)

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

namespace Media.Codecs.Audio
{
    /// <summary>
    /// Defines the base class of all Audio transformations
    /// </summary>
    public abstract class AudioTransformation : Media.Codec.Transformation
    {
        #region Nested Types

        public delegate void AudioTransform(AudioBuffer source, AudioBuffer dest);

        #endregion

        #region Fields

        protected AudioBuffer m_Source, m_Dest;

        #endregion

        #region Constructor

        /// <summary>
        ///
        /// </summary>
        /// <param name="quality"></param>
        /// <param name="shouldDispose"></param>
        protected AudioTransformation(Codec.TransformationQuality quality = Codec.TransformationQuality.Unspecified, bool shouldDispose = true)
            : base(Codec.MediaType.Audio, quality, shouldDispose)
        {

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="dest"></param>
        /// <param name="quality"></param>
        /// <param name="shouldDispose"></param>
        public AudioTransformation(AudioBuffer source, AudioBuffer dest, Codec.TransformationQuality quality = Codec.TransformationQuality.Unspecified, bool shouldDispose = true)
            :this(quality, shouldDispose)
        {
            if (Common.IDisposedExtensions.IsNullOrDisposed(source)) throw new System.ArgumentNullException("source");
            m_Source = source;

            if (Common.IDisposedExtensions.IsNullOrDisposed(dest)) throw new System.ArgumentNullException("dest");
            m_Dest = dest;  
        }

        #endregion

        #region Properties

        public AudioBuffer Source
        {
            get { return m_Source; }
            set
            {
                if (Common.IDisposedExtensions.IsNullOrDisposed(value)) throw new System.ArgumentNullException("value");
                m_Source = value;
            }
        }

        public AudioBuffer Destination
        {
            get { return m_Dest; }
            set
            {
                if (Common.IDisposedExtensions.IsNullOrDisposed(value)) throw new System.ArgumentNullException("value");
                m_Dest = value;
            }
        }

        #endregion

        #region Methods

        public override void Dispose()
        {
            m_Source = null;

            m_Dest = null;

            base.Dispose();
        }

        #endregion
    }

    /// <summary>
    /// Defines a class which can upsample or downsample data.
    /// </summary>
    public class AudioTransformer : AudioTransformation
    {
        // The upsampling or downsampling factor
        private int sampleRateFactor;

        public AudioTransformer(AudioBuffer source, AudioBuffer destination, int sampleRateFactor, Codec.TransformationQuality quality = Codec.TransformationQuality.Unspecified, bool shouldDispose = true)
            : base(source, destination, quality, shouldDispose)
        {
            this.sampleRateFactor = sampleRateFactor;
        }

        public override void Transform()
        {
            if (sampleRateFactor == 1)
            {
                // No change in sample rate, just copy the data
                Destination.Data.Array.CopyTo(Source.Data.Array, Source.Data.Offset);
                return;
            }

            if (sampleRateFactor > 0)
            {
                // Upsample the audio data
                int destSampleCount = Source.SampleCount * sampleRateFactor;
                var format = new AudioFormat(Source.AudioFormat.SampleRate * sampleRateFactor, Source.AudioFormat.IsSigned, Source.AudioFormat.ByteOrder, Source.DataLayout, Source.AudioFormat.Components);
                var destBuffer = new AudioBuffer(format, destSampleCount);

                for (int channel = 0; channel < Source.Channels; channel++)
                {
                    for (int i = 0; i < Source.SampleCount; i++)
                    {
                        int destIndex = i * sampleRateFactor;
                        for (int j = 0; j < sampleRateFactor; j++)
                        {
                            destBuffer.SetSampleData(destIndex + j, channel, Source.GetSampleData(i, channel));
                        }
                    }
                }

                destBuffer.Data.Array.CopyTo(Destination.Data.Array, Destination.Data.Offset);
            }
            else if (sampleRateFactor < 0)
            {
                // Downsampling by dropping samples
                int destSampleCount = Source.SampleCount / System.Math.Abs(sampleRateFactor);
                var format = new AudioFormat(Source.AudioFormat.SampleRate / System.Math.Abs(sampleRateFactor), Source.AudioFormat.IsSigned, Source.AudioFormat.ByteOrder, Source.DataLayout, Source.AudioFormat.Components);
                var destBuffer = new AudioBuffer(format, destSampleCount);

                for (int channel = 0; channel < Source.Channels; channel++)
                {
                    for (int i = 0; i < destSampleCount; i++)
                    {
                        int sourceIndex = i * System.Math.Abs(sampleRateFactor);
                        destBuffer.SetSampleData(i, channel, Source.GetSampleData(sourceIndex, channel));
                    }
                }

                destBuffer.Data.Array.CopyTo(Destination.Data.Array, Destination.Data.Offset);
            }
        }
    }

    //// Example for upsampling by a factor of 2
    //int upsampleFactor = 2;
    //AudioBuffer sourceBuffer = ... // Initialize the source buffer
    //AudioBuffer destinationBuffer = new AudioBuffer(new AudioFormat(sourceBuffer.SampleRate * upsampleFactor, 16, Common.Binary.ByteOrder.Little, sourceBuffer.AudioFormat.Components), sourceBuffer.SampleCount * upsampleFactor);
    //AudioTransformer upsampleTransform = new AudioTransformer(upsampleFactor);
    //upsampleTransform.Source = sourceBuffer;
    //upsampleTransform.Destination = destinationBuffer;
    //upsampleTransform.Transform();

    //// Example for downsampling by a factor of 2
    //int downsampleFactor = -2;
    //AudioBuffer sourceBuffer = ... // Initialize the source buffer
    //AudioBuffer destinationBuffer = new AudioBuffer(new AudioFormat(sourceBuffer.SampleRate / Math.Abs(downsampleFactor), 16, Common.Binary.ByteOrder.Little, sourceBuffer.AudioFormat.Components), sourceBuffer.SampleCount / Math.Abs(downsampleFactor));
    //AudioTransformer downsampleTransform = new AudioTransformer(downsampleFactor);
    //downsampleTransform.Source = sourceBuffer;
    //downsampleTransform.Destination = destinationBuffer;
    //downsampleTransform.Transform();
}
