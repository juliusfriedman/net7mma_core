using System;
using System.Collections.Generic;
using System.Linq;

namespace UnitTests.Code;

//https://github.com/bert2/DtmfDetection/blob/master/src/DtmfDetection

    /// <summary>Provides helpers to generate DTMF tones.</summary>
public static class DtmfGenerator
{
    /// <summary>The detector configuration.</summary>
    public readonly struct Config : IEquatable<Config>
{
    /// <summary>The default detection threshold (tuned to normalized responses).</summary>
    public const double DefaultThreshold = 30;

    /// <summary>The default number of samples to analyze before the Goertzel response should be calulated (tuned to minimize error of the target frequency bin).</summary>
    public const int DefaultSampleBlockSize = 205;

    /// <summary>Default rate (in Hz) at which the analyzed samples are expected to have been measured.</summary>
    public const int DefaultSampleRate = 8000;

    /// <summary>A default configuration instance.</summary>
    public static readonly Config Default = new Config(DefaultThreshold, DefaultSampleBlockSize, DefaultSampleRate, normalizeResponse: true);

    /// <summary>The detection threshold. Typical values are `30`-`35` (when `NormalizeResponse` is `true`) and `100`-`115` (when `NormalizeResponse` is `false`).</summary>
    public readonly double Threshold;

    /// <summary>The number of samples to analyze before the Goertzel response should be calulated. It is recommened to leave it at the default value `205` (tuned to minimize error of the target frequency bin).</summary>
    public readonly int SampleBlockSize;

    /// <summary>The sample rate (in Hz) the Goertzel algorithm expects. Sources with higher samples rates must resampled to this sample rate. It is recommended to leave it at the default value `8000`.</summary>
    public readonly int SampleRate;

    /// <summary>Toggles normalization of the Goertzel response with the total signal energy of the sample block. Recommended setting is `true` as this provides invariance to loudness changes of the signal.</summary>
    public readonly bool NormalizeResponse;

    /// <summary>Creates a new `Config` instance.</summary>
    /// <param name="threshold">The detection threshold. Typical values are `30`-`35` (when `normalizeResponse` is `true`) and `100`-`115` (when `normalizeResponse` is `false`).</param>
    /// <param name="sampleBlockSize">The number of samples to analyze before the Goertzel response should be calulated. It is recommened to leave it at the default value `205` (tuned to minimize error of the target frequency bin).</param>
    /// <param name="sampleRate">The sample rate (in Hz) the Goertzel algorithm expects. Sources with higher samples rates must resampled to this sample rate. It is recommended to leave it at the default value `8000`.</param>
    /// <param name="normalizeResponse">Toggles normalization of the Goertzel response with the total signal energy of the sample block. Recommended setting is `true` as this provides invariance to loudness changes of the signal.</param>
    public Config(double threshold, int sampleBlockSize, int sampleRate, bool normalizeResponse)
        => (Threshold, SampleBlockSize, SampleRate, NormalizeResponse) = (threshold, sampleBlockSize, sampleRate, normalizeResponse);

    /// <summary>Creates a cloned `Config` instance from this instance, but with a new `Threshold` setting.</summary>
    /// <param name="threshold">The detection threshold. Typical values are `30`-`35` (when `normalizeResponse` is `true`) and `100`-`115` (when `normalizeResponse` is `false`).</param>
    /// <returns>A new `Config` instance with the specified `Threshold` setting.</returns>
    public Config WithThreshold(double threshold) => new Config(threshold, SampleBlockSize, SampleRate, NormalizeResponse);

    /// <summary>Creates a cloned `Config` instance from this instance, but with a new `SampleBlockSize` setting.</summary>
    /// <param name="sampleBlockSize">The number of samples to analyze before the Goertzel response should be calulated. It is recommened to leave it at the default value `205` (tuned to minimize error of the target frequency bin).</param>
    /// <returns>A new `Config` instance with the specified `SampleBlockSize` setting.</returns>
    public Config WithSampleBlockSize(int sampleBlockSize) => new Config(Threshold, sampleBlockSize, SampleRate, NormalizeResponse);

    /// <summary>Creates a cloned `Config` instance from this instance, but with a new `SampleRate` setting.</summary>
    /// <param name="sampleRate">The sample rate (in Hz) the Goertzel algorithm expects. Sources with higher samples rates must resampled to this sample rate. It is recommended to leave it at the default value `8000`.</param>
    /// <returns>A new `Config` instance with the specified `SampleRate` setting.</returns>
    public Config WithSampleRate(int sampleRate) => new Config(Threshold, SampleBlockSize, sampleRate, NormalizeResponse);

    /// <summary>Creates a cloned `Config` instance from this instance, but with a new `NormalizeResponse` setting.</summary>
    /// <param name="normalizeResponse">Toggles normalization of the Goertzel response with the total signal energy of the sample block. Recommended setting is `true` as this provides invariance to loudness changes of the signal.</param>
    /// <returns>A new `Config` instance with the specified `NormalizeResponse` setting.</returns>
    public Config WithNormalizeResponse(bool normalizeResponse) => new Config(Threshold, SampleBlockSize, SampleRate, normalizeResponse);

    #region Equality implementations

    /// <summary>Indicates whether the current `Config` is equal to another `Config`.</summary>
    /// <param name="other">A `Config` to compare with this `Config`.</param>
    /// <returns>Returns `true` if the current `Config` is equal to `other`; otherwise, `false`.</returns>
    public bool Equals(Config other) =>
        (Threshold, SampleBlockSize, SampleRate, NormalizeResponse)
        == (other.Threshold, other.SampleBlockSize, other.SampleRate, other.NormalizeResponse);

    /// <summary>Indicates whether this `Config` and a specified object are equal.</summary>
    /// <param name="obj">The object to compare with the current `Config`.</param>
    /// <returns>Returns `true` if `obj` this `Config` are the same type and represent the same value; otherwise, `false`.</returns>
    public override bool Equals(object? obj) => obj is Config other && Equals(other);

    /// <summary>Indicates whether the left-hand side `Config` is equal to the right-hand side `Config`.</summary>
    /// <param name="left">The left-hand side `Config` of the comparison.</param>
    /// <param name="right">The right-hand side `Config` of the comparison.</param>
    /// <returns>Returns `true` if the left-hand side `Config` is equal to the right-hand side `Config`; otherwise, `false`.</returns>
    public static bool operator ==(Config left, Config right) => left.Equals(right);

    /// <summary>Indicates whether the left-hand side `Config` is not equal to the right-hand side `Config`.</summary>
    /// <param name="left">The left-hand side `Config` of the comparison.</param>
    /// <param name="right">The right-hand side `Config` of the comparison.</param>
    /// <returns>Returns `true` if the left-hand side `Config` is not equal to the right-hand side `Config`; otherwise, `false`.</returns>
    public static bool operator !=(Config left, Config right) => !(left == right);

    /// <summary>Returns the hash code for this `Config`.</summary>
    /// <returns>A 32-bit signed integer that is the hash code for this `Config`.</returns>
    public override int GetHashCode() => HashCode.Combine(Threshold, SampleBlockSize, SampleRate, NormalizeResponse);

    #endregion Equality implementations
}

