﻿using System;
using System.Collections.Generic;
using System.Linq;
/*
Copyright (c) 2013 juliusfriedman@gmail.com
  
 SR. Software Engineer ASTI Transportation Inc.

Permission is hereby granted, free of charge, 
 * to any person obtaining a copy of this software and associated documentation files (the "Software"), 
 * to deal in the Software without restriction, 
 * including without limitation the rights to :
 * use, 
 * copy, 
 * modify, 
 * merge, 
 * publish, 
 * distribute, 
 * sublicense, 
 * and/or sell copies of the Software, 
 * and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
 * 
 * 
 * JuliusFriedman@gmail.com should be contacted for further details.

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
 * 
 * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
 * DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, 
 * TORT OR OTHERWISE, 
 * ARISING FROM, 
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 * 
 * v//
 */

//https://www.codeproject.com/Articles/578116/Complete-Managed-Media-Aggregation-Part-III-Quantu

///All types besides UnitBase could eventually be struct
namespace Media.Concepts.Classes
{
    /// <summary>
    /// 
    /// </summary>
    public interface IUnit
    {
        IEnumerable<string> Symbols { get; } //Used with formatting

        /// <summary>
        /// A Number which represents a value from which a scalar value can be calculated from the TotalUnits member
        /// </summary>
        Number Constant { get; }

        /// <summary>
        /// A Number which represents the total amount of integral units of this instance
        /// </summary>
        Number TotalUnits { get; }

        //MinValue, MaxValue
    }

    /// <summary>
    /// The base class of all units or magnitudes.
    /// </summary>
    /// <remarks>
    /// Other implementations...
    /// https://github.com/dotnet/corefx/issues/6831
    /// https://github.com/JohanLarsson/Gu.Units/blob/master/Gu.Units/ElectricCharge.generated.cs
    /// </remarks>
    public abstract class UnitBase : IUnit, IFormattable
    {
        #region Statics

        /// <summary>
        /// The <see cref="System.Globalization.RegionInfo"/> as retrieved from <see cref="System.Globalization.RegionInfo.CurrentRegion"/>
        /// </summary>
        public static readonly System.Globalization.RegionInfo CurrentRegion = System.Globalization.RegionInfo.CurrentRegion;

        /// <summary>
        /// Indicates if the <see cref="CurrentRegion"/> utilizes the Metric system of units.
        /// </summary>
        public static bool IsMetricSystem
        {
            get { return CurrentRegion.IsMetric; }
        }

        #endregion

        //Weird, but would allow the ability to add units of differing types without having to access the Units.. would also be doable via Extenions
        //public static class UnitBaseExtensions
        //{
        //    //public static UnitBase Add(UnitBase a, UnitBase b)
        //    //{

        //    //}

        //    //public static UnitBase Subtract(UnitBase a, UnitBase b)
        //    //{

        //    //}

        //    //public static UnitBase Multiply(UnitBase a, UnitBase b)
        //    //{

        //    //}

        //    //public static UnitBase Divide(UnitBase a, UnitBase b)
        //    //{

        //    //}

        //    //public static UnitBase Modulus(UnitBase a, UnitBase b)
        //    //{

        //    //}
        //}

        #region Fields

        /// <summary>
        /// The symbols utilized by this instance
        /// </summary>
        protected abstract List<string> m_Symbols { get; }

        #endregion

        #region Properties

        /// <summary>
        /// The symbols of this instance.
        /// </summary>
        public IEnumerable<string> Symbols
        {
            get { return m_Symbols.AsReadOnly(); }
        }

        /// <summary>
        /// Defines the number used to scale other distances to this number.
        /// </summary>
        public Number Constant
        {
            get;
            protected internal set;
        }

        /// <summary>
        /// The <see cref="Number"/> associated.
        /// </summary>
        public Number Units
        {
            get;
            protected set;
        }

        /// <summary>
        /// The product of <see cref="Units"/> and <see cref="Constant"/>.
        /// </summary>
        public Number TotalUnits
        {
            get
            {
                //More Flexible
                //return Constant.ToDouble() > 1D ? Units.ToDouble() * Constant.ToDouble() : Units.ToDouble() / Constant.ToDouble();
                return new Number(Units.ToDouble() * Constant.ToDouble());
            }
        }

        #endregion

        #region IUnit

        /// <summary>
        /// 
        /// </summary>
        IEnumerable<string> IUnit.Symbols
        {
            get { return Symbols; }
        }

