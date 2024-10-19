using Codecs.Image;
using Media.Codec;
using Media.Codecs.Image;
using Media.Common;
using System.Numerics;
using System.Text;

namespace Codec.Bmp;

public class BmpImage : Image
{
    private const float DefaultDpi = 96.0f;

    public static BmpImage FromStream(Stream stream)
    {
        BitmapHeader bitmapHeader = new();
        if (BitmapHeader.Length != stream.Read(bitmapHeader.Array, 0, bitmapHeader.Count))
            throw new InvalidOperationException($"Need {BitmapHeader.Length} Bytes for the Bitmap Header");

        switch (bitmapHeader.FileSignature)
        {
            case BitmapHeader.BMFileSignature:
            case BitmapHeader.BAFileSignature:
            case BitmapHeader.CIFileSignature:
            case BitmapHeader.CPFileSignature:
            case BitmapHeader.ICFileSignature:
            case BitmapHeader.PTFileSignature:
                break;
            default:
                throw new InvalidOperationException("Need BM File Header.");
        }

        var fileSize = bitmapHeader.FileSize;

        BitmapInfoHeader header = new();

        if (BitmapInfoHeader.Length != stream.Read(header.Array, 0, header.Count))
            throw new InvalidOperationException($"Need {BitmapInfoHeader.Length} Bytes for the Bitmap Header");

        // 124 - is BITMAPV5INFOHEADER, 56 - is BITMAPV3INFOHEADER, where we ignore the additional values see https://web.archive.org/web/20150127132443/https://forums.adobe.com/message/3272950
        if (!header.IsKnownSize)
            throw new Exception("The information header size is incorrect.");

        //Need to build components based on header for now just use RGB or use a single component.

        var componentCount = header.Planes;

        var componentSize = header.BitCount;

        var components = new MediaComponent[componentCount];

        for (var c = 0; c < componentCount; c++)
            components[c] = new MediaComponent((byte)c, componentSize);

        var image = new BmpImage(new ImageFormat(Binary.SystemByteOrder, DataLayout.Packed, components), header.Width, header.Height);

        stream.Read(image.Data.Array);

        return image;
    }

    #region Fields

    public readonly BitmapHeader BitmapHeader;

    public readonly BitmapInfoHeader BitmapInfoHeader;

    #endregion

    #region Constructors

    public BmpImage(BitmapHeader bitmapHeader, BitmapInfoHeader bitmapInfoHeader, ImageFormat imageFormat)
        : this(imageFormat, bitmapInfoHeader.Width, bitmapInfoHeader.Height)
    {
        BitmapHeader = bitmapHeader;

        BitmapInfoHeader = bitmapInfoHeader;
    }

    public BmpImage(ImageFormat imageFormat, int width, int height)
        : base(imageFormat, width, height, new BmpCodec())
    {
        int headersSize = BitmapHeader.Length + BitmapInfoHeader.Length;
        int fileSize = headersSize + Data.Array.Length;

        BitmapHeader = new BitmapHeader();
        BitmapHeader.FileSignature = BitmapHeader.BMFileSignature;
        BitmapHeader.FileSize = (uint)fileSize;
        BitmapHeader.Reserved = Binary.ReadU32(Encoding.ASCII.GetBytes(ImageFormat.FormatString), 0, false);
        BitmapHeader.DataOffset = (uint)headersSize;

        var compressionFormat = (int)BitmapInfoHeader.CompressionMethodType.RGB;

        // Convert pixels to meters: 1 inch = 0.0254 meters
        float horizontalResolutionMeters = width / DefaultDpi * 0.0254f;
        float verticalResolutionMeters = height / DefaultDpi * 0.0254f;

        // Convert meters to pixels per meter
        int xpelsPerMeter = (int)Math.Round(1.0f / horizontalResolutionMeters);
        int ypelsPerMeter = (int)Math.Round(1.0f / verticalResolutionMeters);

        BitmapInfoHeader = new(width, height, (short)ImageFormat.Components.Length, (short)ImageFormat.Size, compressionFormat, Data.Count, xpelsPerMeter, ypelsPerMeter, 0, 0);
    }

    #endregion

    #region Methods

    public void Save(Stream stream)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream));
        // Write the BMP file header to the stream
        stream.Write(BitmapHeader.Array, BitmapHeader.Offset, BitmapHeader.Count);
        // Write the DIB header to the stream
        stream.Write(BitmapInfoHeader.Array, BitmapInfoHeader.Offset, BitmapInfoHeader.Count);
        // Write the image data to the stream
        stream.Write(Data.Array, Data.Offset, Data.Count);
    }

    public MemorySegment GetPixelDataAt(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            return MemorySegment.Empty;

        // BMP format stores pixels from bottom to top, so we need to adjust the y-coordinate
        int adjustedY = Height - 1 - y;

        // Calculate the byte offset for the pixel data
        int bytesPerPixel = ImageFormat.Length;
        int rowSize = Width * bytesPerPixel;
        int offset = (adjustedY * rowSize) + (x * bytesPerPixel);

        return Data.Slice(offset, ImageFormat.Length);
    }

    public Vector<byte> GetVectorDataAt(int x, int y)
    {
        // BMP format stores pixels from bottom to top, so we need to adjust the y-coordinate
        int adjustedY = Height - 1 - y;

        // Calculate the byte offset for the pixel data
        int bytesPerPixel = ImageFormat.Length;
        int rowSize = Width * bytesPerPixel;
        int offset = (adjustedY * rowSize) + (x * bytesPerPixel);
        offset -= offset % Vector<byte>.Count; // Align the offset to vector size
        return new Vector<byte>(Data.Array, Data.Offset + offset);
    }

    public void SetVectorDataAt(int x, int y, Vector<byte> vectorData)
    {
        // BMP format stores pixels from bottom to top, so we need to adjust the y-coordinate
        int adjustedY = Height - 1 - y;

        // Calculate the byte offset for the pixel data
        int bytesPerPixel = ImageFormat.Length;
        int rowSize = Width * bytesPerPixel;
        int offset = (adjustedY * rowSize) + (x * bytesPerPixel);
        offset -= offset % Vector<byte>.Count; // Align the offset to vector size
        vectorData.CopyTo(new Span<byte>(Data.Array, Data.Offset + offset, Vector<byte>.Count));
    }

    #endregion
}