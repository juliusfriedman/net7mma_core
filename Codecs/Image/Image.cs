using Codecs.Image;
using Media.Codec;
using Media.Codecs.Image;
using Media.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Media.Codecs.Image
{
    public class Image : MediaBuffer
    {
        private const float DefaultDpi = 96.0f;

        #region Statics

        public static Image FromStream(Stream stream)
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

            if (header.Size is not 40 and not 56 and not 124) // 124 - is BITMAPV5INFOHEADER, 56 - is BITMAPV3INFOHEADER, where we ignore the additional values see https://web.archive.org/web/20150127132443/https://forums.adobe.com/message/3272950
            {
                throw new Exception("The information header size is incorrect.");
            }

            //Need to build components based on header for now just use RGB or use a single component.

            var componentCount = header.Planes;

            var componentSize = header.BitCount;

            var components = new MediaComponent[componentCount];

            for (var c = 0; c < componentCount; c++)
                components[c] = new MediaComponent((byte)c, componentSize);

            var image = new Image(new ImageFormat(Binary.SystemByteOrder, DataLayout.Packed, components), header.Width, header.Height);

            stream.Read(image.Data.Array);

            return image;
        }

        //static Image Crop(Image source)

        internal static int CalculateSize(ImageFormat format, int width, int height)
        {
            int size = 0;

            switch (format.DataLayout)
            {
                case DataLayout.Planar:
                    // For planar layout, each component has its own plane
                    for (int i = 0; i < format.Components.Length; i++)
                    {
                        int componentWidth = width >> format.Widths[i];
                        int componentHeight = height >> format.Heights[i];
                        size += componentWidth * componentHeight * format.Components[i].Length;
                    }
                    break;

                case DataLayout.SemiPlanar:
                    var fistComponentLength = format.Components[0].Length;
                    // For semi-planar layout, the first component has its own plane)
                    // the second plane is for the packed components
                    size = width >> format.Widths[0] * height >> format.Heights[0] * fistComponentLength;
                    width >>= format.Widths[1];
                    height >>= format.Heights[1];
                    goto case DataLayout.Packed;
                case DataLayout.Packed:
                    // For packed layout, all components are interleaved
                    size += width * height * format.Length;
                    break;

                default:
                    throw new NotSupportedException($"Data layout {format.DataLayout} is not supported.");
            }

            return size;
        }

        internal int CalculateStride(int alignment = Common.Binary.BytesPerInteger)
        {
            // Calculate the number of bits per pixel
            int bitsPerPixel = ImageFormat.Size;

            // Calculate the number of bytes per row before alignment
            int bytesPerRow = (Width * bitsPerPixel + 7) / Binary.BitsPerByte;

            // Align the stride to the specified alignment
            int stride = (bytesPerRow + alignment - 1) & ~(alignment - 1);

            return stride;
        }

        #endregion

        #region Fields

        public readonly int Width;

        public readonly int Height;

        #endregion

        #region Constructor

        //Needs a subsampling...

        public Image(ImageFormat format, int width, int height)
            : base(format, CalculateSize(format, width, height))
        {
            Width = width;

            Height = height;
        }

        public Image(ImageFormat format, int width, int height, byte[] data)
            : base(format, new MemorySegment(data))
        {
            Width = width;

            Height = height;
        }

        #endregion

        #region Properties

        //Should be Vector<byte>?

        //Assumes component order
        public IEnumerable<MemorySegment> this[int x, int y]
        {
            get
            {
                if (x < 0 || y < 0 || x > Width || y > Height)
                    yield break;

                //Loop each component and return the segment which corresponds to the data at the offset for that component
                for (int c = 0, ce = ImageFormat.Components.Length; c < ce; ++c)
                    yield return new MemorySegment(Data.Array, Data.Offset + CalculateComponentDataOffset(x, y, c), ImageFormat[c].Length);
            }
            set
            {
                if (x < 0 || y < 0 || x > Width || y > Height)
                    return;

                int componentIndex = 0;

                foreach (var componentData in value)
                    SetComponentData(x, y, componentIndex++, componentData);
            }
        }

        public ImageFormat ImageFormat { get { return MediaFormat as ImageFormat; } }

        public int Planes { get { return MediaFormat.Components.Length; } }

        #endregion

        #region Methods

        public void SaveBitmap(Stream stream)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            int width = Width;
            int height = Height;

            var compressionFormat = (int)BitmapInfoHeader.CompressionMethodType.RGB;

            // Convert pixels to meters: 1 inch = 0.0254 meters
            float horizontalResolutionMeters = width / DefaultDpi * 0.0254f;
            float verticalResolutionMeters = height / DefaultDpi * 0.0254f;

            // Convert meters to pixels per meter
            int xpelsPerMeter = (int)Math.Round(1.0f / horizontalResolutionMeters);
            int ypelsPerMeter = (int)Math.Round(1.0f / verticalResolutionMeters);

            // Create a new BitmapInfoHeader based on the ImageFormat
            BitmapInfoHeader header = new(width, height, (short)ImageFormat.Components.Length, (short)ImageFormat.Size, compressionFormat, Data.Count, xpelsPerMeter, ypelsPerMeter, 0, 0);
            SaveBitmap(header, stream);
        }

        public void SaveBitmap(BitmapInfoHeader bitmapInfoHeader, Stream stream)
        {
            int headersSize = BitmapHeader.Length + BitmapInfoHeader.Length;
            int fileSize = headersSize + Data.Array.Length;

            BitmapHeader bitmapHeader = new BitmapHeader();
            bitmapHeader.FileSignature = BitmapHeader.BMFileSignature;
            bitmapHeader.FileSize = (uint)fileSize;
            bitmapHeader.Reserved = Binary.ReadU32(Encoding.ASCII.GetBytes(ImageFormat.FormatString), 0, false);
            bitmapHeader.DataOffset = (uint)headersSize;

            // Write the BMP file header to the stream
            stream.Write(bitmapHeader.Array, bitmapHeader.Offset, bitmapHeader.Count);
            // Write the DIB header to the stream
            stream.Write(bitmapInfoHeader.Array, bitmapInfoHeader.Offset, bitmapInfoHeader.Count);
            // Write the image data to the stream
            stream.Write(Data.Array, Data.Offset, Data.Count);
        }

        public int PlaneWidth(int plane)
        {
            return plane < 0 || plane >= MediaFormat.Components.Length ? -1 : Width >> ImageFormat.Widths[plane];
        }

        public int PlaneHeight(int plane)
        {
            return plane < 0 || plane >= MediaFormat.Components.Length ? -1 : Height >> ImageFormat.Heights[plane];
        }

        /// <summary>
        /// Gets the amount of bits in the given plane
        /// </summary>
        /// <param name="plane"></param>
        /// <returns></returns>
        public int PlaneSize(int plane)
        {
            return plane < 0 || plane >= MediaFormat.Components.Length ? -1 : (PlaneWidth(plane) + PlaneHeight(plane)) * ImageFormat.Size;
        }

        /// <summary>
        /// Gets the amount of bytes in the given plane.
        /// </summary>
        /// <param name="plane"></param>
        /// <returns></returns>
        public int PlaneLength(int plane)
        {
            return plane < 0 || plane >= MediaFormat.Components.Length ? -1 : Binary.BitsToBytes(PlaneSize(plane));
        }

        public Common.MemorySegment GetPlaneData(int plane)
        {
            int offset = 0;
            for (int i = 0; i < plane; i++)
                offset += PlaneLength(i);
            return Data.Slice(offset, PlaneLength(plane));
        }

        public Common.MemorySegment GetPixelDataAt(int x, int y, int plane)
        {
            return GetPlaneData(plane).Slice(y * PlaneWidth(plane) + x, ImageFormat.Length);
        }

        /// <summary>
        /// Calculates the byte offset to component/channel data
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="componentIndex"></param>
        /// <returns></returns>
        public int CalculateComponentDataOffset(int x, int y, int componentIndex)
        {
            // Validate the input parameters
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return -1;

            if (componentIndex < 0 || componentIndex >= ImageFormat.Components.Length)
                return -1;

            // Calculate the offset based on the data layout
            int offset = 0;

            var component = ImageFormat.Components[componentIndex];
            switch (DataLayout)
            {
                case DataLayout.SemiPlanar:
                    if (componentIndex is 0)
                        goto case DataLayout.Planar;
                    offset += Width + Height * component.Length;
                    y >>= 1;
                    x >>= 1;
                    goto case DataLayout.Packed;
                case DataLayout.Planar:
                    // Each component is stored in a separate plane. The offset is calculated based on the plane's width and the pixel's position within the plane.
                    // Calculate the width and height of the plane for the given component
                    int widthSampling = ImageFormat.Widths[componentIndex];
                    int heightSampling = ImageFormat.Heights[componentIndex];

                    int planeWidth = Width >> widthSampling;
                    int planeHeight = Height >> heightSampling;

                    // Calculate the position within the plane
                    int planeX = x >> widthSampling;
                    int planeY = y >> heightSampling;

                    // Calculate the offset within the plane
                    offset = (planeY * planeWidth + planeX) * component.Length;

                    // Add the base offset for the component's plane
                    for (int i = 0; i < componentIndex; i++)
                    {
                        int previousPlaneWidth = Width >> ImageFormat.Widths[i];
                        int previousPlaneHeight = Height >> ImageFormat.Heights[i];
                        offset += previousPlaneWidth * previousPlaneHeight * ImageFormat.Components[i].Length;
                    }
                    break;
                case DataLayout.Packed:
                    // In packed layout, components are interleaved
                    //int componentDataIndex = (y * PlaneWidth(componentIndex) + x) * ImageFormat.Components.Length + componentIndex;
                    //offset += componentDataIndex * component.Length;
                    offset = x + (PlaneHeight(componentIndex) - (y + 1)) * PlaneWidth(componentIndex);
                    break;
            }

            return offset;
        }

        //Gets all component samples for the given coordinate into a SegmentStream (not really useful besides saving on some allocation)

        public SegmentStream GetSampleData(int x, int y)
        {
            var result = new SegmentStream();
            foreach (var component in ImageFormat.Components)
                result.AddMemory(GetComponentData(x, y, component));
            return result;
        }

        // Get the value of a specific component at the given (x, y) coordinates

        public MemorySegment GetComponentData(int x, int y, MediaComponent component)
        {
            int componentIndex = GetComponentIndex(component);

            if (componentIndex < 0) return MemorySegment.Empty;
            //throw new ArgumentException("Invalid component specified.");

            int offset = CalculateComponentDataOffset(x, y, componentIndex);

            if (offset < 0) return MemorySegment.Empty;
            //throw new IndexOutOfRangeException("Coordinates are outside the bounds of the image.");

            return new MemorySegment(Data.Array, Binary.Min(Data.Count, offset), Binary.Min(Data.Count - offset, component.Length));
        }

        public MemorySegment GetComponentData(int x, int y, byte componentId) => GetComponentData(x, y, ImageFormat.GetComponentById(componentId));

        public Vector<byte> GetComponentVector(int x, int y, byte componentId) => GetComponentVector(x, y, GetComponentIndex(componentId));

        public Vector<byte> GetComponentVector(int x, int y, int componentIndex)
        {
            int offset = CalculateComponentDataOffset(x, y, componentIndex);
            offset -= offset % Vector<byte>.Count; // Align the offset to vector size
            return new Vector<byte>(Data.Array, Data.Offset + offset);
        }

        // Set the value of a specific component at the given (x, y) coordinates

        public void SetComponentData(int x, int y, int componentIndex, MemorySegment data)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return; // or throw an exception

            int offset = CalculateComponentDataOffset(x, y, componentIndex);
            if (offset < 0 || offset >= data.Count)
                return; // or throw an exception

            Buffer.BlockCopy(data.Array, data.Offset, Data.Array, Data.Offset + offset, data.Count);
        }

        public void SetComponentVector(int x, int y, int componentIndex, Vector<byte> componentVector)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return; // or throw an exception

            int offset = CalculateComponentDataOffset(x, y, componentIndex);
            if (offset < 0)
                return; // or throw an exception

            int vectorOffset = offset - (offset % Vector<byte>.Count);
            componentVector.CopyTo(new Span<byte>(Data.Array, Data.Offset + vectorOffset, Vector<byte>.Count));
        }

        public void SetComponentData(int x, int y, byte componentId, MemorySegment data) => SetComponentData(x, y, GetComponentIndex(componentId), data);

        public void SetComponentVector(int x, int y, byte componentId, Vector<byte> componentVector) => SetComponentVector(x, y, GetComponentIndex(componentId), componentVector);

        public void Fill(byte value) => Array.Fill(Data.Array, value, Data.Offset, Data.Count);

        public void FillVector(byte value)
        {
            var filledVector = new Vector<byte>(value);
            for (int i = 0; i < Data.Count; i += Vector<byte>.Count)
                filledVector.CopyTo(new Span<byte>(Data.Array, Data.Offset + i, Vector<byte>.Count));
        }

        #endregion
    }
}


