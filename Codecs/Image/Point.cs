using Media.Common;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Codecs.Image;

/// <summary>
/// Consolidates the concepts of `Point` and `PointF` into a single class.
/// Represents a 64 bit <see cref="MemorySegment"/>
/// Stored X, Y
/// </summary>
public class Point : MemorySegment, IEquatable<Point>
{
    /// <summary>
    /// Represents a <see cref="Point"/> that has X and Y values set to zero.
    /// </summary>
    public new static readonly Point Empty = new Point();

    #region Constructors

    public Point(MemorySegment segment)
        : base(segment)
    {

    }

    public Point()
        : base(Binary.BitsPerInteger * 2)
    {

    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Point"/> struct.
    /// </summary>
    /// <param name="value">The horizontal and vertical position of the point.</param>
    public Point(float value)
     : this()
    {
        Xf = value;
        Yf = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Point"/>.
    /// </summary>
    /// <param name="value">The horizontal and vertical position of the point.</param>
    public Point(int value)
     : this()
    {
        X = value;
        Y = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Point"/>.
    /// </summary>
    /// <param name="x">The horizontal position of the point.</param>
    /// <param name="y">The vertical position of the point.</param>
    public Point(int x, int y)
        : this()
    {
        X = x;
        Y = y;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Point"/>.
    /// </summary>
    /// <param name="x">The horizontal position of the point.</param>
    /// <param name="y">The vertical position of the point.</param>
    public Point(float x, float y)
        : this()
    {
        Xf = x;
        Yf = y;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Point"/>.
    /// </summary>
    /// <param name="size">The size.</param>
    public Point(Point size)
        : this()
    {
        X = size.X;
        Y = size.Y;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the X of this <see cref="Point"/>.
    /// </summary>
    public int X
    {
        get => Binary.Read32(this, base.Offset, false);
        set => Binary.Write32(Array, base.Offset, false, value);
    }

    /// <summary>
    /// Gets or sets the Y of this <see cref="Point"/>.
    /// </summary>
    public int Y
    {
        get => Binary.Read32(this, base.Offset + Binary.BytesPerInteger, false);
        set => Binary.Write32(Array, base.Offset + Binary.BytesPerInteger, false, value);
    }

    /// <summary>
    /// Gets the 32bit float value of the X.
    /// </summary>
    public float Xf
    {
        get => Binary.Read32(this, base.Offset, false);
        set => Binary.Write32(Array, base.Offset, false, (uint)value);
    }

    /// <summary>
    /// Gets the 32bit float value of the Y.
    /// </summary>
    public float Yf
    {
        get => Binary.Read32(this, base.Offset + Binary.BytesPerInteger, false);
        set => Binary.Write32(Array, base.Offset + Binary.BytesPerInteger, false, (uint)value);
    }

    /// <summary>
    /// Gets a value indicating whether this <see cref="Size"/> is empty.
    /// </summary>
    public bool IsEmpty => Equals(Empty);

    #endregion

    #region Operators

    /// <summary>
    /// Creates a <see cref="Vector2"/> with the coordinates of the specified <see cref="Point"/>.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>
    /// The <see cref="Vector2"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Point(Vector2 vector) => new(vector.X, vector.Y);

    /// <summary>
    /// Creates a <see cref="Vector2"/> with the coordinates of the specified <see cref="Point"/>.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>
    /// The <see cref="Vector2"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Vector2(Point point) => new(point.X, point.Y);

    /// <summary>
    /// Negates the given point by multiplying all values by -1.
    /// </summary>
    /// <param name="value">The source point.</param>
    /// <returns>The negated point.</returns>
    public static Point operator -(Point value) => new(-value.X, -value.Y);

    /// <summary>
    /// Translates a <see cref="Point"/> by a given <see cref="Size"/>.
    /// </summary>
    /// <param name="point">The point on the left hand of the operand.</param>
    /// <param name="size">The size on the right hand of the operand.</param>
    /// <returns>
    /// The <see cref="Point"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point operator +(Point point, Size size) => Add(point, size);

    /// <summary>
    /// Translates a <see cref="Point"/> by the negative of a given <see cref="Size"/>.
    /// </summary>
    /// <param name="point">The point on the left hand of the operand.</param>
    /// <param name="size">The size on the right hand of the operand.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point operator -(Point point, Point size) => Subtract(point, size);

    /// <summary>
    /// Translates a <see cref="Point"/> by a given <see cref="Size"/>.
    /// </summary>
    /// <param name="point">The point on the left hand of the operand.</param>
    /// <param name="size">The size on the right hand of the operand.</param>
    /// <returns>
    /// The <see cref="Point"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point operator +(Point point, Point size) => Add(point, size);

    /// <summary>
    /// Translates a <see cref="Point"/> by the negative of a given <see cref="Size"/>.
    /// </summary>
    /// <param name="point">The point on the left hand of the operand.</param>
    /// <param name="size">The size on the right hand of the operand.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point operator -(Point point, Size size) => Subtract(point, size);

    /// <summary>
    /// Multiplies <see cref="Point"/> by a <see cref="float"/> producing <see cref="Size"/>.
    /// </summary>
    /// <param name="left">Multiplier of type <see cref="float"/>.</param>
    /// <param name="right">Multiplicand of type <see cref="Size"/>.</param>
    /// <returns>Product of type <see cref="Size"/>.</returns>
    public static Point operator *(float left, Point right) => Multiply(right, left);

    /// <summary>
    /// Multiplies <see cref="Point"/> by a <see cref="float"/> producing <see cref="Size"/>.
    /// </summary>
    /// <param name="left">Multiplicand of type <see cref="Point"/>.</param>
    /// <param name="right">Multiplier of type <see cref="float"/>.</param>
    /// <returns>Product of type <see cref="Size"/>.</returns>
    public static Point operator *(Point left, float right) => Multiply(left, right);

    /// <summary>
    /// Divides <see cref="Point"/> by a <see cref="float"/> producing <see cref="Size"/>.
    /// </summary>
    /// <param name="left">Dividend of type <see cref="Point"/>.</param>
    /// <param name="right">Divisor of type <see cref="int"/>.</param>
    /// <returns>Result of type <see cref="Point"/>.</returns>
    public static Point operator /(Point left, float right)
        => new(left.X / right, left.Y / right);

    /// <summary>
    /// Compares two <see cref="Point"/> objects for equality.
    /// </summary>
    /// <param name="left">
    /// The <see cref="Point"/> on the left side of the operand.
    /// </param>
    /// <param name="right">
    /// The <see cref="Point"/> on the right side of the operand.
    /// </param>
    /// <returns>
    /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Point left, Point right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="Point"/> objects for inequality.
    /// </summary>
    /// <param name="left">
    /// The <see cref="Point"/> on the left side of the operand.
    /// </param>
    /// <param name="right">
    /// The <see cref="Point"/> on the right side of the operand.
    /// </param>
    /// <returns>
    /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Point left, Point right) => !left.Equals(right);

    /// <summary>
    /// Creates a <see cref="Size"/> with the coordinates of the specified <see cref="Point"/>.
    /// </summary>
    /// <param name="point">The point.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Size(Point point) => new(point.X, point.Y);

    /// <summary>
    /// Multiplies <see cref="Point"/> by a <see cref="int"/> producing <see cref="Point"/>.
    /// </summary>
    /// <param name="left">Multiplier of type <see cref="int"/>.</param>
    /// <param name="right">Multiplicand of type <see cref="Point"/>.</param>
    /// <returns>Product of type <see cref="Point"/>.</returns>
    public static Point operator *(int left, Point right) => Multiply(right, left);

    /// <summary>
    /// Multiplies <see cref="Point"/> by a <see cref="int"/> producing <see cref="Point"/>.
    /// </summary>
    /// <param name="left">Multiplicand of type <see cref="Point"/>.</param>
    /// <param name="right">Multiplier of type <see cref="int"/>.</param>
    /// <returns>Product of type <see cref="Point"/>.</returns>
    public static Point operator *(Point left, int right) => Multiply(left, right);

    /// <summary>
    /// Divides <see cref="Point"/> by a <see cref="int"/> producing <see cref="Point"/>.
    /// </summary>
    /// <param name="left">Dividend of type <see cref="Point"/>.</param>
    /// <param name="right">Divisor of type <see cref="int"/>.</param>
    /// <returns>Result of type <see cref="Point"/>.</returns>
    public static Point operator /(Point left, int right)
        => new(left.X / right, left.Y / right);

    #endregion

    #region Methods

    /// <summary>
    /// Translates a <see cref="Point"/> by the negative of a given <see cref="Size"/>.
    /// </summary>
    /// <param name="point">The point on the left hand of the operand.</param>
    /// <param name="size">The size on the right hand of the operand.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point Add(Point point, Size size) => new(unchecked(point.X + size.Width), unchecked(point.Y + size.Height));

    /// <summary>
    /// Translates a <see cref="Point"/> by the negative of a given value.
    /// </summary>
    /// <param name="point">The point on the left hand of the operand.</param>
    /// <param name="value">The value on the right hand of the operand.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point Multiply(Point point, int value) => new(unchecked(point.X * value), unchecked(point.Y * value));

    /// <summary>
    /// Translates a <see cref="Point"/> by the negative of a given <see cref="Size"/>.
    /// </summary>
    /// <param name="point">The point on the left hand of the operand.</param>
    /// <param name="size">The size on the right hand of the operand.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point Subtract(Point point, Size size) => new(unchecked(point.X - size.Width), unchecked(point.Y - size.Height));

    /// <summary>
    /// Converts a <see cref="PointF"/> to a <see cref="Point"/> by performing a ceiling operation on all the coordinates.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point Ceiling(Point point) => new(unchecked((int)MathF.Ceiling(point.Xf)), unchecked((int)MathF.Ceiling(point.Yf)));

    /// <summary>
    /// Converts a <see cref="PointF"/> to a <see cref="Point"/> by performing a round operation on all the coordinates.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point Round(Point point) => new(unchecked((int)MathF.Round(point.Xf)), unchecked((int)MathF.Round(point.Yf)));

    /// <summary>
    /// Converts a <see cref="Vector2"/> to a <see cref="Point"/> by performing a round operation on all the coordinates.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point Round(Vector2 vector) => new(unchecked((int)MathF.Round(vector.X)), unchecked((int)MathF.Round(vector.Y)));

    /// <summary>
    /// Converts a <see cref="PointF"/> to a <see cref="Point"/> by performing a truncate operation on all the coordinates.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point Truncate(Point point) => new(unchecked(point.X), unchecked(point.Y));

    /// <summary>
    /// Transforms a point by a specified 3x2 matrix.
    /// </summary>
    /// <param name="point">The point to transform.</param>
    /// <param name="matrix">The transformation matrix used.</param>
    /// <returns>The transformed <see cref="PointF"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point Transform(Point point, Matrix3x2 matrix) => Round(Vector2.Transform(new Vector2(point.X, point.Y), matrix));

    /// <summary>
    /// Deconstructs this point into two integers.
    /// </summary>
    /// <param name="x">The out value for X.</param>
    /// <param name="y">The out value for Y.</param>
    public void Deconstruct(out int x, out int y)
    {
        x = X;
        y = Y;
    }

    /// <summary>
    /// Deconstructs this point into two floats.
    /// </summary>
    /// <param name="x">The out value for X.</param>
    /// <param name="y">The out value for Y.</param>
    public void Deconstruct(out float x, out float y)
    {
        x = X;
        y = Y;
    }

    /// <summary>
    /// Translates this <see cref="Point"/> by the specified amount.
    /// </summary>
    /// <param name="dx">The amount to offset the x-coordinate.</param>
    /// <param name="dy">The amount to offset the y-coordinate.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new void Offset(int dx, int dy)
    {
        unchecked
        {
            X += dx;
            Y += dy;
        }
    }

    /// <summary>
    /// Translates this <see cref="Point"/> by the specified amount.
    /// </summary>
    /// <param name="point">The <see cref="Point"/> used offset this <see cref="Point"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new void Offset(Point point) => Offset(point.X, point.Y);

    /// <summary>
    /// Translates a <see cref="Point"/> by the given <see cref="Point"/>.
    /// </summary>
    /// <param name="point">The point on the left hand of the operand.</param>
    /// <param name="pointb">The point on the right hand of the operand.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point Add(Point point, Point pointb) => new(point.X + pointb.X, point.Y + pointb.Y);

    /// <summary>
    /// Translates a <see cref="Point"/> by the negative of a given <see cref="Point"/>.
    /// </summary>
    /// <param name="point">The point on the left hand of the operand.</param>
    /// <param name="pointb">The point on the right hand of the operand.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point Subtract(Point point, Point pointb) => new(point.X - pointb.X, point.Y - pointb.Y);

    /// <summary>
    /// Translates a <see cref="Point"/> by the multiplying the X and Y by the given value.
    /// </summary>
    /// <param name="point">The point on the left hand of the operand.</param>
    /// <param name="right">The value on the right hand of the operand.</param>
    /// <returns>The <see cref="Point"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point Multiply(Point point, float right) => new(point.X * right, point.Y * right);    

    /// <summary>
    /// Translates this <see cref="Point"/> by the specified amount.
    /// </summary>
    /// <param name="dx">The amount to offset the x-coordinate.</param>
    /// <param name="dy">The amount to offset the y-coordinate.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new void Offset(float dx, float dy)
    {
        Xf += dx;
        Yf += dy;
    }

    #endregion

    #region Overides

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(X, Y);

    /// <inheritdoc/>
    public override string ToString() => $"Point [ X={X}, Y={Y} ]";

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Point Point && Equals(Point);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Point other) => X.Equals(other.X) && Y.Equals(other.Y);

    #endregion
}