        /// <summary>
        /// 
        /// </summary>
        Number IUnit.Constant
        {
            get { return Constant; }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Constructs a new UnitBase with the given constant
        /// </summary>
        /// <param name="constant">The constant which when multiplied by the Units property represents a quantity</param>
        public UnitBase(Number constant)
        {
            Constant = constant;
        }

        /// <summary>
        /// Constructs a new UnitBase from another.
        /// If the Constants of the two Units are the same the Units property is assigned, otherwise the Units is obtained by division of the other UnitBase's Units by this instances Constant.
        /// </summary>
        /// <param name="constant">The constant which when multiplied by the Units property represents a quantity</param>
        /// <param name="other">Another Unit base</param>
        public UnitBase(Number constant, UnitBase other)
            : this(constant)
        {
            Units = other.Constant != Constant ? (Number)(Constant.ToDouble() / other.Units.ToDouble()) : other.Units;
        }

        #endregion

        public virtual string ToString(string join = " ")
        {
            return string.Concat(Units.ToString(), join, m_Symbols.FirstOrDefault());
        }

        public override string ToString()
        {
            return ToString(null);
        }

        string IFormattable.ToString(string format, IFormatProvider formatProvider)
        {
            return string.Format(formatProvider, format, ToString());
        }

        //Implementing operators is not really feasible at this level however it could be done in CoreUnits if that provides any meaning.
        //Could also interoperate on Units whatever that would mean to who uses it.
        //virtuals might actually help here but since there interfaces it makes little sense.

        //https://github.com/dotnet/corefx/issues/6831#issuecomment-230557261

        //To parse you can provide a static which can be exposed from the dervived types if they desire so.

        private static bool Parse(UnitBase units, string value, int offset = 0, int count = -1, char[] symbols = null, System.Globalization.NumberStyles ns = System.Globalization.NumberStyles.None, System.Globalization.NumberFormatInfo nfi = null)
        {
            if ((units is null || units.Symbols is null) &&
                units.Symbols is null || string.IsNullOrWhiteSpace(value)) return false;

            if (count < 0) count = value.Length - offset;

            int symbolIndex = value.IndexOfAny(symbols ?? units.Symbols.SelectMany(s => s.ToArray()).ToArray(), offset, count);

            if (symbolIndex < 0 || symbolIndex > count) return false;

            if (units is not null)
            {
                try
                {
                    units.Units += Number.Parse(System.Text.Encoding.Default.GetBytes(value.ToCharArray(), symbolIndex, count), symbolIndex, count = value.Length - symbolIndex, System.Text.Encoding.Default, ns, nfi ?? System.Globalization.NumberFormatInfo.CurrentInfo);
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                //Must check loaded types and get all Symbols...

                throw new NotImplementedException();
            }

            return true;
        }
    }

    /// <summary>
    /// A layer of <see cref="IUnit"/>; the unit of indirection as well as an <see cref="Media.Common.Interfaces.Interface"/>
    /// </summary>
    public interface IndirectionUnit : IUnit, Media.Common.Interfaces.Interface
    {
        /// <summary>
        /// The Error
        /// </summary>
        Number Error { get; }
    }

    /// <summary>
    /// A unit which represents [within a small margin of error] itself. <see cref="IndirectionUnit"/>
    /// </summary>
    public class IdealUnit : UnitBase, IndirectionUnit
    {

        //Todo, allow min, max and symbols to be given
        public static IdealUnit Create(Number value, Number constant)
        {
            return new IdealUnit(value, new IdealUnit(constant));
        }

        /// <summary>
        /// 
        /// </summary>
        public const double Zero = Common.Binary.DoubleZero,
            One = 1.00000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001;

        /// <summary>
        /// The smallest and largest positive values.
        /// </summary>
        public static readonly IdealUnit MinValue = new(-0.0), MaxValue = new(double.MaxValue);

        //@Sprintf
        private static readonly List<string> IndirectUnitSymbols =
            [
                "{{0}}"
            ];

        /// <summary>
        /// The Error
        /// </summary>
        protected internal Number Error;

        /// <summary>
        /// Create the;
        /// </summary>
        public IdealUnit()
            : base(One)
        {
            Constant = MinValue.Constant;

            Units = MinValue.Units;

            Error = Zero;
        }

        /// <summary>
        /// Create a;
        /// </summary>
        /// <param name="units"></param>
        public IdealUnit(Number units)
            : base(Zero)
        {
            Units = units;

            Error = Zero;
        }

        /// <summary>
        /// Create from;
        /// </summary>
        /// <param name="other"></param>
        public IdealUnit(IdealUnit other) : base(One, other) { }

        /// <summary>
        /// <see cref="UnitBase"/>
        /// </summary>
        /// <param name="value"></param>
        /// <param name="other"></param>
        public IdealUnit(Number value, IdealUnit other)
            : base(One, other)
        {
            Units = value;

            Error = One;
        }

        /// <summary>
        /// The symbol
        /// </summary>
        protected override List<string> m_Symbols
        {
            get
            {
                return IndirectUnitSymbols;
            }
        }

        //Use IdealUnit for conversions to other units and allow the error to be specified.

        Number IndirectionUnit.Error
        {
            get { return this.Error; }
        }

        IEnumerable<string> IUnit.Symbols
        {
            get { return IndirectUnitSymbols; }
        }

        Number IUnit.Constant
        {
            get { return this.Constant; }
        }

        Number IUnit.TotalUnits
        {
            get { return this.TotalUnits; }
        }
    }

    //One value
    //ScalarUnit => IdealUnit

    //Many values
    //VectoralUnit => IdealUnit

    /// <summary>
    /// A form of <see cref="Unit"/> which represents values through bias.
    /// </summary>
    internal class VisceralUnit : IdealUnit
    {

    }

    /// <summary>
    /// A <see cref="IUnit"/> which contains an <see cref="Action"/> which is called by the <see cref="Sample"/> method.
    /// </summary>
    public class InformalUnit : IdealUnit
    {

        //IsError ()=> LastValue != 0 && LastValue >= 0x80000000

        /// <summary>
        /// Any <see cref="IUnit"/> which is conveyed from the <see cref="OnSample"/> method
        /// </summary>
        public IUnit LastValue;

        /// <summary>
        /// Typically used to compute a value or call a function
        /// </summary>
        private readonly Action OnSample = Common.Extensions.Delegate.ActionExtensions.NoOp;

        /// <summary>
        /// Stores the current instance in <see cref="LastValue"/> and calls <see cref="OnSample"/> which may modify this instance to pass values.
        /// </summary>
        public virtual void Sample()
        {
            LastValue = this;
            try
            {
                OnSample();
            }
            catch
            {
                Error = this.TotalUnits;
            }
        }
    }

    /// <summary>
    /// Subclass of <see cref="InformalUnit"/> with a <see cref="ValueType"/>
    /// </summary>
    public class InformationalUnit : InformalUnit
    {
        /// <summary>
        /// The <see cref="ValueType"/> which is used by the <see cref="IUnit"/>
        /// </summary>
        public ValueType Value;

        /// <summary>
        /// Calls <see cref="InformalUnit.Sample"/> and then copies the result to <see cref="Value"/> as a <see cref="double"/>
        /// </summary>
        public override void Sample()
        {
            try
            {
                base.Sample();
                Value = this.TotalUnits.ToDouble();
            }
            catch
            {
                Error = this.TotalUnits.ToDouble();
            }
        }
    }

    /// <summary>
    /// Class which is useful for measuring and converting distance
    /// </summary>
    public static class Distances
    {
        public interface IDistance : IUnit
        {
            Number TotalMeters { get; }
        }

        public class Distance : UnitBase, IDistance
        {

            //Should be Number to avoid readonly ValueType

            public static readonly double PlanckLengthsPerMeter = 6.1873559 * System.Math.Pow(10, 34);

            public static readonly double MilsPerMeter = 2.54 * System.Math.Pow(10, -5);

            public const double InchesPerMeter = 0.0254;

            public const double FeetPerMeter = 0.3048;

            public const double YardsPerMeter = 0.9144;

            public const double MilesPerMeter = 1609.344;

            public static readonly double AttometersPerMeter = System.Math.Pow(10, 18);

            //1 yoctometer = 0,001 zeptometer
            //1 attometer = 1000 zeptometer
            //1 000 yoctometer
            //0,001 attometer
            //10−21 meter
            public static readonly double ZeptometersPerMeter = System.Math.Pow(10, -21);

            public static readonly double YoctometersPerMeter = System.Math.Pow(10, -24);

            public const double NanometersPerMeter = 1000000000;

            public const double MicronsPerMeter = 1000000;

            public const double MillimetersPerMeter = 1000;

            public const double CentimetersPerMeter = 100;

            public const double DecimetersPerMeter = 10;

            public const double M = 1;

            public const double KilometersPerMeter = 0.001;

            /// <summary>
            /// The minimum distance in Meters = The Planck Length
            /// </summary>
            public static readonly Distance MinValue = Physics.ℓP;

            public static readonly Distance PositiveInfinity = new(Number.PositiveInfinty);

            public static readonly Distance NegitiveInfinity = new(Number.NegitiveInfinity);

            public static readonly Distance Zero = new(Number.Zero);
            private static readonly List<string> DistanceSymbols =
            [
                "ℓP",
                "mil",
                "in",
                "ft",
                "yd",
                "mi",
                "n",
                "µ",
                "mm",
                "cm",
                "m",
                "km"
            ];

            public Distance()
                : base(M)
            {
                Constant = MinValue.Constant;
                Units = MinValue.Units;
            }

            public Distance(Number meters)
                : base(M)
            {
                Units = meters;
            }

            public Distance(Distance other) : base(M, other) { }

            public Distance(Number value, Distance other) : base(M, other) { Units = value; }

            protected override List<string> m_Symbols
            {
                get
                {
                    return DistanceSymbols;
                }
            }

            public virtual Number TotalMeters
            {
                get { return Units; }
            }

            public virtual Number TotalInches
            {
                get { return TotalMeters / InchesPerMeter; }
            }

            public virtual Number TotalFeet
            {
                get { return TotalMeters / FeetPerMeter; }
            }

            public virtual Number TotalYards
            {
                get { return TotalMeters / YardsPerMeter; }
            }

            public virtual Number TotalKilometers
            {
                get { return TotalMeters / KilometersPerMeter; }
            }

            public static Distance FromInches(Number value)
            {
                return new Distance(value.ToDouble() * InchesPerMeter);
            }

            public static Distance FromFeet(Number value)
            {
                return new Distance(value.ToDouble() * FeetPerMeter);
            }

            public static Distance FromYards(Number value)
            {
                return new Distance(value.ToDouble() * YardsPerMeter);
            }

            public static Distance operator +(Distance a, int amount)
            {
                return new Distance(a.Units.ToDouble() + amount);
            }

            public static Distance operator -(Distance a, int amount)
            {
                return new Distance(a.Units.ToDouble() - amount);
            }

            public static Distance operator *(Distance a, int amount)
            {
                return new Distance(a.Units.ToDouble() * amount);
            }

            public static Distance operator /(Distance a, int amount)
            {
                return new Distance(a.Units.ToDouble() / amount);
            }

            public static Distance operator %(Distance a, int amount)
            {
                return new Distance(a.Units.ToDouble() % amount);
            }

            public static bool operator >(Distance a, IDistance b)
            {
                return a.Constant.Equals(b.Constant) is false ? a.Units * b.Constant > b.TotalMeters : a.Units > b.TotalMeters;
            }

            public static bool operator <(Distance a, IDistance b)
            {
                return (a > b) is false;
            }

            public static bool operator ==(Distance a, IDistance b)
            {
                return a is null
                    ? b is null
                    : b is not null && (a.Constant.Equals(b.Constant) is false ? a.Units * b.Constant == b.TotalMeters : a.Units == b.TotalMeters);
            }

            public static bool operator !=(Distance a, IDistance b)
            {
                return (a == b) is false;
            }

            public override bool Equals(object obj)
            {
                return obj is IDistance ? obj as IDistance == this : base.Equals(obj);
            }

            public override int GetHashCode()
            {
                return Constant.GetHashCode() << 16 | Units.GetHashCode() >> 16;
            }
        }
    }

    /// <summary>
    /// Class which is usefl for measuring and converting frequency
    /// </summary>
    public static class Frequencies
    {
        ////    public enum FrequencyKind
        ////    {
        ////        Local,
        ////        Universal
        ////    }

        ////    public static class Clock
        ////    {
        ////    }


        public interface IFrequency
        {
            Number TotalMegahertz { get; }
        }


        //https://en.wikipedia.org/wiki/Frequency
        /* Frequencies not expressed in hertz:
         * 
         * Even higher frequencies are believed to occur naturally, 
         * in the frequencies of the quantum-mechanical wave functions of high-energy
         * (or, equivalently, massive) particles, although these are not directly observable, 
         * and must be inferred from their interactions with other phenomena. 
         * For practical reasons, these are typically not expressed in hertz, 
         * but in terms of the equivalent quantum energy, which is proportional to the frequency by the factor of Planck's constant.
         */
        public class Frequency : UnitBase, IFrequency
        {

            public static implicit operator double(Frequency t) { return t.Units.ToDouble(); }

            public static implicit operator Frequency(double t) { return new Frequency(t); }

            public static readonly Frequency Zero = new(Number.Zero);

            public static readonly Frequency One = new(new Number(Hz)); //Hz

            //Should be Number to avoid readonly ValueType

            public const double Hz = 1;

            public const double KHz = 1000D;

            public const double MHz = 1000000D;

            public const double GHz = 1000000000D;

            public const double THz = 1000000000000D;


            //https://en.wikipedia.org/wiki/Visible_spectrum - Audible?
            public static bool IsVisible(Frequency f, double min = 430, double max = 790)
            {
                double F = f.Terahertz.ToDouble();
                return F >= min && F <= max;
            }

            private static readonly List<string> FrequencySymbols =
            [
                "Hz",
                "KHz",
                "MHz",
                "GHz",
                "THz"
            ];

            public Frequency()
                : base(Hz)
            {
                //Constant = MinValue.Constant;
                //Units = MinValue.Units;
            }

            public Frequency(double MHz)
                : base(Hz)
            {
                Units = MHz;
            }

            public Frequency(Frequency other) : base(Hz, other) { }

            public Frequency(Number value, Frequency other) : base(Hz, other) { Units = value; }

            protected override List<string> m_Symbols
            {
                get
                {
                    return FrequencySymbols;
                }
            }

            public TimeSpan Period
            {
                get
                {
                    return TimeSpan.FromSeconds(TotalHertz);
                }
            }

            public virtual Number TotalHertz
            {
                get { return Units; }
            }

            public virtual Number TotalKilohertz
            {
                get { return TotalHertz * KHz; }
            }

            public virtual Number TotalMegahertz
            {
                get { return TotalHertz * MHz; }
            }

            public virtual Number TotalGigahertz
            {
                get { return TotalHertz * GHz; }
            }

            public virtual Number Terahertz
            {
                get { return TotalHertz * THz; }
            }

            public static Frequency FromKilohertz(Number value)
            {
                return new Frequency(value.ToDouble() * KHz);
            }

            public static Frequency FromMegahertz(Number value)
            {
                return new Frequency(value.ToDouble() * MHz);
            }

            public static Frequency FromGigahertz(Number value)
            {
                return new Frequency(value.ToDouble() * GHz);
            }

            public static Frequency FromTerahertz(Number value)
            {
                return new Frequency(value.ToDouble() * THz);
            }

            public static Frequency operator +(Frequency a, int amount)
            {
                return new Frequency(a.Units.ToDouble() + amount);
            }

            public static Frequency operator -(Frequency a, int amount)
            {
                return new Frequency(a.Units.ToDouble() - amount);
            }

            public static Frequency operator *(Frequency a, int amount)
            {
                return new Frequency(a.Units.ToDouble() * amount);
            }

            public static Frequency operator /(Frequency a, int amount)
            {
                return new Frequency(a.Units.ToDouble() / amount);
            }

            public static Frequency operator %(Frequency a, int amount)
            {
                return new Frequency(a.Units.ToDouble() % amount);
            }

            public static bool operator >(Frequency a, Frequency b)
            {
                return a.Constant.Equals(b.Constant) is false ? a.Units * b.Constant > b.TotalUnits : a.Units > b.TotalUnits;
            }

            public static bool operator <(Frequency a, Frequency b)
            {
                return !(a > b);
            }

            public static bool operator ==(Frequency a, Frequency b)
            {
                return a is null
                    ? b is null
                    : b is not null && (a.Constant.Equals(b.Constant) is false ? a.Units * b.Constant == b.TotalUnits : a.Units == b.TotalUnits);
            }

            public static bool operator !=(Frequency a, Frequency b)
            {
                return !(a == b);
            }

            public override bool Equals(object obj)
            {
                return obj is Frequency || base.Equals(obj);
            }

            public override int GetHashCode()
            {
                return Constant.GetHashCode() << 16 | Units.GetHashCode() >> 16;
            }


            //Add methods for conversion to time
            //http://www.hellspark.com/dm/ebench/tools/Analog_Oscilliscope/tutorials/scope_notes_from_irc.html        

        }

        ////    public struct Date
        ////    {
        ////        pulic DateTime ToDateTime(Frequency? time = null);
        ////    }
    }

    /// <summary>
    /// Class which is useful for measuring and converting temperature
    /// </summary>
    public static class Temperatures
    {

        public interface ITemperature : IUnit
        {
            Number TotalCelcius { get; }
        }

        public class Temperature : UnitBase, ITemperature
        {

            public static implicit operator double(Temperature t) { return t.Units.ToDouble(); }

            public static implicit operator Temperature(double t) { return new Temperature(t); }

            public static readonly Temperature MinValue = 0D;

            public static readonly Temperature One = 1D; //Celcius

            private const double FahrenheitMultiplier = 1.8;

            public const double Fahrenheit = 32D;

            public const double Kelvin = 273.15D;

            public const char Degrees = '°';
            private static readonly List<string> TempratureSymbols =
            [
                "C",
                "F",
                "K",
            ];

            public Temperature()
                : base(One.Units)
            {
                //Constant = MinValue.Constant;
                //Units = MinValue.Units;
            }

            public Temperature(double celcius)
                : base(One.Units)
            {
                Units = celcius;
            }

            public Temperature(Temperature other) : base(One.Units, other) { }

            public Temperature(Number value, Temperature other) : base(One.Units, other) { Units = value; }

            protected override List<string> m_Symbols
            {
                get
                {
                    return TempratureSymbols;
                }
            }

            public virtual Number TotalCelcius
            {
                get { return Units; }
            }

            public virtual Number TotalKelvin
            {
                get { return TotalCelcius + Kelvin; }
            }

            public virtual Number TotalFahrenheit
            {
                get { return TotalCelcius * FahrenheitMultiplier + Fahrenheit; }
            }

            public static Temperature FromFahrenheit(Number value)
            {
                return new Temperature(value.ToDouble() * Fahrenheit);
            }

            public static Temperature FromKelvin(Number value)
            {
                return new Temperature(value.ToDouble() - Kelvin);
            }

            public static Temperature operator +(Temperature a, int amount)
            {
                return new Temperature(a.Units.ToDouble() + amount);
            }

            public static Temperature operator -(Temperature a, int amount)
            {
                return new Temperature(a.Units.ToDouble() - amount);
            }

            public static Temperature operator *(Temperature a, int amount)
            {
                return new Temperature(a.Units.ToDouble() * amount);
            }

            public static Temperature operator /(Temperature a, int amount)
            {
                return new Temperature(a.Units.ToDouble() / amount);
            }

            public static Temperature operator %(Temperature a, int amount)
            {
                return new Temperature(a.Units.ToDouble() % amount);
            }

            public static bool operator >(Temperature a, ITemperature b)
            {
                return a.Constant.Equals(b.Constant) is false ? a.Units * b.Constant > b.TotalUnits : a.Units > b.TotalUnits;
            }

            public static bool operator <(Temperature a, ITemperature b)
            {
                return !(a > b);
            }

            public static bool operator ==(Temperature a, ITemperature b)
            {
                return a is null
                    ? b is null
                    : b is not null && (a.Constant.Equals(b.Constant) is false ? a.Units * b.Constant == b.TotalUnits : a.Units == b.TotalUnits);
            }

            public static bool operator !=(Temperature a, ITemperature b)
            {
                return !(a == b);
            }

            public override bool Equals(object obj)
            {
                return obj is ITemperature || base.Equals(obj);
            }

            public override int GetHashCode()
            {
                return Constant.GetHashCode() << 16 | Units.GetHashCode() >> 16;
            }

            public override string ToString()
            {
                return ToString(" " + Degrees);
            }
        }

    }

    /// <summary>
    /// A description of mass with respect to physical and mathematical properties.
    /// </summary>
    public static class Masses
    {
        public interface IMass : IUnit
        {
            Number TotalKilograms { get; }
        }

        public class Mass : UnitBase, IMass
        {

            //Should be Number to avoid readonly ValueType

            public const double AtomicMassesPerKilogram = 6.022136652e+26;

            public const double OuncesPerKilogram = 35.274;

            public const double PoundsPerKilogram = 2.20462;

            public const double Kg = 1;

            public const double GramsPerKilogram = 1000;
            private static readonly List<string> MassSymbols =
            [
                "u",
                "o",
                "lb",
                "kg",
                "g",
            ];

            public Mass()
                : base(Kg)
            {
                //Constant = MinValue.Constant;
                //Units = MinValue.Units;
            }

            public Mass(Number kiloGrams)
                : base(Kg)
            {
                Units = kiloGrams;
            }

            public Mass(Mass other) : base(Kg, other) { }

            public Mass(Number value, Mass other) : base(Kg, other) { Units = value; }

            protected override List<string> m_Symbols
            {
                get
                {
                    return MassSymbols;
                }
            }

            public virtual Number TotalKilograms
            {
                get { return Units; }
            }

            public virtual Number TotalAtomicMasses
            {
                get { return TotalKilograms * AtomicMassesPerKilogram; }
            }

            public virtual Number TotalGrams
            {
                get { return TotalKilograms * GramsPerKilogram; }
            }
            public virtual Number TotalOunces
            {
                get { return TotalKilograms * OuncesPerKilogram; }
            }

            public virtual Number TotalPounds
            {
                get { return TotalKilograms * PoundsPerKilogram; }
            }

            public static Mass FromGrams(Number value)
            {
                return new Mass(value.ToDouble() * GramsPerKilogram);
            }

            public static Mass FromPounds(Number value)
            {
                return new Mass(value.ToDouble() * PoundsPerKilogram);
            }

            public static Mass FromOunces(Number value)
            {
                return new Mass(value.ToDouble() * OuncesPerKilogram);
            }

            public static Mass FromAtomicMasses(Number value)
            {
                return new Mass(value.ToDouble() * AtomicMassesPerKilogram);
            }

            public static Mass operator +(Mass a, int amount)
            {
                return new Mass(a.Units.ToDouble() + amount);
            }

            public static Mass operator -(Mass a, int amount)
            {
                return new Mass(a.Units.ToDouble() - amount);
            }

            public static Mass operator *(Mass a, int amount)
            {
                return new Mass(a.Units.ToDouble() * amount);
            }

            public static Mass operator /(Mass a, int amount)
            {
                return new Mass(a.Units.ToDouble() / amount);
            }

            public static Mass operator %(Mass a, int amount)
            {
                return new Mass(a.Units.ToDouble() % amount);
            }

            public static bool operator >(Mass a, IMass b)
            {
                return a.Constant.Equals(b.Constant) is false ? a.Units * b.Constant > b.TotalUnits : a.Units > b.TotalUnits;
            }

            public static bool operator <(Mass a, IMass b)
            {
                return (a > b) is false;
            }

            //<=, >=

            public static bool operator ==(Mass a, IMass b)
            {
                return a is null
                    ? b is null
                    : b is not null && (a.Constant.Equals(b.Constant) is false ? a.Units * b.Constant == b.TotalUnits : a.Units == b.TotalUnits);
            }

            public static bool operator !=(Mass a, IMass b)
            {
                return (a == b) is false;
            }

            public override bool Equals(object obj)
            {
                return obj is IMass || base.Equals(obj);
            }

            public override int GetHashCode()
            {
                return Constant.GetHashCode() << 16 | Units.GetHashCode() >> 16;
            }
        }
    }

    /// <summary>
    /// A description of energy with respect to physical and mathematical properties.
    /// </summary>
    public static class Energies
    {
        /// <summary>
        /// The interface of the <see cref="IUnit"/> which describes conversions to and from energy.
        /// </summary>
        public interface IEnergy : IUnit
        {
            /// <summary>
            /// Gets the <see cref="Number"/> which implies the total joules of the energy
            /// </summary>
            Number TotalJoules { get; }

            //Number TotalNewtons { get; }

            //TotalCharge or TotalColumbs is useful

            //ToDistance

            //ToWavelength

            //Etc
        }

        /// <summary>
        /// A representation of energy in the form of math.
        /// This class can be useful for converting a <see cref="IUnit"/> to <see cref="IEnergy"/> for calulcations.
        /// <seealso href="https://en.wikipedia.org/wiki/Erg">Erg</seealso>, This class can be sub-classed or composed to define other units.
        /// </summary>
        public class Energy : UnitBase, IEnergy
        {

            public static implicit operator double(Energy t) { return t.Units.ToDouble(); }

            public static implicit operator Energy(double t) { return new Energy(t); }

            /// <summary>
            /// `(-0)`
            /// </summary>
            /// <remarks>
            /// Where as 0 would imply infinite `time` but not `frequency` and make such diambiguation quite difficult. (∞)
            /// </remarks>
            public static readonly Energy MinValue = -0D;

            /// <summary>
            /// `1`
            /// </summary>
            public static readonly Energy One = Joule;

            /// <summary>
            /// `0`
            /// </summary>
            public static readonly Energy Zero = 0D;

            //Should be Number to avoid readonly ValueType

            public const double ITUCaloriesPerJoule = 0.23884589663;

            public const double BtusPerJoule = 0.00094781707775;

            public const double ThermochemicalBtusPerJoule = 0.00094845138281;

            public const double DekajoulesPerJoule = 0.1;

            public const double NanojoulesPerJoule = 1e+9;

            //* 0.0000001 Converts from erg to Newtons | Joule
            public const double ErgPerJoule = 1e+7;

            public const double Joule = 1;

            public const double ExajoulesPerJoule = 1.0e-18;

            public const double TerajoulesPerJoule = 1.0e-12;

            public const double DecijoulesPerJoule = 10;

            public const double CentijoulesPerJoule = 100;

            public const double TeraelectronvoltsPerJoule = 6241506.48;

            public const double FootPoundsPerJoule = 1.356;

            //public const double USThermoPerJoule = 1.055e+8

            public const double FemtojoulesPerJoule = 1000000000000000;

            public const double AuttojoulePerJoule = 1000000000000000000;
            private static readonly List<string> EnergySymbols =
            [
                "J",
                //"Btu",
            ];


            public Energy(double joules)
                : this(new Number(joules))
            {
            }

            public Energy()
                : base(Joule) { }

            public Energy(Energy other) : base(Joule, other) { }

            public Energy(Number joules)
                : base(Joule)
            {
                Units = joules;
            }

            /// <summary>
            /// Converts a corresponding <see cref="Masses.IMass"/> to Erg or <see cref="IEnergy"/>
            /// </summary>
            /// <param name="m"></param>
            public Energy(Masses.IMass m) :
                this(System.Math.Pow(m.TotalKilograms.ToDouble() * Velocities.Velocity.MaxValue.TotalMetersPerSecond.ToDouble(), 2))
            {

            }

            protected override List<string> m_Symbols
            {
                get
                {
                    return EnergySymbols;
                }
            }

            public Number TotalJoules
            {
                get { return Units; }
            }

            public Number Decijoules
            {
                get { return TotalJoules / DecijoulesPerJoule; }
            }

            public Number Dekajoules
            {
                get { return TotalJoules / DekajoulesPerJoule; }
            }

            public Number TotalErg
            {
                get { return TotalJoules * 1e+7; }
            }

            public Number Kilojoules
            {
                get { return TotalJoules * 1000; }
            }

            public Number TotalITUCalories
            {
                get { return TotalJoules / ITUCaloriesPerJoule; }
            }

            public static Energy FromITUCaloriesPerJoule(Number value)
            {
                return new Energy(value.ToDouble() * ITUCaloriesPerJoule);
            }

            public static Energy FromDekajoules(Number value)
            {
                return new Energy(value.ToDouble() * DekajoulesPerJoule);
            }

            public static Energy operator +(Energy a, int amount)
            {
                return new Energy(a.Units.ToDouble() + amount);
            }

            public static Energy operator -(Energy a, int amount)
            {
                return new Energy(a.Units.ToDouble() - amount);
            }

            public static Energy operator *(Energy a, int amount)
            {
                return new Energy(a.Units.ToDouble() * amount);
            }

            public static Energy operator /(Energy a, int amount)
            {
                return new Energy(a.Units.ToDouble() / amount);
            }

            public static Energy operator %(Energy a, int amount)
            {
                return new Energy(a.Units.ToDouble() % amount);
            }

            public static bool operator >(Energy a, IEnergy b)
            {
                return a.Constant.Equals(b.Constant) is false ? a.Units * b.Constant > b.TotalUnits : a.Units > b.TotalUnits;
            }

            public static bool operator <(Energy a, IEnergy b)
            {
                return (a > b) is false;
            }

            public static bool operator ==(Energy a, IEnergy b)
            {
                return a is null
                    ? b is null
                    : b is not null && (a.Constant.Equals(b.Constant) is false ? a.Units * b.Constant == b.TotalUnits : a.Units == b.TotalUnits);
            }

            public static bool operator !=(Energy a, IEnergy b)
            {
                return (a == b) is false;
            }

            //public static Energy operator ~(Energy a)
            //{
            //    throw new NotImplementedException();
            //}

            //public static Energy operator !(Energy a)
            //{
            //    throw new NotImplementedException();
            //}

            //public static Energy operator &(Energy a, Energy b)
            //{
            //    throw new NotImplementedException();
            //}

            //public static Energy operator |(Energy a, Energy b)
            //{
            //    throw new NotImplementedException();
            //}

            //public static Energy operator <<(Energy a, int f)
            //{
            //    throw new NotImplementedException();
            //}

            //public static Energy operator >>(Energy a, int f)
            //{
            //    throw new NotImplementedException();
            //}

            public override bool Equals(object obj)
            {
                return obj is IEnergy ? obj as IEnergy == this : base.Equals(obj);
            }

            public override int GetHashCode()
            {
                return Constant.GetHashCode() << 16 | Units.GetHashCode() >> 16;
            }

        }

        /// <summary>
        /// An example derivation of <see cref="Energy"/> in Nanojoules.
        /// This example would be more succinct as 2 methods within Energy which should be a partial class. (ToNanoEnergy, FromNanoEnergy)
        /// </summary>
        public class NanoEnergy : Energy
        {

            public static implicit operator double(NanoEnergy t) { return t.Units.ToDouble(); }

            public static implicit operator NanoEnergy(double t) { return new NanoEnergy(t); }

            /// <summary>
            /// `(-0)`
            /// </summary>
            /// <remarks>
            /// Where as 0 would imply infinite `time` but not `frequency` and make such diambiguation quite difficult. (∞)
            /// </remarks>
            public static new readonly NanoEnergy MinValue = -0D;

            /// <summary>
            /// `1`
            /// </summary>
            public static new readonly NanoEnergy One = Energy.NanojoulesPerJoule;

            /// <summary>
            /// `0`
            /// </summary>
            public static new readonly NanoEnergy Zero = 0D;

            /// <summary>
            /// Constructs a new instance
            /// </summary>
            /// <param name="nanoJoules"></param>
            public NanoEnergy(double nanoJoules) : base(nanoJoules / Energy.NanojoulesPerJoule)
            {

            }

            /// <summary>
            /// Constructs a new instance
            /// </summary>
            /// <param name="energy"></param>
            public NanoEnergy(Energy energy) : base(energy.TotalJoules * Energy.NanojoulesPerJoule) { }
        }
    }

    /// <summary>
    /// A class which is useful for describing velocity.
    /// </summary>
    public static class Velocities
    {
        public interface IVelocity : IUnit
        {
            Number TotalMetersPerSecond { get; }
        }

        /// <summary>
        /// A class which is useful for calculations involing speed / velocity
        /// </summary>
        public class Velocity : UnitBase, IVelocity
        {
            //Should be Number to avoid readonly ValueType

            public const double FeetPerSecond = 3.28084;

            public const double MilesPerHour = 2.23694;

            public const double KilometersPerHour = 3.6;

            public const double Knots = 1.94384;

            public const double MetersPerSecond = 1;

            public static readonly Velocity MaxValue = new(Physics.c);//the speed of light = 299 792 458 meters per second

            private static readonly List<string> VelocitySymbols =
            [
                "mph",
                "fps",
                "kph",
                "mps",
            ];

            public Velocity()
                : base(MetersPerSecond) { }

            public Velocity(Number metersPerSecond)
                : base(MetersPerSecond)
            {
                Units = metersPerSecond;
            }

            public Velocity(Velocity other) : base(MetersPerSecond, other) { }

            public Velocity(Number value, Velocity other) : base(MetersPerSecond, other) { Units = value; }

            protected override List<string> m_Symbols
            {
                get
                {
                    return VelocitySymbols;
                }
            }

            //Todo, virtual not needed with interface.

            public virtual Number TotalMetersPerSecond
            {
                get { return Units; }
            }

            public virtual Number TotalMilesPerHour
            {
                get { return TotalMetersPerSecond * MilesPerHour; }
            }

            public virtual Number TotalFeetPerSecond
            {
                get { return TotalMetersPerSecond * FeetPerSecond; }
            }

            public virtual Number TotalKilometersPerHour
            {
                get { return TotalMetersPerSecond * KilometersPerHour; }
            }

            public static Velocity FromKnots(Number value)
            {
                return new Velocity(value.ToDouble() * Knots);
            }

            public static Velocity operator +(Velocity a, int amount)
            {
                return new Velocity(a.Units.ToDouble() + amount);
            }

            public static Velocity operator -(Velocity a, int amount)
            {
                return new Velocity(a.Units.ToDouble() - amount);
            }

            public static Velocity operator *(Velocity a, int amount)
            {
                return new Velocity(a.Units.ToDouble() * amount);
            }

            public static Velocity operator /(Velocity a, int amount)
            {
                return new Velocity(a.Units.ToDouble() / amount);
            }

            public static Velocity operator %(Velocity a, int amount)
            {
                return new Velocity(a.Units.ToDouble() % amount);
            }

            public static bool operator >(Velocity a, IVelocity b)
            {
                return a.Constant.Equals(b.Constant) is false ? a.Units * b.Constant > b.TotalUnits : a.Units > b.TotalUnits;
            }

            public static bool operator <(Velocity a, IVelocity b)
            {
                return !(a > b);
            }

            public static bool operator ==(Velocity a, IVelocity b)
            {
                return a is null
                    ? b is null
                    : b is not null && (a.Constant.Equals(b.Constant) is false ? a.Units * b.Constant == b.TotalUnits : a.Units == b.TotalUnits);
            }

            public static bool operator !=(Velocity a, IVelocity b)
            {
                return !(a == b);
            }

            public override bool Equals(object obj)
            {
                return obj is IVelocity ? obj as IVelocity == this : base.Equals(obj);
            }

            public override int GetHashCode()
            {
                return Constant.GetHashCode() << 16 | Units.GetHashCode() >> 16;
            }
        }
    }

    /// <summary>
    /// A class which is useful for describing forces in newtons
    /// </summary>
    public static class Forces
    {
        /// <summary>
        /// An interface which provides access to the <see cref="TotalNewtons"/> of the <see cref="IForce"/>
        /// </summary>
        public interface IForce : IUnit
        {
            /// <summary>           
            /// </summary>
            Number TotalNewtons { get; }
        }

        /// <summary>
        /// A class which is usefl for representing and converting to newtonian representation.
        /// </summary>
        /// <remarks>
        /// newton is the unit for force
        /// joules is the unit for work done
        /// by definition, work done = force X distance
        /// so multiply newton by metre to get joules
        /// 1 newton = 1 joule/meter
        /// </remarks>
        public class Force : UnitBase, IForce
        {

            public static Energies.Energy ToEnergy(Distances.IDistance d)
            {
                return new Energies.Energy(d.TotalMeters.ToDouble());
            }

            public static implicit operator double(Force t) { return t.Units.ToDouble(); }

            public static implicit operator Force(double t) { return new Force(t); }

            public const double Newton = 1D;

            //0.0000001 Converts from erg
            //10000000 Converts to erg

            private static readonly List<string> ForceSymbols =
            [
                "N"
            ];

            /// <summary>
            /// Constructs the default 1 newton = 1 joule/meter
            /// </summary>
            public Force()
                : base(Newton)
            {
            }

            /// <summary>
            /// Constructs a newtonian <see cref="Force"/> with the given parameters
            /// </summary>
            /// <param name="value">The newtonian value which describes to the <see cref="TotalNewtons"/></param>
            public Force(double value)
                : base(Newton)
            {
                Units = value;
            }

            /// <summary>
            /// Constructs a newtonian <see cref="Force"/> from another <see cref="Force"/>
            /// </summary>
            /// <param name="other">The other <see cref="Force"/></param>
            public Force(Force other) : base(Newton, other) { }

            /// <summary>
            /// Constructs a newtonian <see cref="Force"/> from another <see cref="Force"/> with the specified parameters
            /// </summary>
            /// <param name="value">The newtonian value which describes to the <see cref="TotalNewtons"/></param>
            /// <param name="other">The other <see cref="Force"/></param>
            public Force(Number value, Force other) : base(Newton, other) { Units = value; }

            protected override List<string> m_Symbols
            {
                get
                {
                    return ForceSymbols;
                }
            }

            public virtual Number TotalNewtons
            {
                get { return Units; }
            }

            public static Force operator +(Force a, int amount)
            {
                return new Force(a.Units.ToDouble() + amount);
            }

            public static Force operator -(Force a, int amount)
            {
                return new Force(a.Units.ToDouble() - amount);
            }

            public static Force operator *(Force a, int amount)
            {
                return new Force(a.Units.ToDouble() * amount);
            }

            public static Force operator /(Force a, int amount)
            {
                return new Force(a.Units.ToDouble() / amount);
            }

            public static Force operator %(Force a, int amount)
            {
                return new Force(a.Units.ToDouble() % amount);
            }

            public static bool operator >(Force a, IForce b)
            {
                return a.Constant.Equals(b.Constant) is false ? a.Units * b.Constant > b.TotalUnits : a.Units > b.TotalUnits;
            }

            public static bool operator <(Force a, IForce b)
            {
                return (a > b) is false;
            }

            public static bool operator ==(Force a, IForce b)
            {
                return a is null
                    ? b is null
                    : b is not null && (a.Constant.Equals(b.Constant) is false ? a.Units * b.Constant == b.TotalUnits : a.Units == b.TotalUnits);
            }

            public static bool operator !=(Force a, IForce b)
            {
                return (a == b) is false;
            }

            public override bool Equals(object obj)
            {
                return obj is IForce ? ((IForce)obj) == this : base.Equals(obj);
            }

            public override int GetHashCode()
            {
                return Constant.GetHashCode() << 16 | Units.GetHashCode() >> 16;
            }
        }
    }

    /// <summary>
    /// A class which is useful for dealing with the frequency and velocity of wave forms
    /// </summary>
    public static class Wavelengths
    {
        public interface IWavelength : IUnit
        {
            Distances.IDistance TotalMeters { get; }

            Frequencies.IFrequency TotalHz { get; }

            Energies.IEnergy TotalJoules { get; }

            Velocities.IVelocity TotalVelocity { get; }

            //Shape is Waveform
            //ToWaveform(Shape)
        }

        /*
        newton is the unit for Wavelength
        joules is the unit for work done
        by definition, work done = Wavelength X distance
        so multiply newton by metre to get joules
        1 newton = 1 joule/meter
         */
        /// <summary>
        /// A class which is useful for computing the frequency and velocity of waves from physical <see cref="Forces"/>
        /// </summary>
        public class Wavelength : UnitBase, IWavelength
        {

            public static implicit operator double(Wavelength t) { return t.Units.ToDouble(); }

            public static implicit operator Wavelength(double t) { return new Wavelength(t); }

            private static readonly List<string> WavelengthSymbols =
            [
                "nm",
                "μm",
                "m"
            ];

            public const double Nm = 1D;

            public Wavelength()
                : base(Nm)
            {
            }

            public Wavelength(Distances.Distance meters)
                : base(Nm)
            {
                Units = meters.TotalMeters * Distances.Distance.NanometersPerMeter;
            }

            public Wavelength(double nanometers)
                : base(Nm)
            {
                Units = nanometers;
            }

            public Wavelength(Frequencies.Frequency hZ)
                : base(Nm)
            {
                Units = Velocities.Velocity.MaxValue.Units.ToComplex() * hZ.TotalHertz.ToComplex();
            }

            public Wavelength(Wavelength other) : base(Nm, other) { }

            public Wavelength(Number value, Wavelength other) : base(Nm, other) { Units = value; }

            protected override List<string> m_Symbols
            {
                get
                {
                    return WavelengthSymbols;
                }
            }

            public virtual Distances.IDistance TotalMeters
            {
                get { return new Distances.Distance(Units.ToComplex() * Distances.Distance.NanometersPerMeter); }
            }

            public virtual Velocities.IVelocity TotalVelocity
            {
                get { return new Velocities.Velocity(Velocities.Velocity.MaxValue.Units.ToDouble() / Units.ToDouble()); }
            }

            public virtual Frequencies.IFrequency TotalHz
            {
                get { return new Frequencies.Frequency(TotalVelocity.TotalMetersPerSecond.ToDouble() * TotalMeters.TotalUnits.ToDouble()); }
            }

            public virtual Energies.IEnergy TotalJoules
            {
                get { return new Energies.Energy(new Number(Physics.hc / TotalMeters.TotalUnits.ToDouble())); }
            }

            public static Wavelength operator +(Wavelength a, int amount)
            {
                return new Wavelength(a.Units.ToDouble() + amount);
            }

            public static Wavelength operator -(Wavelength a, int amount)
            {
                return new Wavelength(a.Units.ToDouble() - amount);
            }

            public static Wavelength operator *(Wavelength a, int amount)
            {
                return new Wavelength(a.Units.ToDouble() * amount);
            }

            public static Wavelength operator /(Wavelength a, int amount)
            {
                return new Wavelength(a.Units.ToDouble() / amount);
            }

            public static Wavelength operator %(Wavelength a, int amount)
            {
                return new Wavelength(a.Units.ToDouble() % amount);
            }

            public static bool operator >(Wavelength a, IWavelength b)
            {
                return a.Constant.Equals(b.Constant) is false ? a.Units * b.Constant > b.TotalUnits : a.Units > b.TotalUnits;
            }

            public static bool operator <(Wavelength a, IWavelength b)
            {
                return !(a > b);
            }

            public static bool operator ==(Wavelength a, IWavelength b)
            {
                return a is null
                    ? b is null
                    : b is not null && (a.Constant.Equals(b.Constant) is false ? a.Units * b.Constant == b.TotalUnits : a.Units == b.TotalUnits);
            }

            public static bool operator !=(Wavelength a, IWavelength b)
            {
                return !(a == b);
            }

            public override bool Equals(object obj)
            {
                return obj is IWavelength ? obj as IWavelength == this : base.Equals(obj);
            }

            public override int GetHashCode()
            {
                return Constant.GetHashCode() << 16 | Units.GetHashCode() >> 16;
            }
        }
    }

    //Current ->     //https://en.wikipedia.org/wiki/Coulomb
    //Charge
}