namespace Media.UnitTests
{
    /// <summary>
    /// Provides tests which ensure the logic of the Image class is correct
    /// </summary>
    internal class ImageUnitTests
    {
        public static void TestConstructor()
        {
            foreach (var dataLayout in Enum.GetValues<DataLayout>())
            {
                if (dataLayout == DataLayout.Unknown) continue;

                using (Media.Codecs.Image.Image image = new(Media.Codecs.Image.ImageFormat.RGB(8, Binary.SystemByteOrder, dataLayout), 1, 1))
                {
                    if (image.SampleCount != 1) throw new System.InvalidOperationException();

                    if (image.Data.Count != Image.CalculateSize(image.ImageFormat, image.Width, image.Height)) throw new System.InvalidOperationException();
                }
            }
        }

        public static void Test_GetComponentData_SetComponentData()
        {
            foreach (var dataLayout in Enum.GetValues<DataLayout>())
            {
                if (dataLayout == DataLayout.Unknown) continue;

                System.DateTime start = System.DateTime.UtcNow, end;

                using (var image = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.RGB(8, Binary.SystemByteOrder, dataLayout), 50, 50))
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        for (int y = 0; y < image.Height; y++)
                        {
                            for (int c = 0; c < image.ImageFormat.Length; c++)
                            {
                                var component = image.ImageFormat[c];

                                var data = image.GetComponentData(x, y, component);

                                if (data.Count != component.Length) throw new InvalidOperationException();

                                Array.Fill(data.Array, byte.MaxValue);

                                image.SetComponentData(x, y, c, data);
                            }
                        }
                    }

