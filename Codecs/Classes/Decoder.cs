namespace Media.Codec
{
    public class Decoder : Media.Codec.Interfaces.IDecoder
    {
        public Media.Codec.Interfaces.ICodec Codec { get; protected set; }

        //Write to base stream, seek back and call Decode
        //Decode(byte[], int offset, int length)

        //Decode(int offset, int length)
    }
}
