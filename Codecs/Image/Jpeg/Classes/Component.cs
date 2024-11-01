using Codecs.Image;

namespace Media.Codec.Jpeg.Classes;

public class Component : MediaComponent
{
    /// <summary>
    /// Quantization table destination selector
    /// </summary>
    public byte Tqi;

    /// <summary>
    /// DC Entropy encoding table destination selector
    /// </summary>
    public byte Tdj;

    /// <summary>
    /// AC Entropy coding table destination selector.
    /// </summary>
    public byte Taj;

    /// <summary>
    /// </summary>
    public int DcPredictor;

    /// <summary>
    /// Gets the horizontal sampling factor.
    /// </summary>
    public byte HorizontalSamplingFactor;

    /// <summary>
    /// Gets the vertical sampling factor.
    /// </summary>
    public byte VerticalSamplingFactor;

    /// <summary>
    /// Gets the number of blocks per line.
    /// </summary>
    public int WidthInBlocks;

    /// <summary>
    /// Gets the number of blocks per column.
    /// </summary>
    public int HeightInBlocks;

    /// <summary>
    /// 
    /// </summary>
    public Size? SizeInBlocks;

    /// <summary>
    /// 
    /// </summary>
    public Size? SubSamplingDivisors;

    /// <summary>
    /// </summary>
    public Size? SamplingFactors;

    /// <summary>
    /// Constructs a <see cref="Component"> with the given quantization table number, id and size.
    /// </summary>
    /// <param name="quantizationTableNumber"></param>
    /// <param name="id"></param>
    /// <param name="size"></param>
    public Component(byte quantizationTableNumber, byte id, int size)
        : base(id, size)
        => Tqi = quantizationTableNumber;
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override int GetHashCode()
        => System.HashCode.Combine(base.GetHashCode(), Tqi, Tdj, Taj);
}