                    end = System.DateTime.UtcNow;

                    System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms for image DataLayout=" + dataLayout + " width,height=" + image.Width + "," + image.Height);

                    if (image.Data.Array.Any(b => b != byte.MaxValue)) throw new Exception($"Did not set Component data for DataLayout={dataLayout}");
                }
            }
        }

        public static void Test_GetComponentVector_SetComponentVector()
        {
            System.DateTime start = System.DateTime.UtcNow, end;

            var filledVector = (Vector<float>)new Vector<byte>(byte.MaxValue);

            foreach (var dataLayout in Enum.GetValues<DataLayout>())
            {
                if (dataLayout == DataLayout.Unknown) continue;

                using (Media.Codecs.Image.ImageFormat imageFormat = new(Media.Codecs.Image.ImageFormat.RGB(8, Binary.SystemByteOrder, dataLayout)))
                {
                    using (var image = new Codecs.Image.Image(imageFormat, 32, 32))
                    {
                        for (int c = 0; c < image.ImageFormat.Length; c++)
                        {
                            for (int x = 0; x < image.Width; x += Vector<float>.Count)
                            {
                                for (int y = 0; y < image.Height; ++y)
                                {
                                    // Set the modified data back to the image
                                    image.SetComponentVector(x, y, c, Vector.AsVectorByte(filledVector));
                                    image.SetComponentVector(x + 1, y, c, Vector.AsVectorByte(filledVector));
                                    image.SetComponentVector(x + 2, y, c, Vector.AsVectorByte(filledVector));
                                }
                            }
                        }

                        end = System.DateTime.UtcNow;

                        System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms for image " + dataLayout + " width,height=" + image.Width + "," + image.Height);

                        //if (image.Data.Array.Any(b => b != byte.MaxValue)) throw new InvalidOperationException($"Did not set Component Data {dataLayout} (Vector)");
                    }
                }
            }
        }

        public static void Test_Get_Set_Indexer()
        {
            foreach (var dataLayout in Enum.GetValues<DataLayout>())
            {
                if (dataLayout == DataLayout.Unknown) continue;

                System.DateTime start = System.DateTime.UtcNow, end;

                using (var image = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.RGB(8, Binary.SystemByteOrder, dataLayout), 50, 50))
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        for (int y = 0; y < image.Height; y++)
                        {
                            var data = image[x, y];

                            foreach (var componentData in data)
                                Array.Fill(componentData.Array, byte.MaxValue);

                            image[x, y] = data;
                        }
                    }

                    end = System.DateTime.UtcNow;

                    System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms for image width,height=" + image.Width + "," + image.Height);

                    if (image.Data.Array.Any(b => b != byte.MaxValue)) throw new Exception("Did not set Component data");
                }
            }
        }

        public static void TestSave()
        {
            var currentPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            var outputDirectory = Directory.CreateDirectory(Path.Combine(currentPath, "Media", "BmpTest", "output"));

            foreach (var dataLayout in Enum.GetValues<DataLayout>())
            {
                if (dataLayout == DataLayout.Unknown) continue;

                using (var image = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.RGB(8, Binary.SystemByteOrder, dataLayout), 696, 564))
                {
                    for (int i = 0; i < image.Width; i += 2)
                    {
                        for (int j = 0; j < image.Height; j += 2)
                        {
                            for (int c = 0; c < image.ImageFormat.Components.Length; ++c)
                            {
                                var data = image.GetComponentData(i, j, image.ImageFormat[c]);

                                Array.Fill(data.Array, byte.MaxValue, data.Offset, data.Count);

                                image.SetComponentData(i, j, c, data);
                            }
                        }
                    }

                    using (var outputBmpStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, $"rgb_{dataLayout}.bmp"), FileMode.OpenOrCreate))
                    {
                        image.SaveBitmap(outputBmpStream);
                    }

                    using (var inputBmp = Codecs.Image.Image.FromStream(new System.IO.FileStream(Path.Combine(outputDirectory.FullName, $"rgb_{dataLayout}.bmp"), FileMode.OpenOrCreate)))
                    {
                        if (inputBmp.Width != 696) throw new Exception();

                        if (inputBmp.Height != 564) throw new Exception();
                    }
                }
            }

            foreach (var dataLayout in Enum.GetValues<DataLayout>())
            {
                if (dataLayout == DataLayout.Unknown) continue;

                using (var image = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.BGR(8, Binary.SystemByteOrder, dataLayout), 696, 564))
                {
                    for (int i = 0; i < image.Width; i += 2)
                    {
                        for (int j = 0; j < image.Height; j += 2)
                        {
                            for (int c = 0; c < image.ImageFormat.Components.Length; ++c)
                            {
                                var data = image.GetComponentVector(i, j, c);

                                data = Vector<byte>.One * byte.MaxValue;

                                image.SetComponentVector(i, j, c, data);
                            }
                        }
                    }

                    using (var outputBmpStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, $"BGR_Vector_{dataLayout}.bmp"), FileMode.OpenOrCreate))
                    {
                        image.SaveBitmap(outputBmpStream);
                    }
                }
            }

            using (var image = new Codecs.Image.Image(ImageFormat.Binary(8), 696, 564))
            {
                for (int i = 0; i < image.Width; i += 4)
                {
                    for (int j = 0; j < image.Height; j += 4)
                    {
                        for (int c = 0; c < image.ImageFormat.Components.Length; ++c)
                        {
                            var data = image.GetComponentVector(i, j, c);

                            data = Vector<byte>.One * byte.MaxValue;

                            image.SetComponentVector(i, j, c, data);
                        }
                    }
                }

                using (var outputBmpStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "Binary_packed_line2.bmp"), FileMode.OpenOrCreate))
                {
                    image.SaveBitmap(outputBmpStream);
                }
            }

            using (var image = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.RGB(8), 696, 564))
            {
                for (int i = 0; i < image.Width; i += 4)
                {
                    for (int j = 0; j < image.Height; j += 4)
                    {
                        for (int c = 0; c < image.ImageFormat.Components.Length; ++c)
                        {
                            var data = image.GetComponentVector(i, j, c);

                            data = Vector<byte>.One * byte.MaxValue;

                            image.SetComponentVector(i, j, c, data);
                        }
                    }
                }

                using (var outputBmpStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "rgb24_packed_line2.bmp"), FileMode.OpenOrCreate))
                {
                    image.SaveBitmap(outputBmpStream);
                }
            }

            using (var image = new Codecs.Image.Image(Media.Codecs.Image.ImageFormat.RGB(8), 696, 564))
            {
                for (int i = 0; i < image.Width; i += 16)
                {
                    for (int j = 0; j < image.Height; j += 16)
                    {
                        for (int c = 0; c < image.ImageFormat.Length; ++c)
                        {
                            var data = image.GetComponentVector(i, j, c);

                            data = Vector<byte>.One * byte.MaxValue;

                            image.SetComponentVector(i, j, c, data);
                        }
                    }
                }

                using (var outputBmpStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, "rgb24_packed_line3.bmp"), FileMode.OpenOrCreate))
                {
                    image.SaveBitmap(outputBmpStream);
                }
            }
        }

        public static void TestLoad()
        {
            string currentPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            using var grayStream = new FileStream(Path.Combine(currentPath, "Media", "BmpTest", "lena_gray.bmp"), FileMode.Open, FileAccess.Read);
            using var grayImage = Codecs.Image.Image.FromStream(grayStream);

            using var colorStream = new FileStream(Path.Combine(currentPath, "Media", "BmpTest", "lena_color.bmp"), FileMode.Open, FileAccess.Read);
            using var colorImage = Codecs.Image.Image.FromStream(colorStream);

            if (colorImage.Width != grayImage.Width) throw new InvalidOperationException();
            if (colorImage.Height != grayImage.Height) throw new InvalidOperationException();

            var outputGray = new FileStream(Path.Combine(currentPath, "Media", "BmpTest", "output", "lena_gray_save.bmp"), FileMode.OpenOrCreate, FileAccess.Write);
            grayImage.SaveBitmap(outputGray);

            var outputColor = new FileStream(Path.Combine(currentPath, "Media", "BmpTest", "output", "lena_color_save.bmp"), FileMode.OpenOrCreate, FileAccess.Write);
            colorImage.SaveBitmap(outputColor);

            var newImage = new Codecs.Image.Image(Codecs.Image.ImageFormat.RGB(8), grayImage.Width, grayImage.Height);
            grayImage.Data.CopyTo(newImage.Data);

            var outputNew = new FileStream(Path.Combine(currentPath, "Media", "BmpTest", "output", "lena_gray_new_save.bmp"), FileMode.OpenOrCreate, FileAccess.Write);
            newImage.SaveBitmap(outputNew);
        }

        public void Test_CalculateComponentDataOffset_PlanarLayout()
        {
            var imageFormat = Media.Codecs.Image.ImageFormat.RGB(8, Binary.ByteOrder.Little, DataLayout.Planar);
            var image = new Codecs.Image.Image(imageFormat, 640, 480);

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    for (int componentIndex = 0; componentIndex < imageFormat.Length; componentIndex++)
                    {
                        int widthSampling = imageFormat.Widths[componentIndex];
                        int heightSampling = imageFormat.Heights[componentIndex];

                        int planeWidth = image.Width >> widthSampling;
                        int planeX = x >> widthSampling;
                        int planeY = y >> heightSampling;

                        int expectedOffset = (planeY * planeWidth + planeX) * imageFormat.Components[componentIndex].Length;

                        for (int i = 0; i < componentIndex; i++)
                        {
                            int previousPlaneWidth = image.Width >> image.ImageFormat.Widths[i];
                            int previousPlaneHeight = image.Height >> image.ImageFormat.Heights[i];
                            expectedOffset += previousPlaneWidth * previousPlaneHeight * image.ImageFormat.Components[i].Length;
                        }

                        int calculatedOffset = image.CalculateComponentDataOffset(x, y, componentIndex);

                        if (expectedOffset != calculatedOffset) throw new InvalidOperationException();
                    }
                }
            }
        }

        public void Test_CalculateComponentDataOffset_SemiPlanarLayout()
        {
            var imageFormat = Media.Codecs.Image.ImageFormat.YUV(8, Binary.ByteOrder.Little, DataLayout.SemiPlanar);
            var image = new Codecs.Image.Image(imageFormat, 640, 480);

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    for (int componentIndex = 0; componentIndex < imageFormat.Length; componentIndex++)
                    {
                        var component = imageFormat[componentIndex];

                        int expectedOffset = 0;
                        switch (componentIndex)
                        {
                            case 0:
                                // Calculate the width and height of the plane for the given component
                                int widthSampling = image.ImageFormat.Widths[componentIndex];
                                int heightSampling = image.ImageFormat.Heights[componentIndex];

                                int planeWidth = image.Width >> widthSampling;
                                int planeHeight = image.Height >> heightSampling;

                                // Calculate the position within the plane
                                int planeX = x >> widthSampling;
                                int planeY = y >> heightSampling;

                                // Calculate the offset within the plane
                                expectedOffset += (planeY * planeWidth + planeX) * component.Length;
                                break;
                            default:
                                expectedOffset += image.Width + image.Height * component.Length;
                                var Y = y >> 1;
                                var X = x >> 1;
                                expectedOffset = X + (image.PlaneHeight(componentIndex) - (Y + 1)) * image.PlaneWidth(componentIndex);
                                break;
                        }

                        int calculatedOffset = image.CalculateComponentDataOffset(x, y, componentIndex);

                        if (expectedOffset != calculatedOffset) throw new InvalidOperationException();
                    }
                }
            }
        }

        public void Test_CalculateComponentDataOffset_PackedLayout()
        {
            var imageFormat = Media.Codecs.Image.ImageFormat.BGR(8);
            var image = new Codecs.Image.Image(imageFormat, 640, 480);

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    for (int componentIndex = 0; componentIndex < imageFormat.Components.Length; componentIndex++)
                    {
                        var component = image.ImageFormat.Components[componentIndex];

                        int expectedOffset = x + (image.PlaneHeight(componentIndex) - (y + 1)) * image.PlaneWidth(componentIndex);

                        int calculatedOffset = image.CalculateComponentDataOffset(x, y, componentIndex);

                        if (expectedOffset != calculatedOffset) throw new InvalidOperationException();
                    }
                }
            }
        }

        public static void TestConversionRGB()
        {
            int testHeight = 1920, testWidth = 1080;

            System.DateTime start = System.DateTime.UtcNow, end;

            //Create the source image
            using (Media.Codecs.Image.Image rgbImage = new(Media.Codecs.Image.ImageFormat.RGB(8), testWidth, testHeight))
            {
                if (rgbImage.ImageFormat.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                //Create the ImageFormat based on YUV packed but in Planar format with a full height luma plane and half hight chroma planes
                var Yuv420P = new Codecs.Image.ImageFormat(Media.Codecs.Image.ImageFormat.YUV(8, Common.Binary.ByteOrder.Little, Codec.DataLayout.Planar), new int[] { 0, 1, 1 });

                if (Yuv420P.IsInterleaved) throw new System.Exception("IsInterleaved should be false");

                if (Yuv420P.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                //ImageFormat could be given directly to constructor here

                //Create the destination image
                using (Media.Codecs.Image.Image yuvImage = new(Yuv420P, testWidth, testHeight))
                {

                    //Cache the data of the source before transformation
                    byte[] left = rgbImage.Data.ToArray(), right;

                    //Transform RGB to YUV

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.RgbToYuvImageTransformation(rgbImage, yuvImage))
                    {
                        start = System.DateTime.UtcNow;

                        it.Transform();

                        end = System.DateTime.UtcNow;

                        System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms");

                        //Yuv Data
                        //left = dest.Data.ToArray();
                    }

                    //Transform YUV to RGB

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.YuvToRgbTransformation(yuvImage, rgbImage))
                    {
                        start = System.DateTime.UtcNow;

                        it.Transform();

                        end = System.DateTime.UtcNow;

                        System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms");

                        //Rgb Data
                        right = rgbImage.Data.ToArray();
                    }

                    //Compare the two sequences
                    if (false == left.SequenceEqual(right)) throw new System.InvalidOperationException();

                    //Draw some lines
                    for (int i = 0; i < yuvImage.Width; i += 16)
                    {
                        for (int j = 0; j < yuvImage.Height; j += 16)
                        {
                            for (int c = 0; c < yuvImage.ImageFormat.Length; ++c)
                            {
                                var data = yuvImage.GetComponentVector(i, j, c);

                                data = Vector<byte>.One * byte.MaxValue;

                                yuvImage.SetComponentVector(i, j, c, data);
                            }
                        }
                    }

                    var currentPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

                    var outputDirectory = Directory.CreateDirectory(Path.Combine(currentPath, "Media", "BmpTest", "output"));

                    using (var outputStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, $"Yuv420P_{Yuv420P.DataLayout}.yuv"), FileMode.OpenOrCreate))
                    {
                        outputStream.Write(yuvImage.Data.Array, yuvImage.Data.Offset, yuvImage.Data.Count);
                    }
                }

                //Done with the format.
                Yuv420P = null;
            }
        }

        public static void TestVectorizedConversionRGB()
        {
            int testHeight = 1920, testWidth = 1080;

            System.DateTime start = System.DateTime.UtcNow, end;

            //Create the source image
            using (Media.Codecs.Image.Image rgbImage = new(Media.Codecs.Image.ImageFormat.RGB(8), testWidth, testHeight))
            {
                if (rgbImage.ImageFormat.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                //Create the ImageFormat based on YUV packed but in Planar format with a full height luma plane and half hight chroma planes
                Media.Codecs.Image.ImageFormat Yuv420P = new(Media.Codecs.Image.ImageFormat.YUV(8, Common.Binary.ByteOrder.Little, Codec.DataLayout.Planar), new int[] { 0, 1, 1 });

                if (Yuv420P.IsInterleaved) throw new System.Exception("IsInterleaved should be false");

                if (Yuv420P.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                //ImageFormat could be given directly to constructor here

                //Create the destination image
                using (Media.Codecs.Image.Image yuvImage = new(Yuv420P, testWidth, testHeight))
                {

                    //Cache the data of the source before transformation
                    byte[] left = rgbImage.Data.ToArray(), right;

                    //Transform RGB to YUV

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.VectorizedRgbToYuvImageTransformation(rgbImage, yuvImage))
                    {
                        start = System.DateTime.UtcNow;

                        it.Transform();

                        end = System.DateTime.UtcNow;

                        System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms");

                        //Yuv Data
                        //left = dest.Data.ToArray();
                    }

                    //Transform YUV to RGB

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.VectorizedYuvToRgbTransformation(yuvImage, rgbImage))
                    {
                        start = System.DateTime.UtcNow;

                        it.Transform();

                        end = System.DateTime.UtcNow;

                        System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms");

                        //Rgb Data
                        right = rgbImage.Data.ToArray();
                    }

                    //Compare the two sequences
                    if (false == left.SequenceEqual(right)) throw new System.InvalidOperationException();
                }

                //Done with the format.
                Yuv420P = null;
            }
        }

        //Averages around 100 msec, 0.1 sec, still 60 times would be 600 msec or .6 seconds, just fast enough
        public static void TestUnsafeConversionRGB()
        {
            int testHeight = 1920, testWidth = 1080;

            System.DateTime start = System.DateTime.UtcNow, end;

            Codecs.Image.Image yuvImage;

            //Create the source image
            using (Media.Codecs.Image.Image rgbImage = new(Media.Codecs.Image.ImageFormat.RGB(8), testWidth, testHeight))
            {
                if (rgbImage.ImageFormat.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                //Create the ImageFormat based on YUV packed but in Planar format with a full height luma plane and half hight chroma planes
                Media.Codecs.Image.ImageFormat Yuv420P = new(Media.Codecs.Image.ImageFormat.YUV(8, Common.Binary.ByteOrder.Little, Codec.DataLayout.Planar), new int[] { 0, 1, 1 });

                if (Yuv420P.IsInterleaved) throw new System.Exception("IsInterleaved should be false");

                if (Yuv420P.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                //ImageFormat could be given directly to constructor here

                //Create the destination image

                //Cache the data of the source before transformation
                byte[] left = rgbImage.Data.ToArray(), right;

                //Transform RGB to YUV

                start = System.DateTime.UtcNow;

                unsafe
                {
                    fixed (byte* ptr = rgbImage.Data.Array)
                    {
                        yuvImage = new Codecs.Image.Image(Yuv420P, testWidth, testHeight, Media.Codecs.Image.ColorConversions.RGBToYUV420Managed(rgbImage.Width, rgbImage.Height, (nint)ptr));
                    }

                }

                end = System.DateTime.UtcNow;

                System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms");

                //Transform YUV to RGB

                start = System.DateTime.UtcNow;

                //it.Transform();

                Media.Codecs.Image.ColorConversions.YUV2RGBManaged(yuvImage.Data.Array, rgbImage.Data.Array, rgbImage.Width >> 1, rgbImage.Height >> 1);

                end = System.DateTime.UtcNow;

                System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms");

                //Rgb Data
                right = rgbImage.Data.ToArray();

                //Compare the two sequences
                //if (false == left.SequenceEqual(right)) throw new System.InvalidOperationException();

                //Done with the format.
                Yuv420P = null;
            }
        }

        public static void TestConversionARGB()
        {
            //Create the source image
            using (Media.Codecs.Image.Image rgbImage = new(Media.Codecs.Image.ImageFormat.ARGB(8), 8, 8))
            {
                //Create the ImageFormat based on YUV packed but in Planar format with a full height luma plane and half hight chroma planes
                Media.Codecs.Image.ImageFormat Yuv420P = new(Media.Codecs.Image.ImageFormat.YUV(8, Common.Binary.ByteOrder.Little, Codec.DataLayout.Planar), new int[] { 0, 1, 1 });

                if (Yuv420P.IsInterleaved) throw new System.Exception("IsInterleaved should be false");

                if (Yuv420P.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                if (false == rgbImage.ImageFormat.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be true");

                //ImageFormat be given directly to constructor here

                //Create the destination image
                using (Media.Codecs.Image.Image yuvImage = new(Yuv420P, 8, 8))
                {

                    //Cache the data of the source before transformation
                    byte[] left = rgbImage.Data.ToArray(), right;

                    //Transform RGB to YUV

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.RgbToYuvImageTransformation(rgbImage, yuvImage))
                    {
                        it.Transform();

                        //Yuv Data
                        //left = dest.Data.ToArray();
                    }

                    //Transform YUV to RGB

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.YuvToRgbTransformation(yuvImage, rgbImage))
                    {
                        it.Transform();

                        //Rgb Data
                        right = rgbImage.Data.ToArray();
                    }

                    //Compare the two sequences
                    if (false == left.SequenceEqual(right)) throw new System.InvalidOperationException();
                }

                //Done with the format.
                Yuv420P = null;
            }
        }

        public static void TestConversionRGBA()
        {
            //Create the source image
            using (Media.Codecs.Image.Image rgbImage = new(Media.Codecs.Image.ImageFormat.RGBA(8), 8, 8))
            {
                //Create the ImageFormat based on YUV packed but in Planar format with a full height luma plane and half hight chroma planes
                Media.Codecs.Image.ImageFormat Yuv420P = new(Media.Codecs.Image.ImageFormat.YUV(8, Common.Binary.ByteOrder.Little, Codec.DataLayout.Planar), new int[] { 0, 1, 1 });

                if (Yuv420P.IsInterleaved) throw new System.Exception("IsInterleaved should be false");

                if (Yuv420P.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                if (false == rgbImage.ImageFormat.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be true");

                //ImageFormat could be given directly to constructor here

                //Create the destination image
                using (Media.Codecs.Image.Image yuvImage = new(Yuv420P, 8, 8))
                {

                    //Cache the data of the source before transformation
                    byte[] left = rgbImage.Data.ToArray(), right;

                    //Transform RGB to YUV

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.RgbToYuvImageTransformation(rgbImage, yuvImage))
                    {
                        it.Transform();

                        //Yuv Data
                        //left = dest.Data.ToArray();
                    }

                    //Transform YUV to RGB

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.YuvToRgbTransformation(yuvImage, rgbImage))
                    {
                        it.Transform();

                        //Rgb Data
                        right = rgbImage.Data.ToArray();
                    }

                    //Compare the two sequences
                    if (false == left.SequenceEqual(right)) throw new System.InvalidOperationException();
                }

                //Done with the format.
                Yuv420P = null;
            }
        }

        public static void TestConversionRGAB()
        {

            Codecs.Image.ImageFormat weird = new(Common.Binary.ByteOrder.Little, Codec.DataLayout.Packed, new Codec.MediaComponent[]
            {
                new((byte)'r', 10),
                new((byte)'g', 10),
                new((byte)'a', 2),
                new((byte)'b', 10),

            });

            //Create the source image
            using (Media.Codecs.Image.Image rgbImage = new(weird, 8, 8))
            {
                //Create the ImageFormat based on YUV packed but in Planar format with a full height luma plane and half hight chroma planes
                Media.Codecs.Image.ImageFormat Yuv420P = new(Media.Codecs.Image.ImageFormat.YUV(8, Common.Binary.ByteOrder.Little, Codec.DataLayout.Planar), new int[] { 0, 1, 1 });

                if (Yuv420P.IsInterleaved) throw new System.Exception("IsInterleaved should be false");

                if (Yuv420P.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be false");

                if (false == rgbImage.ImageFormat.HasAlphaComponent) throw new System.Exception("HasAlphaComponent should be true");

                if (rgbImage.ImageFormat.AlphaComponent.Size != 2) throw new System.Exception("AlphaComponent.Size should be 2 (bits)");

                //ImageFormat could be given directly to constructor here

                //Create the destination image
                using (Media.Codecs.Image.Image yuvImage = new(Yuv420P, 8, 8))
                {

                    //Cache the data of the source before transformation
                    byte[] left = rgbImage.Data.ToArray(), right;

                    //Transform RGB to YUV

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.RgbToYuvImageTransformation(rgbImage, yuvImage))
                    {
                        it.Transform();

                        //Yuv Data
                        //left = dest.Data.ToArray();
                    }

                    //Transform YUV to RGB

                    using (Media.Codecs.Image.ImageTransformation it = new Media.Codecs.Image.Transformations.YuvToRgbTransformation(yuvImage, rgbImage))
                    {
                        it.Transform();

                        //Rgb Data
                        right = rgbImage.Data.ToArray();
                    }

                    //Compare the two sequences
                    if (false == left.SequenceEqual(right)) throw new System.InvalidOperationException();
                }

                //Done with the format.
                Yuv420P = null;
            }
        }

        public static void TestFillVector()
        {
            System.DateTime start = System.DateTime.UtcNow, end;

            Media.Codecs.Image.ImageFormat Yuv420P = new(Media.Codecs.Image.ImageFormat.YUV(8, Common.Binary.ByteOrder.Little, Codec.DataLayout.Planar), new int[] { 0, 1, 1 });

            using (var image = new Codecs.Image.Image(Yuv420P, 32, 32))
            {
                image.FillVector(byte.MaxValue);

                end = System.DateTime.UtcNow;

                System.Console.WriteLine("Took: " + (end - start).TotalMilliseconds.ToString() + " ms for image width,height=" + image.Width + "," + image.Height);

                if (image.Data.Array.Any(b => b != byte.MaxValue)) throw new InvalidOperationException("Did not set Component data (Vector)");
            }
        }

        public static void TestCalculateSize()
        {
            var format = ImageFormat.RGB(8);
            int width = 100;
            int height = 100;
            int size = Image.CalculateSize(format, width, height);
            Console.WriteLine(width * height * format.Length == size ? "Pass" : "Fail");
        }

        public static void TestPlaneDimensions()
        {
            var format = ImageFormat.RGB(8);
            var image = new Image(format, 100, 100);
            int planeWidth = image.PlaneWidth(0);
            int planeHeight = image.PlaneHeight(0);
            Console.WriteLine(planeWidth == 100 && planeHeight == 100 ? "Pass" : "Fail");
        }

        public static void TestSaveBitmap()
        {
            var format = ImageFormat.RGB(8);
            var image = new Image(format, 100, 100);
            using (var stream = new MemoryStream())
            {
                image.SaveBitmap(stream);
                Console.WriteLine(stream.Length > 0 ? "Pass" : "Fail");
            }
        }

        public static void TestFill()
        {
            var format = ImageFormat.RGB(8);
            var image = new Image(format, 100, 100);
            image.Fill(255);
            var data = image.GetComponentData(0, 0, format.Components[0]);
            Console.WriteLine(data.All(b => b == 255) ? "Pass" : "Fail");
        }

        public static void TestSetComponentData()
        {
            var format = ImageFormat.RGB(8);
            var image = new Image(format, 100, 100);
            var data = new MemorySegment(image.ImageFormat.Length);
            image.SetComponentData(0, 0, 0, data);
            var retrievedData = image[0, 0];
            Console.WriteLine(retrievedData.SelectMany(arg => arg).SequenceEqual(data) ? "Pass" : "Fail");
        }

        public void CalculateStride_RGB8_ReturnsCorrectStride()
        {
            // Arrange
            int width = 1920;
            int height = 1080;
            var imageFormat = ImageFormat.RGB(8);
            var image = new Image(imageFormat, width, height);

            // Act
            int stride = image.CalculateStride();

            // Assert
            int expectedStride = ((width * 3) + 3) & ~3; // 3 bytes per pixel for RGB8, aligned to 4 bytes
            System.Diagnostics.Debug.Assert(expectedStride == stride);
        }

        public void CalculateStride_ARGB8_ReturnsCorrectStride()
        {
            // Arrange
            int width = 1920;
            int height = 1080;
            var imageFormat = ImageFormat.ARGB(8);
            var image = new Image(imageFormat, width, height);

            // Act
            int stride = image.CalculateStride();

            // Assert
            int expectedStride = ((width * Common.Binary.BytesPerInteger) + 3) & ~3; // 4 bytes per pixel for ARGB8, aligned to 4 bytes
            System.Diagnostics.Debug.Assert(expectedStride == stride);
        }

        public void CalculateStride_Monochrome1_ReturnsCorrectStride()
        {
            // Arrange
            int width = 1920;
            int height = 1080;
            var imageFormat = ImageFormat.Monochrome(1);
            var image = new Image(imageFormat, width, height);

            // Act
            int stride = image.CalculateStride();

            // Assert
            int rowSize = (width + 7) / Common.Binary.BitsPerByte; // 1 bit per pixel, convert width to bytes
            int expectedStride = (rowSize + 3) & ~3; // Align to 4 bytes
            System.Diagnostics.Debug.Assert(expectedStride == stride);
        }

        public void Test_GetPlaneData_ValidComponentIndex_ReturnsCorrectData()
        {
            // Arrange
            int width = 1920;
            int height = 1080;
            var imageFormat = ImageFormat.RGB(24, Common.Binary.ByteOrder.Little, DataLayout.Planar);
            var image = new Image(imageFormat, width, height);

            // Act
            var planeData = image.GetPlaneData(0);

            // Assert
            System.Diagnostics.Debug.Assert(planeData != null && planeData.Count > 0);
            System.Diagnostics.Debug.Assert(image.PlaneLength(0) == planeData.Count);
        }

        public void Test_GetPlaneData_InvalidComponentIndex_ThrowsException()
        {
            // Arrange
            int width = 1920;
            int height = 1080;
            var imageFormat = ImageFormat.RGB(24, Common.Binary.ByteOrder.Little, DataLayout.Planar);
            var image = new Image(imageFormat, width, height);

            // Act & Assert
            try { image.GetPlaneData(-1); }
            catch (ArgumentOutOfRangeException) { }
            try { image.GetPlaneData(3); }
            catch (ArgumentOutOfRangeException) { }
        }

        public void Test_GetPixelDataAt_ValidCoordinates_ReturnsCorrectData()
        {
            // Arrange
            int width = 1920;
            int height = 1080;
            var imageFormat = ImageFormat.RGB(8, Common.Binary.ByteOrder.Little, DataLayout.Packed);
            var image = new Image(imageFormat, width, height);

            // Act
            var pixelData = image.GetPixelDataAt(100, 100, 0);
            pixelData.Fill(byte.MaxValue);

            //Lower left
            pixelData = image.GetPixelDataAt(0, 0, 0);
            pixelData.Fill(byte.MaxValue);

            //Upper right
            pixelData = image.GetPixelDataAt(width - 1, height -1, 0);
            pixelData.Fill(byte.MaxValue);

            // Assert
            System.Diagnostics.Debug.Assert(pixelData != null);
            System.Diagnostics.Debug.Assert(imageFormat.Length == pixelData.Count);

            foreach (var component in image[101, 101])
                component.Fill(byte.MaxValue);

            // Save
            var currentPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            var outputDirectory = Directory.CreateDirectory(Path.Combine(currentPath, "Media", "BmpTest", "output"));

            using (var outputBmpStream = new System.IO.FileStream(Path.Combine(outputDirectory.FullName, $"rgb24_{imageFormat.DataLayout}.bmp"), FileMode.OpenOrCreate))
            {
                image.SaveBitmap(outputBmpStream);
            }
        }

        public void Test_GetPixelDataAt_InvalidCoordinates_ThrowsException()
        {
            // Arrange
            int width = 1920;
            int height = 1080;
            var imageFormat = ImageFormat.RGB(24, Common.Binary.ByteOrder.Little, DataLayout.Packed);
            var image = new Image(imageFormat, width, height);

            // Act & Assert
            try { image.GetPixelDataAt(-1, 100, 0); }
            catch (ArgumentOutOfRangeException) { }
            try { image.GetPixelDataAt(100, -1, 0); }
            catch (ArgumentOutOfRangeException) { }
            try { image.GetPixelDataAt(1920, 100, 0); }
            catch (ArgumentOutOfRangeException) { }
            try { image.GetPixelDataAt(100, 1080, 0); }
            catch (ArgumentOutOfRangeException) { }
        }
    }
}