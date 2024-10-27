
namespace Codec.Png;

internal class PngState
{
    public ulong FileSignature;

    public byte BitDepth;

    public byte ColorType;

    public byte CompressionMethod;

    public byte FilterMethod;

    public byte InterlaceMethod;

    public override bool Equals(object? obj)
         => obj is PngState jpegState && Equals(jpegState);

    public bool Equals(PngState other)
        => FileSignature == other.FileSignature &&
           BitDepth == other.BitDepth &&
           ColorType == other.ColorType &&
           CompressionMethod == other.CompressionMethod &&
           FilterMethod == other.FilterMethod &&
           InterlaceMethod == other.InterlaceMethod;

    public override int GetHashCode()
        => HashCode.Combine(
            FileSignature, 
            BitDepth, 
            ColorType, 
            CompressionMethod, 
            FilterMethod, 
            InterlaceMethod);

    public static bool operator ==(PngState a, PngState b)
        => a.Equals(b);

    public static bool operator !=(PngState a, PngState b)
        => false == a.Equals(b);
}
