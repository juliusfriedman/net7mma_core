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
    /// Not yet useful.
    /// </summary>
    public byte Reserved;

    /// <summary>
    /// </summary>
    public int DcPredictor;

    /// <summary>
    /// 
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