    /// <summary>Generates single-channel PCM data playing the dual tone comprised of the two frequencies `highFreq` and `lowFreq` infinitely.</summary>
    /// <param name="highFreq">The high frequency part of the dual tone.</param>
    /// <param name="lowFreq">The low frequency part of the dual tone.</param>
    /// <param name="sampleRate">Optional sample rate of the PCM data. Defaults to `Config.DefaultSampleRate`.</param>
    /// <returns>An infinite sequence of PCM data playing the specified dual tone.</returns>
    public static IEnumerable<float> Generate(int highFreq, int lowFreq, int sampleRate = Config.DefaultSampleRate)
        => Sine(highFreq, sampleRate).Add(Sine(lowFreq, sampleRate)).Normalize(1);

    /// <summary>Generates single-channel PCM data playing the dual tone comprised of the two frequencies `highFreq` and `lowFreq` infinitely.</summary>
    /// <param name="dual">A tuple holding the high and low frequency.</param>
    /// <param name="sampleRate">Optional sample rate of the PCM data. Defaults to `Config.DefaultSampleRate`.</param>
    /// <returns>An infinite sequence of PCM data playing the specified dual tone.</returns>
    public static IEnumerable<float> Generate((int highFreq, int lowFreq) dual, int sampleRate = Config.DefaultSampleRate)
        => Generate(dual.highFreq, dual.lowFreq, sampleRate);

