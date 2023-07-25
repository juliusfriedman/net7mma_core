using Media.Codecs.Audio.Alaw;
using Media.Codecs.Audio.Mulaw;
using Media.Common;
using Media.Rtp;
using System;

namespace Media.Rtsp.Server.MediaTypes;

/// <summary>
/// Implementation of <see href="https://datatracker.ietf.org/doc/html/rfc7655">rfc7655</see>
/// </summary>
public class RFC7655Media : RtpAudioSink
{
    #region Constants

    /// <summary>
    /// Used to identify the codec
    /// </summary>
    const string RfcEncodingName = "G711";

    /// <summary>
    /// Used to specify the clock rate
    /// </summary>
    const int RfcClockRate = 8000;

    #endregion

    #region Nested Types

    /// <summary>
    /// Specifies the coding to be used
    /// </summary>
    public enum CompandingLaw
    {
        ALaw = 0,
        Mulaw = 1
    }

    #endregion

    /// <summary>
    /// Creates the Codec required based on the parameters specified
    /// </summary>
    /// <param name="name"></param>
    /// <param name="source"></param>
    /// <param name="payloadType"></param>
    /// <param name="channels"></param>
    /// <param name="compandingLaw"></param>
    public RFC7655Media(string name, Uri source, int payloadType, int channels, CompandingLaw compandingLaw) 
        : base(name, source, payloadType, channels, RfcClockRate)
    {
        switch (compandingLaw)
        {
            case CompandingLaw.ALaw:
                Codec = new ALawCodec();
                break;
            case CompandingLaw.Mulaw:
                Codec = new MulawCodec();
                break;
        }
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public override void Start()
    {
        base.Start();

        bool aLaw = Codec is ALawCodec;

        SessionDescription.Add(new Media.Sdp.Lines.RtpMapLine(PayloadType, RfcEncodingName, RfcClockRate, Channels.ToString()));
        SessionDescription.Add(new Media.Sdp.Lines.FormatTypeLine(PayloadType, aLaw ? "complaw=al" : "complaw=mu"));
    }

    /// <summary>
    /// Uses the <see cref="Codec"/> to encode to the data, returns a value indicating if the data was succesfully encoded and queued.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="offset"></param>
    /// <param name="length"></param>
    public bool Packetize(byte[] data, int offset, int length)
    {
        //Always half the size
        Span<byte> encoded = new byte[length / 2];

        //Get the shorts which are the samples
        Span<short> rawSamples = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, short>(new Span<byte>(data, offset, length));

        //Loop all samples and put the output byte as encoded from the input sample

        if(Codec is ALawCodec)
        {
            for(int i = 0, o = 0; i < rawSamples.Length; ++i)
            {
                encoded[o++] = ALawEncoder.LinearToALawSample(rawSamples[i]);
            }
        }
        else
        {
            for (int i = 0, o = 0; i < rawSamples.Length; ++i)
            {
                encoded[o++] = MuLawEncoder.LinearToMuLawSample(rawSamples[i]);
            }
        }

        //Get the context for the payloadType so we can increment the timestamps and sequence numbers.
        var transportContext = RtpClient.GetContextBySourceId(sourceId);

        //Create a frame
        RtpFrame newFrame = new RtpFrame();

        //Create the packet
        RtpPacket newPacket = new RtpPacket(encoded.Length + RtpHeader.Length)
        {
            Timestamp = transportContext.SenderRtpTimestamp,
            SequenceNumber = transportContext.SendSequenceNumber,
            PayloadType = PayloadType,
            Marker = true,
        };

        //Copy the data to the packet
        encoded.CopyTo(newPacket.Payload.ToSpan());

        //Add the packet to the frame
        newFrame.Add(newPacket);

        //Return the value indicating if the frame was queued.
        return m_Frames.TryEnqueue(ref newFrame);
    }
}
