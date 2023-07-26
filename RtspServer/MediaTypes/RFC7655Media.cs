using Media.Codecs.Audio.Alaw;
using Media.Codecs.Audio.Mulaw;
using Media.Common;
using Media.Rtp;
using System;
using System.Linq;

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
    const string RfcEncodingName = "G711-0";

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

        var mediaDescription = SessionDescription.MediaDescriptions.First();

        mediaDescription.Add(new Media.Sdp.Lines.RtpMapLine(PayloadType, RfcEncodingName, RfcClockRate, Channels.ToString()));
        mediaDescription.Add(new Media.Sdp.Lines.FormatTypeLine(PayloadType, aLaw ? "complaw=al" : "complaw=mu"));
        mediaDescription.Add(new Media.Sdp.Lines.SessionAttributeLine("ptime", "20"));
    }

    /// <summary>
    /// Uses the <see cref="Codec"/> to encode to the data, returns a value indicating if the data was succesfully encoded and queued.
    /// </summary>
    /// <param name="data"></param>
    /// <param name="offset"></param>
    /// <param name="length"></param>
    public bool Packetize(byte[] data, int offset, int length)
    {
        //Get the context for the payloadType so we can increment the timestamps and sequence numbers.
        var transportContext = RtpClient.GetContextBySourceId(sourceId);

        //Create a frame
        RtpFrame newFrame = new RtpFrame();

        //Create the packet
        RtpPacket newPacket = new RtpPacket(length / 2 + RtpHeader.Length)
        {
            Timestamp = transportContext.SenderRtpTimestamp,
            SequenceNumber = transportContext.SendSequenceNumber,
            PayloadType = PayloadType,
            Marker = true,
        };

        //Add the packet to the frame
        newFrame.Add(newPacket);

        //Loop all samples and put the [i]nput bytes into the encoder and [o]utput byte into the Payload

        if (Codec is ALawCodec)
        {
            for(int i = offset, o = 0; i < length; i += 2)
            {
                newPacket.Payload[o++] = ALawEncoder.LinearToALawSample(Common.Binary.Read16(data, i, System.BitConverter.IsLittleEndian));
            }
        }
        else
        {
            for (int i = offset, o = 0; i < length; i += 2)
            {
                newPacket.Payload[o++] = MuLawEncoder.LinearToMuLawSample(Common.Binary.Read16(data, i, System.BitConverter.IsLittleEndian));
            }
        }

        //Return the value indicating if the frame was queued.
        return m_Frames.TryEnqueue(ref newFrame);
    }
}
