using Media.Codec;
using Media.Common;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Media.Codecs.Audio
{
    //Should be in AudioCodec
    ///// <summary>
    ///// Indicates how individual samples are retrieved from an AudioBuffer in memory.
    ///// </summary>
    //public enum PCMType
    //{
    //    //An unknown type of PCM.
    //    Unknown,
    //    //The most common PCM type.
    //    Linear,
    //    //Rather than representing sample amplitudes on a linear scale as linear PCM coding does, logarithmic PCM coding plots the amplitudes on a logarithmic scale. Log PCM is more often used in telephony and communications applications than in entertainment multimedia applications. (alaw or mulaw)
    //    Logarithmic,
    //    //Values are encoded as differences between the current and the previous value. This reduces the number of bits required per audio sample by about 25% compared to PCM.
    //    Differential,
    //    //The size of the quantization step is varied to allow further reduction of the required bandwidth for a given signal-to-noise ratio.
    //    Adaptive
    //}

    /// <summary>
    /// Defines the logic commonly assoicated with all types of Audio samples.
    /// <see href="http://wiki.multimedia.cx/?title=PCM#Frequency_And_Sample_Rate">The Multimedia Wiki</see>
    /// </summary>
    public class AudioBuffer : Media.Codec.MediaBuffer
    {
        #region Statics

        /// <summary>
        /// Calulcates the size in bytes required to store data of the given configuration
        /// </summary>
        /// <param name="numberOfSamples"></param>
        /// <param name="channels">The number of channels</param>
        /// <param name="sampleRate">The rate at which the audio will be played each second (hZ)</param>
        /// <param name="bitsPerComponent">The amount of bits </param>
        /// <returns>The amount of bytes required.</returns>
        private static int CalculateSize(int numberOfSamples, int channels, int sampleRate, int bitsPerComponent)
        {
            return Math.Abs(numberOfSamples * (sampleRate / (Common.Binary.BitsToBytes(bitsPerComponent) * channels)));
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Constructs a new AudioBuffer with the given configuration.
        /// Typically there is enough room to store 1 second of data in the buffer.
        /// </summary>
        /// <param name="byteOrder"></param>
        /// <param name="channels"></param>
        /// <param name="sampleRate"></param>
        /// <param name="bitsPerComponent"></param>
        public AudioBuffer(AudioFormat audioFormat, int numberOfSamples = 1, bool shouldDispose = true)
            : base(audioFormat, CalculateSize(numberOfSamples, audioFormat.Components.Length, audioFormat.SampleRate, audioFormat.Size), null, 0, shouldDispose)
        {
            //Validate the sampleRate given
            if (numberOfSamples <= 0) throw new ArgumentOutOfRangeException("numberOfSamples", "Must be > 0");

            //Validate the sampleRate given
            if (audioFormat.SampleRate <= 0 || false == Common.Binary.IsEven(audioFormat.SampleRate))
                throw new ArgumentOutOfRangeException("sampleRate", "Must be > 0 and an even number");

            //Set the SampleCount from the given value
            //SampleCount = numberOfSamples;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the <see cref="Media.Codecs.Audio.AudioFormat"/> assoicated with the samples within the buffer.
        /// </summary>
        public AudioFormat AudioFormat { get { return MediaFormat as AudioFormat; } }

        /// <summary>
        /// The number of different speakers for which data can be found in the sample.
        /// </summary>
        public int Channels { get { return MediaFormat.Components.Length; } }

        /// <summary>
        /// Indicates if the sample contains data for only 1 speaker.
        /// </summary>
        public bool Mono { get { return Channels == 1; } }

        /// <summary>
        /// Indicates if the samples contians data for exactly 2 speakers.
        /// </summary>
        public bool Stereo { get { return Channels == 2; } }

        /// <summary>
        /// Indicates if the sample contains data for more than 2 speakers.
        /// </summary>
        public bool MultiChannel { get { return Channels > 2; } }

        /// <summary>
        /// Gets the sample rate
        /// </summary>
        public int SampleRate { get { return AudioFormat.SampleRate; } }

        #endregion

        #region Methods

        public MemorySegment GetComponentData(int sampleOffset, MediaComponent component)
        {
            if (sampleOffset < 0 || sampleOffset >= SampleCount)
                throw new ArgumentOutOfRangeException(nameof(sampleOffset), "Sample offset is out of range.");

            if (component is null)
                throw new ArgumentNullException(nameof(component));

            int componentIndex = Array.IndexOf(MediaFormat.Components, component);

            if (componentIndex < 0)
                throw new ArgumentException("The specified component is not part of the media format.", nameof(component));

            int offset = CalculateSampleDataOffset(sampleOffset, componentIndex);
            int byteOffset = offset / Binary.BitsPerByte;

            return new MemorySegment(Data.Array, Data.Offset + byteOffset, Data.Count - byteOffset);
        }

        public Vector<byte> GetComponentVector(int sampleIndex, int channelIndex)
        {
            int offset = CalculateSampleDataOffset(sampleIndex, channelIndex);
            offset -= offset % Vector<byte>.Count; // Align the offset to vector size
            return new Vector<byte>(Data.Array, Data.Offset + offset);
        }

        public int CalculateSampleDataOffset(int sampleIndex, int channelIndex)
        {
            if (sampleIndex < 0 || sampleIndex >= SampleCount)
                throw new ArgumentOutOfRangeException(nameof(sampleIndex), "Invalid sample index");

            if (channelIndex < 0 || channelIndex >= Channels)
                throw new ArgumentOutOfRangeException(nameof(channelIndex), "Invalid channel index");

            switch (DataLayout)
            {
                case DataLayout.Packed:
                    // Packed layout
                    return sampleIndex * SampleLength + channelIndex * MediaFormat.Components[channelIndex].Length;
                case DataLayout.Planar:
                    // Planar layout
                    return sampleIndex * SampleLength * Channels + channelIndex * MediaFormat.Components[channelIndex].Length;
                case DataLayout.SemiPlanar:
                    // SemiPlanar layout
                    int packedSize = 0;
                    for (int i = 0; i < Channels - 1; i++)
                        packedSize += MediaFormat.Components[i].Size / Binary.BitsPerByte;

                    if (channelIndex == Channels - 1)
                    {
                        // Last component (the packed one)
                        return sampleIndex * SampleLength + packedSize;
                    }
                    else
                    {
                        // Planar component
                        return sampleIndex * SampleLength * Channels + channelIndex * MediaFormat.Components[channelIndex].Length;
                    }
                default:
                    throw new InvalidOperationException("Unsupported data layout.");
            }
        }

        /// <summary>
        /// Sets the sample data for the given sample index, channel, and component.
        /// </summary>
        /// <param name="sampleIndex">The index of the sample in the buffer.</param>
        /// <param name="channel">The channel index. Must be within the range of available channels.</param>
        /// <param name="componentData">The data to be set for the given sample, channel, and component.</param>
        public void SetSampleData(int sampleIndex, int channel, MemorySegment data)
        {
            if (sampleIndex < 0 || sampleIndex >= SampleCount)
                throw new ArgumentOutOfRangeException(nameof(sampleIndex), "Invalid sample index");

            if (channel < 0 || channel >= Channels)
                throw new ArgumentOutOfRangeException(nameof(channel), "Invalid channel index");

            int offset = CalculateSampleDataOffset(sampleIndex, channel);

            if (data.Count + offset > Data.Count)
                throw new ArgumentException("The provided data segment is too large for the buffer.", nameof(data));

            data.CopyTo(Data.Array, offset);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sampleIndex"></param>
        /// <param name="channel"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public MemorySegment GetSampleData(int sampleIndex, int channel)
        {
            if (sampleIndex < 0 || sampleIndex >= SampleCount)
                throw new ArgumentOutOfRangeException(nameof(sampleIndex), "Invalid sample index");

            if (channel < 0 || channel >= Channels)
                throw new ArgumentOutOfRangeException(nameof(channel), "Invalid channel index");

            int bytesPerSample = AudioFormat.Length;
            int offset = CalculateSampleDataOffset(sampleIndex, channel);

            return offset + bytesPerSample > Data.Count
                ? throw new ArgumentException("The requested sample data is outside the bounds of the buffer.")
                : new MemorySegment(Data.Array, Data.Offset + offset, bytesPerSample);
        }

        #endregion
    }
}

namespace Media.UnitTests
{
    /// <summary>
    /// Provides tests which ensure the logic of the supporting class is correct
    /// </summary>
    internal class AudioUnitTests
    {
        public static void Test_AudioFormat_AudioBuffer_Constructor()
        {
            //Construct a new AudioFormat with one component, sampled at 8000hz, all samples are signed 8 bit and in little endian order.
            Media.Codecs.Audio.AudioFormat audioFormat = new(8000, true, Common.Binary.ByteOrder.Little, Media.Codec.DataLayout.Packed, new Media.Codec.MediaComponent[]{
                new(0, 8)
            });

            //Could be given in place to the constructor.
            using (Media.Codecs.Audio.AudioBuffer audio = new(audioFormat))
            {
                if (audio.Channels != 1) throw new System.InvalidOperationException();

                //if (audio.SampleCount != 1) throw new System.InvalidOperationException();

                //if (audio.Data.Count != 1000) throw new System.InvalidOperationException();
            }
        }

        public static void Test_AudioTransformer()
        {
            //Construct a new AudioFormat with one component, sampled at 8000hz, all samples are signed 8 bit and in little endian order.
            Media.Codecs.Audio.AudioFormat audioFormat = new(8000, true, Common.Binary.ByteOrder.Little, Media.Codec.DataLayout.Packed, new Media.Codec.MediaComponent[]{
                new(0, 8)
            });

            //Could be given in place to the constructor.
            using (Media.Codecs.Audio.AudioBuffer source = new(audioFormat))
            {
                //Example for upsampling by a factor of 2
                int sampleFactor = 2;

                using (Media.Codecs.Audio.AudioBuffer destination = new(new Codecs.Audio.AudioFormat(source.SampleRate * sampleFactor, source.AudioFormat.IsSigned, source.AudioFormat.ByteOrder, source.DataLayout, audioFormat.Components)))
                {
                    using (Media.Codecs.Audio.AudioTransformer audioTransformer = new(source, destination, sampleFactor, TransformationQuality.None))
                    {
                        audioTransformer.Transform();
                    }
                }

                //Example for downsample by a factor of 2
                sampleFactor = -2;

                using (Media.Codecs.Audio.AudioBuffer destination = new(new Codecs.Audio.AudioFormat(Math.Abs(source.SampleRate * sampleFactor), source.AudioFormat.IsSigned, source.AudioFormat.ByteOrder, source.DataLayout, audioFormat.Components)))
                {
                    using (Media.Codecs.Audio.AudioTransformer audioTransformer = new(source, destination, sampleFactor, TransformationQuality.None))
                    {
                        audioTransformer.Transform();
                    }
                }
            }
        }

    }
}