
namespace Media.Codec.Png;

internal class PngState
{
    public ulong FileSignature;

    public byte BitDepth;

    public byte ColourType;

    public byte CompressionMethod;

    public byte FilterMethod;

    public byte InterlaceMethod;

    public override bool Equals(object? obj)
         => obj is PngState jpegState && Equals(jpegState);

    public bool Equals(PngState other)
        => FileSignature == other.FileSignature &&
           BitDepth == other.BitDepth &&
           ColourType == other.ColourType &&
           CompressionMethod == other.CompressionMethod &&
           FilterMethod == other.FilterMethod &&
           InterlaceMethod == other.InterlaceMethod;

    public override int GetHashCode()
        => HashCode.Combine(
            FileSignature, 
            BitDepth, 
            ColourType, 
            CompressionMethod, 
            FilterMethod, 
            InterlaceMethod);

    public static bool operator ==(PngState a, PngState b)
        => a.Equals(b);

    public static bool operator !=(PngState a, PngState b)
        => false == a.Equals(b);
}