    /// <summary>Generates single-channel PCM data playing silence for the specified length `ms`.</summary>
    /// <param name="ms">The length of the silence in milliseconds.</param>
    /// <param name="sampleRate">Optional sample rate of the PCM data. Defaults to `Config.DefaultSampleRate`.</param>
    /// <returns>A sequence of silent PCM data.</returns>
    public static IEnumerable<float> Space(int ms = 20, int sampleRate = Config.DefaultSampleRate)
        => Constant(.0f).Take(NumSamples(ms, channels: 1, sampleRate));

    /// <summary>Takes two sequences of single-channel PCM data and interleaves them to form a single sequence of dual-channel PCM data.</summary>
    /// <param name="left">The PCM data for the left channel.</param>
    /// <param name="right">The PCM data for the right channel.</param>
    /// <returns>A sequence of dual-channel PCM data.</returns>
    public static IEnumerable<float> Stereo(IEnumerable<float> left, IEnumerable<float> right)
        => left.Zip(right, (l, r) => new[] { l, r }).SelectMany(x => x);

    /// <summary>Generates a sinusoidal PCM signal of infinite length for the specified frequency.</summary>
    /// <param name="freq">The frequency of the signal.</param>
    /// <param name="sampleRate">Optional sample rate of the PCM data. Defaults to `Config.DefaultSampleRate`.</param>
    /// <param name="amplitude">Optional amplitude of the signal. Defaults to `1`.</param>
    /// <returns>An infinite sine signal.</returns>
    public static IEnumerable<float> Sine(int freq, int sampleRate = Config.DefaultSampleRate, float amplitude = 1)
    {
        for (var t = 0.0; ; t += 1.0 / sampleRate)
            yield return (float)(amplitude * Math.Sin(2.0 * Math.PI * freq * t));
    }

    /// <summary>Generates a constant PCM signal of infinite length.</summary>
    /// <param name="amplitude">The amplitude of the signal.</param>
    /// <returns>An infinite constant signal.</returns>
    public static IEnumerable<float> Constant(float amplitude)
    {
        while (true) yield return amplitude;
    }

    /// <summary>Generates an infinite PCM signal of pseudo-random white noise.</summary>
    /// <param name="amplitude">The amplitude of the noise.</param>
    /// <returns>An infinite noise signal.</returns>
    public static IEnumerable<float> Noise(float amplitude)
    {
        var rng = new Random();
        while (true)
        {
            var n = amplitude * rng.NextDouble();
            var sign = Math.Pow(-1, rng.Next(2));
            yield return (float)(sign * n);
        }
    }

    /// <summary>Adds two sequences of PCM data together. Used to generate dual tones. The amplitude might exceed the range `[-1..1]` after adding.</summary>
    /// <param name="xs">One of the two input signals to add.</param>
    /// <param name="ys">One of the two input signals to add.</param>
    /// <returns>The sum of both input signals.</returns>
    public static IEnumerable<float> Add(this IEnumerable<float> xs, IEnumerable<float> ys)
        => xs.Zip(ys, (l, r) => l + r);

    /// <summary>Normlizes a signal with the given `maxAmplitude`.</summary>
    /// <param name="source">The signal to normalize.</param>
    /// <param name="maxAmplitude">The value to normalize by. Ideally it should equal `Math.Abs(source.Max())`.</param>
    /// <returns>The input signal with each sample value divided by `maxAmplitude`.</returns>
    public static IEnumerable<float> Normalize(this IEnumerable<float> source, float maxAmplitude)
        => source.Select(x => x / maxAmplitude);

    /// <summary>Concatenates multiple finite sequences of PCM data. Typically used with `Mark()` and `Space()`.</summary>
    /// <param name="xss">The sequences to concatenate.</param>
    /// <returns>The single sequence that is the concatenation of the given sequences.</returns>
    public static IEnumerable<float> Concat(params IEnumerable<float>[] xss) => xss.SelectMany(xs => xs);

    /// <summary>Converts a duration in milliseconds into the number of samples required to represent a signal of that duration as PCM audio data.</summary>
    /// <param name="milliSeconds">The duration of the signal.</param>
    /// <param name="channels">Optional number of channels in the signal. Defaults to `1`.</param>
    /// <param name="sampleRate">Optional sample rate of the signal. Defaults to `Config.DefaultSampleRate`.</param>
    /// <returns>The number of samples needed for the specified length, channels, and sample rate.</returns>
    public static int NumSamples(int milliSeconds, int channels = 1, int sampleRate = Config.DefaultSampleRate)
        => channels * (int)Math.Round(milliSeconds / (1.0 / sampleRate * 1000));
}
