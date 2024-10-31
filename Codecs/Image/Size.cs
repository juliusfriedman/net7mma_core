using Media.Common;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Codecs.Image;

/// <summary>
/// Consolidates the concepts of `Size` and `SizeF` into a single type.
/// Represents a 64 bit <see cref="MemorySegment"/>
/// Stored Width, Height.
/// </summary>
public class Size : MemorySegment, IEquatable<Size>
{
    #region Statics

    /// <summary>
    /// Represents a <see cref="Size"/> that has Width and Height values set to zero.
    /// </summary>
    public new static readonly Size Empty = new Size(0, 0);

    #endregion

    #region Constructors

    public Size(MemorySegment memory)
        : base(memory)
    {

    }

    public Size()
        : base(Binary.BitsPerInteger * 2)
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Size"/>.
    /// </summary>
    /// <param name="value">The width and height of the size.</param>
    public Size(float value)
     : this()
    {
        WidthF = value;
        HeightF = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Size"/>.
    /// </summary>
    /// <param name="value">The width and height of the size.</param>
    public Size(int value)
     : this()
    {
        Width = value;
        Height = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Size"/>.
    /// </summary>
    /// <param name="width">The width of the size.</param>
    /// <param name="height">The height of the size.</param>
    public Size(int width, int height)
        : this()
    {
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Size"/>.
    /// </summary>
    /// <param name="width">The width of the size.</param>
    /// <param name="height">The height of the size.</param>
    public Size(float width, float height)
        : this()
    {
        WidthF = width;
        HeightF = height;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Size"/>.
    /// </summary>
    /// <param name="size">The size.</param>
    public Size(Size size)
        : this()
    {
        Width = size.Width;
        Height = size.Height;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the width of this <see cref="Size"/>.
    /// </summary>
    public int Width
    {
        get => Binary.Read32(this, Offset, false);
        set => Binary.Write32(Array, Offset, false, value);
    }

    /// <summary>
    /// Gets or sets the height of this <see cref="Size"/>.
    /// </summary>
    public int Height
    {
        get => Binary.Read32(this, Offset + Binary.BytesPerInteger, false);
        set => Binary.Write32(Array, Offset + Binary.BytesPerInteger, false, value);
    }

    /// <summary>
    /// Gets the 32bit float value of the width.
    /// </summary>
    public float WidthF
    {
        get => Binary.Read32(this, Offset, false);
        set => Binary.Write32(Array, Offset, false, (uint)value);
    }

    /// <summary>
    /// Gets the 32bit float value of the height.
    /// </summary>
    public float HeightF
    {
        get => Binary.Read32(this, Offset + Binary.BytesPerInteger, false);
        set => Binary.Write32(Array, Offset + Binary.BytesPerInteger, false, (uint)value);
    }

    /// <summary>
    /// Gets a value indicating whether this <see cref="Size"/> is empty.
    /// </summary>
    public bool IsEmpty => Equals(Empty);

    #endregion

    #region Methods

    /// <summary>
    /// Performs vector addition of two <see cref="Size"/> objects.
    /// </summary>
    /// <param name="left">The size on the left hand of the operand.</param>
    /// <param name="right">The size on the right hand of the operand.</param>
    /// <returns>The <see cref="Size"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Size Add(Size left, Size right) => new Size(unchecked(left.Width + right.Width), unchecked(left.Height + right.Height));

    /// <summary>
    /// Contracts a <see cref="Size"/> by another <see cref="Size"/>.
    /// </summary>
    /// <param name="left">The size on the left hand of the operand.</param>
    /// <param name="right">The size on the right hand of the operand.</param>
    /// <returns>The <see cref="Size"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Size Subtract(Size left, Size right) => new Size(unchecked(left.Width - right.Width), unchecked(left.Height - right.Height));

    /// <summary>
    /// Converts a <see cref="SizeF"/> to a <see cref="Size"/> by performing a ceiling operation on all the dimensions.
    /// </summary>
    /// <param name="size">The size.</param>
    /// <returns>The <see cref="Size"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Size Ceiling(Size size) => new Size(unchecked((int)MathF.Ceiling(size.WidthF)), unchecked((int)MathF.Ceiling(size.HeightF)));

    /// <summary>
    /// Converts a <see cref="SizeF"/> to a <see cref="Size"/> by performing a round operation on all the dimensions.
    /// </summary>
    /// <param name="size">The size.</param>
    /// <returns>The <see cref="Size"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Size Round(Size size) => new Size(unchecked((int)MathF.Round(size.WidthF)), unchecked((int)MathF.Round(size.HeightF)));

    /// <summary>
    /// Transforms a size by the given matrix.
    /// </summary>
    /// <param name="size">The source size.</param>
    /// <param name="matrix">The transformation matrix.</param>
    /// <returns>A transformed size.</returns>
    public static Size Transform(Size size, Matrix3x2 matrix)
    {
        var v = Vector2.Transform(new Vector2(size.Width, size.Height), matrix);

        return new Size(v.X, v.Y);
    }

    /// <summary>
    /// Converts a <see cref="SizeF"/> to a <see cref="Size"/> by performing a round operation on all the dimensions.
    /// </summary>
    /// <param name="size">The size.</param>
    /// <returns>The <see cref="Size"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Size Truncate(Size size) => new Size(unchecked(size.WidthF), unchecked(size.HeightF));

    /// <summary>
    /// Multiplies <see cref="Size"/> by an <see cref="int"/> producing <see cref="Size"/>.
    /// </summary>
    /// <param name="size">Multiplicand of type <see cref="Size"/>.</param>
    /// <param name="multiplier">Multiplier of type <see cref="int"/>.</param>
    /// <returns>Product of type <see cref="Size"/>.</returns>
    private static Size Multiply(Size size, int multiplier) =>
        new Size(unchecked(size.Width * multiplier), unchecked(size.Height * multiplier));

    /// <summary>
    /// Multiplies <see cref="Size"/> by a <see cref="float"/> producing <see cref="SizeF"/>.
    /// </summary>
    /// <param name="size">Multiplicand of type <see cref="SizeF"/>.</param>
    /// <param name="multiplier">Multiplier of type <see cref="float"/>.</param>
    /// <returns>Product of type SizeF.</returns>
    private static Size Multiply(Size size, float multiplier) =>
        new(size.Width * multiplier, size.Height * multiplier);

    /// <summary>
    /// Deconstructs this size into two integers.
    /// </summary>
    /// <param name="width">The out value for the width.</param>
    /// <param name="height">The out value for the height.</param>
    public void Deconstruct(out int width, out int height)
    {
        width = Width;
        height = Height;
    }

    /// <summary>
    /// Deconstructs this size into two floats.
    /// </summary>
    /// <param name="width">The out value for the width.</param>
    /// <param name="height">The out value for the height.</param>
    public void Deconstruct(out float width, out float height)
    {
        width = WidthF;
        height = HeightF;
    }

    #endregion

    #region Overrides

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Width, Height);

    /// <inheritdoc/>
    public override string ToString() => $"Size [ Width={Width}, Height={Height} ]";

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Size other && Equals(other);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Size other) => Width.Equals(other.Width) && Height.Equals(other.Height);

    #endregion

    #region Operators

    /// <summary>
    /// Compares two <see cref="Size"/> objects for equality.
    /// </summary>
    /// <param name="left">
    /// The <see cref="Size"/> on the left side of the operand.
    /// </param>
    /// <param name="right">
    /// The <see cref="Size"/> on the right side of the operand.
    /// </param>
    /// <returns>
    /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Size left, Size right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Size"/> objects for inequality.
    /// </summary>
    /// <param name="left">
    /// The <see cref="Size"/> on the left side of the operand.
    /// </param>
    /// <param name="right">
    /// The <see cref="Size"/> on the right side of the operand.
    /// </param>
    /// <returns>
    /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Size left, Size right) => !left.Equals(right);

    /// <summary>
    /// Computes the sum of adding two sizes.
    /// </summary>
    /// <param name="left">The size on the left hand of the operand.</param>
    /// <param name="right">The size on the right hand of the operand.</param>
    /// <returns>
    /// The <see cref="Size"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Size operator +(Size left, Size right) => Add(left, right);

    /// <summary>
    /// Computes the difference left by subtracting one size from another.
    /// </summary>
    /// <param name="left">The size on the left hand of the operand.</param>
    /// <param name="right">The size on the right hand of the operand.</param>
    /// <returns>
    /// The <see cref="Size"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Size operator -(Size left, Size right) => Subtract(left, right);

    /// <summary>
    /// Multiplies a <see cref="Size"/> by an <see cref="int"/> producing <see cref="Size"/>.
    /// </summary>
    /// <param name="left">Multiplier of type <see cref="int"/>.</param>
    /// <param name="right">Multiplicand of type <see cref="Size"/>.</param>
    /// <returns>Product of type <see cref="Size"/>.</returns>
    public static Size operator *(int left, Size right) => Multiply(right, left);

    /// <summary>
    /// Multiplies <see cref="Size"/> by an <see cref="int"/> producing <see cref="Size"/>.
    /// </summary>
    /// <param name="left">Multiplicand of type <see cref="Size"/>.</param>
    /// <param name="right">Multiplier of type <see cref="int"/>.</param>
    /// <returns>Product of type <see cref="Size"/>.</returns>
    public static Size operator *(Size left, int right) => Multiply(left, right);

    /// <summary>
    /// Multiplies <see cref="Size"/> by a <see cref="float"/> producing <see cref="SizeF"/>.
    /// </summary>
    /// <param name="left">Multiplier of type <see cref="float"/>.</param>
    /// <param name="right">Multiplicand of type <see cref="Size"/>.</param>
    /// <returns>Product of type <see cref="SizeF"/>.</returns>
    public static Size operator *(float left, Size right) => Multiply(right, left);

    /// <summary>
    /// Multiplies <see cref="SizeF"/> by a <see cref="float"/> producing <see cref="SizeF"/>.
    /// </summary>
    /// <param name="left">Multiplicand of type <see cref="SizeF"/>.</param>
    /// <param name="right">Multiplier of type <see cref="float"/>.</param>
    /// <returns>Product of type <see cref="SizeF"/>.</returns>
    public static Size operator *(Size left, float right) => Multiply(left, right);

    /// <summary>
    /// Divides <see cref="Size"/> by an <see cref="int"/> producing <see cref="Size"/>.
    /// </summary>
    /// <param name="left">Dividend of type <see cref="Size"/>.</param>
    /// <param name="right">Divisor of type <see cref="int"/>.</param>
    /// <returns>Result of type <see cref="Size"/>.</returns>
    public static Size operator /(Size left, int right) => new Size(unchecked(left.Width / right), unchecked(left.Height / right));

    /// <summary>
    /// Divides <see cref="Size"/> by a <see cref="float"/> producing <see cref="Size"/>.
    /// </summary>
    /// <param name="left">Dividend of type <see cref="SizeF"/>.</param>
    /// <param name="right">Divisor of type <see cref="int"/>.</param>
    /// <returns>Result of type <see cref="SizeF"/>.</returns>
    public static Size operator /(Size left, float right)
        => new(left.WidthF / right, left.HeightF / right);

    /// <summary>
    /// Creates a <see cref="Vector2"/> with the coordinates of the specified <see cref="PointF"/>.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>
    /// The <see cref="Vector2"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Vector2(Size point) => new(point.WidthF, point.HeightF);

    #endregion
}