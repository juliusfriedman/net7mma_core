#region Copyright
/*
This file came from Managed Media Aggregation, You can always find the latest version @ https://net7mma.codeplex.com/
  
 Julius.Friedman@gmail.com / (SR. Software Engineer ASTI Transportation Inc. http://www.asti-trans.com)

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
#endregion

//ldc.i4.7

//Some interesting methods can be found here
//https://github.com/IllidanS4/SharpUtils/blob/a3b4da490537e361e6a5debc873c303023d83bf1/Unsafe/UnsafeTools.cs

#if NETMF

//The Micro Framework does not support unsafe code.
//Determine if a placeholder Unsafe class will be required there, if so then implement a new class here rather than check in each method

#else

namespace Media.Concepts.Classes
{
    /// <summary>
    /// Provides various functionality which can only be achieved with the used of unsafe code.
    /// </summary>
    public static unsafe class Unsafe
    {
        #region AddressOf

        /// <summary>
        /// Provides the current address of the given object.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static nint AddressOf(object obj)
        {
            if (obj is null) return nint.Zero;

            System.TypedReference reference = __makeref(obj);

            System.TypedReference* pRef = &reference;

            return (nint)pRef; //(&pRef)
        }

        /// <summary>
        /// Provides the current address of the given element
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="t"></param>
        /// <returns></returns>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static nint AddressOf<T>(T t)
        {
            System.TypedReference reference = __makeref(t);

            return *(nint*)(&reference);
        }

        //Basically As / Reinterpret function which is unsafe.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static nint AddressOf<T>(ref T t)
        {
            System.TypedReference reference = __makeref(t);

            return **(nint**)(&reference);
        }

        #endregion

        #region OffsetOf

        /// <summary>
        /// Returns the field offset of the unmanaged form of the managed type using <see cref="System.Runtime.InteropServices.Marshal.OffsetOf"/>
        /// </summary>
        /// <param name="t"></param>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static nint OffsetOf(System.Type t, string fieldName)
        {
            return System.Runtime.InteropServices.Marshal.OffsetOf(t, fieldName);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static nint OffsetOf(object o, string fieldName)
        {
            return System.Runtime.InteropServices.Marshal.OffsetOf(o.GetType(), fieldName);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static nint OffsetOf<T>(string fieldName)
        {
            return System.Runtime.InteropServices.Marshal.OffsetOf(typeof(T).GetType(), fieldName);
        }

        #endregion

        #region BytesPer

        //http://stackoverflow.com/questions/4764573/why-is-typedreference-behind-the-scenes-its-so-fast-and-safe-almost-magical

        internal static class ArrayOfTwoElements<T>
        {
            internal static readonly T[] Value = new T[2];

            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            internal static int AddressingDifference() { return (int)(System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(ArrayOfTwoElements<T>.Value, 1).ToInt64() - System.Runtime.InteropServices.Marshal.UnsafeAddrOfPinnedArrayElement(ArrayOfTwoElements<T>.Value, 0).ToInt64()); }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal static uint BytesPer<T>()
        {
            System.TypedReference
                    elem1 = __makeref(ArrayOfTwoElements<T>.Value[0]),
                    elem2 = __makeref(ArrayOfTwoElements<T>.Value[1]);

            return (uint)((byte*)*(nint*)(&elem2) - (byte*)*(nint*)(&elem1));
        }

        #endregion

        #region Read

        /// <summary>
        /// <see cref="Read"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="address"></param>
        /// <returns></returns>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal static T Read<T>(nint address)
        {
            T obj = default(T);

            System.TypedReference tr = __makeref(obj);

            *(nint*)(&tr) = address;

            return __refvalue(tr, T);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal static bool TryRead<T>(nint address, ref T t)
        {
            System.TypedReference tr = __makeref(t);

            *(nint*)(&tr) = address;

            t = __refvalue(tr, T);

            return true;
        }

        /// <summary>
        /// This bypasses the restriction that you can't have a pointer to.
        /// letting you write very high-performance generic code.
        /// It's dangerous if you don't know what you're doing, but very worth if you do.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        static object Read(nint address)
        {
            //Create memory for a boxed object
            object box = default(object);

            //Make a TypedReference to the object created
            System.TypedReference tr = __makeref(box);

            //This is equivalent to shooting yourself in the foot
            //but it's the only high-perf solution in some cases
            //it sets the first field of the TypedReference (which is a pointer)
            //to the address you give it, then it dereferences the value.
            //Better be 10000% sure that your type T is unmanaged/blittable...
            *(nint*)(&tr) = address;

            //Using __refvalue
            //return __refvalue(tr, object);

            //Convert from the TypedReference back to object
            return System.TypedReference.ToObject(tr);
        }


        #endregion

        #region Write

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal static void Write<T>(nint address, ref T what) { Write<T>(address, ref what, Unsafe.ArrayOfTwoElements<T>.AddressingDifference()); }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal static void Write<T>(nint address, ref T what, int size) { Write<T>(address, ref what, ref size); }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal static void Write<T>(nint address, ref T what, ref int size) { System.Buffer.MemoryCopy((void*)AddressOf(what), (void*)address, size, size); }

        #endregion

        #region Create

        ////Here for reference, I couldn't get them to work as they advertised.

        //public static T[] Create<T>(nint source, int length)
        //{
        //    T[] output = new T[length];

        //    for (int i = 0; i < length; i++)
        //    {
        //        T src = Unsafe.Read<T>(source);

        //        Write<T>(source, src)
        //    }
        //}

        //http://stackoverflow.com/questions/621493/c-sharp-unsafe-value-type-array-to-byte-array-conversions/3577227#3577227

        [System.CLSCompliant(false)]
        public unsafe static T[] Create<T>(void* source, int length)
        {
            System.Type type = typeof(T);

            int sizeInBytes = System.Runtime.InteropServices.Marshal.SizeOf(type);

            T[] output = new T[length];

            if (type.IsPrimitive)
            {
                // Make sure the array won't be moved around by the GC 
                System.Runtime.InteropServices.GCHandle handleOutput = default(System.Runtime.InteropServices.GCHandle);

                try
                {
                    handleOutput = System.Runtime.InteropServices.GCHandle.Alloc(output, System.Runtime.InteropServices.GCHandleType.Pinned);

                    int byteLength = length * sizeInBytes;

                    // There are faster ways to do this, particularly by using wider types or by 
                    // handling special lengths.                     
                    //for (int i = 0; i < byteLength; i++)
                    //    destination[i] = ((byte*)source)[i];

                    //E,g, like this... the problem is that handle doesn't point to the array elements...
                    //Could instead give a T[] source or IntPtr.                     

                    System.Buffer.MemoryCopy(source, (void*)handleOutput.AddrOfPinnedObject(), byteLength, byteLength);
                }
                finally
                {
                    if (handleOutput.IsAllocated) handleOutput.Free();
                }
            }
            else if (type.IsValueType)
            {
                if (false == type.IsLayoutSequential && false == type.IsExplicitLayout)
                {
                    throw new System.InvalidOperationException(string.Format("{0} does not define a StructLayout attribute", type));
                }

                nint sourcePtr = new nint(source);

                for (int i = 0; i < length; i++)
                {
                    nint p = new nint((byte*)source + i * sizeInBytes);

                    output[i] = (T)System.Runtime.InteropServices.Marshal.PtrToStructure(p, typeof(T));
                }
            }
            else
            {
                throw new System.InvalidOperationException(string.Format("{0} is not supported", type));
            }

            return output;
        }

        static unsafe T[] MakeArray<T>(void* t, int length, int tSizeInBytes) where T : struct
        {
            T[] result = new T[length];
            for (int i = 0; i < length; i++)
            {
                nint p = new nint((byte*)t + (i * tSizeInBytes));
                result[i] = (T)System.Runtime.InteropServices.Marshal.PtrToStructure(p, typeof(T));
            }

            return result;
        }

        //byte[] b = MakeArray<byte>(pBytes, lenBytes, sizeof(byte));

        //unsafe static void CreateUsage(string[] args)
        //{
        //    var arrayDouble = Enumerable.Range(1, 1024)
        //                                .Select(i => (double)i)
        //                                .ToArray();

        //    fixed (double* p = arrayDouble)
        //    {
        //        var array2 = Create<double>(p, arrayDouble.Length);

        //        Assert.AreEqual(arrayDouble, array2);
        //    }

        //    var arrayPoint = Enumerable.Range(1, 1024)
        //                               .Select(i => new Point(i, i * 2 + 1))
        //                               .ToArray();

        //    fixed (Point* p = arrayPoint)
        //    {
        //        var array2 = Create<Point>(p, arrayPoint.Length);

        //        Assert.AreEqual(arrayPoint, array2);
        //    }
        //}

        #endregion

        #region Other

        //https://blog.adamfurmanek.pl/blog/2016/06/25/custom-memory-allocation-in-c-part-4/

        public static object AllocateAt(int address, nint typeHandle)
        {
            System.Runtime.InteropServices.Marshal.WriteInt32((nint)(address), 0);
            System.Runtime.InteropServices.Marshal.WriteInt32((nint)(address + 1 * sizeof(int)), (int)typeHandle);
            object createdObject = null;
            unsafe
            {
                System.TypedReference newObjectReference = __makeref(createdObject);
                *(int*)(*(int*)&newObjectReference) = address + 1 * sizeof(int);
            }
            return createdObject;
        }

        public static bool TryAllocateAt<T>(nint address, nint typeHandle, out T t)
        {
            System.Runtime.InteropServices.Marshal.WriteInt32((nint)(address), 0);
            System.Runtime.InteropServices.Marshal.WriteInt32((nint)(address + 1 * sizeof(int)), (int)typeHandle);
            t = default(T);
            CommonIntermediateLanguage.InitBlock(address + 1 * sizeof(int), (byte)0, CommonIntermediateLanguage.SizeOf<T>());
            unsafe
            {
                System.TypedReference newObjectReference = __makeref(t);
                *(int*)(*(int*)&newObjectReference) = (address + 1 * sizeof(int)).ToInt32();
            }
            return true;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoOptimization)]
        static object CreateObject() { return new System.Object(); }

        private static nint GetAllocMethodAddress()
        {
            // Get handle to method creating object
            var methodHandle = Common.Extensions.ExpressionExtensions.SymbolExtensions.GetMethodInfo(() => CreateObject()).MethodHandle;

            // JIT methods
            System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(methodHandle);

            // Get address of jitted method
            nint methodAddress = System.Runtime.InteropServices.Marshal.ReadIntPtr(methodHandle.Value, 8);

            // Call to internal function differs between builds
#if DEBUG
            int offset = nint.Size == 8 ? 0x2D : 0x22;
#else
            int offset = nint.Size == 8 ? 0x1A : 0xE;
#endif

            // Read jump offset
            int jumpOffset = 0;

            //Byte by byte convert to integer
            for (int i = 1; i < 5; ++i)
            {
                jumpOffset = jumpOffset + (System.Runtime.InteropServices.Marshal.ReadByte(methodAddress, offset + i) << (i - 1) * 8);
            }

            // Calculate absolute address
            nint absoluteAddress = methodAddress + offset + jumpOffset + 1 + 4; // 1 byte for jmp instruction, 4 bytes for relative address

            return absoluteAddress;
        }

        //      public static void HijackNew(System.Reflection.MethodBase method)
        //      {
        //          var defaultAllocAddress = (long)GetAllocMethodAddress();

        //          var myAllocAddress = (long)MethodHelper.GetMethodAddress(method);

        //          int offset = (int)(myAllocAddress - defaultAllocAddress - 4 - 1); // 4 bytes for relative address and one byte for opcode
        //          byte[] instruction = {
        //      0xE9, // Long jump instruction
        //(byte)(offset & 0xFF),
        //      (byte)((offset >> 8) & 0xFF),
        //      (byte)((offset >> 16) & 0xFF),
        //      (byte)((offset >> 24) & 0xFF)
        //  };

        //          //UnlockPage((IntPtr)defaultAllocAddress);
        //          System.Runtime.InteropServices.Marshal.Copy(instruction, 0, (nint)defaultAllocAddress, instruction.Length);
        //      }

        #endregion

        #region MakeTypedReference

        //http://stackoverflow.com/questions/26998758/why-is-typedreference-maketypedreference-so-constrained

        //private static readonly System.Reflection.MethodInfo InternalMakeTypedReferenceMethod = typeof(System.TypedReference).GetMethod("InternalMakeTypedReference", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance);
        //Needs DelegatExtensions
        //private static readonly System.Type InternalMakeTypedReferenceDelegateType = ReflectionTools.NewCustomDelegateType(InternalMakeTypedReferenceMethod.ReturnType, InternalMakeTypedReferenceMethod.GetParameters().Select(p => p.ParameterType).ToArray());
        //private static readonly System.Delegate InternalMakeTypedReference = System.Delegate.CreateDelegate(InternalMakeTypedReferenceDelegateType, InternalMakeTypedReferenceMethod);

        //[System.CLSCompliant(false)]
        //public static void MakeTypedReference([System.Runtime.InteropServices.Out]System.TypedReference* result, object target, params System.Reflection.FieldInfo[] fields)
        //{
        //    nint ptr = (nint)result;
        //    nint[] flds = new nint[fields.Length];
        //    System.Type lastType = target.GetType();
        //    for (int i = 0; i < fields.Length; i++)
        //    {
        //        var field = fields[i];
        //        if (field.IsStatic)
        //        {
        //            throw new System.ArgumentException("Field cannot be static.", "fields");
        //        }
        //        flds[i] = field.FieldHandle.Value;
        //        lastType = field.FieldType;
        //    }
        //    //InternalMakeTypedReference.DynamicInvoke(ptr, target, flds, lastType);
        //}

        #endregion

        #region Structures

        //Adapted from 'Mash`s' response on StackOverflow @ http://stackoverflow.com/questions/621493/c-sharp-unsafe-value-type-array-to-byte-array-conversions/3577227#3577227
        //Also SharpUtils UnsafeTools VariadicUnion

        /// <summary>
        /// Provides a structure which can be used to inspect objects or convert arrays quickly.
        /// </summary>
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        public struct Inspector
        {
            #region Methods

            internal nint IntPtr<T>() { return AddressOf<object>(ref Object); }

            public static byte[] GetBytes(float[] floats)
            {
                Inspector i = new Inspector();
                i.FloatArray = floats;
                i.Length.Value = floats.Length << 2; //* 4;
                return i.ByteArray;
            }

            public static float[] GetFloats(byte[] bytes)
            {
                Inspector i = new Inspector();
                i.ByteArray = bytes;
                i.Length.Value = bytes.Length >> 2; // / 4;
                return i.FloatArray;
            }

            public static byte[] GetTop4BytesFrom(object obj)
            {
                Inspector i = new Inspector();
                i.Object = obj;
                return new byte[]
                {
                    i.Octets.Byte_0,
                    i.Octets.Byte_1,
                    i.Octets.Byte_2,
                    i.Octets.Byte_3
                };
            }

            public static byte[] GetBytesFrom(object obj, int size)
            {
                Inspector i = new Inspector();
                i.Object = obj;
                i.Length.Value = size;
                return i.ByteArray;
            }

            #endregion

            #region Nested Types

            //See also.
            //https://blogs.msdn.microsoft.com/abhinaba/2012/02/02/wp7-clr-managed-object-overhead/

            //allows setting any of the array lengths...
            internal class ArrayLength { public int Value; }


            [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
            internal class Pointer
            {
                [System.Runtime.InteropServices.FieldOffset(0)]
                public nint Value;
            }

            internal class Bytes
            {
                public byte Byte_0;
                public byte Byte_1;
                public byte Byte_2;
                public byte Byte_3;
            }

            #endregion            

            [System.Runtime.InteropServices.FieldOffset(0)]
            internal System.Array Array;

            [System.Runtime.InteropServices.FieldOffset(0)]
            internal System.Char[] CharArray;

            [System.Runtime.InteropServices.FieldOffset(0)]
            internal System.Byte[] ByteArray;

            [System.Runtime.InteropServices.FieldOffset(0)]
            internal System.Single[] FloatArray;

            [System.Runtime.InteropServices.FieldOffset(0)]
            internal System.Object Object;

            [System.Runtime.InteropServices.FieldOffset(0)]
            internal System.String String;

            //Probably only need one of these.

            [System.Runtime.InteropServices.FieldOffset(0)]
            internal System.Text.StringBuilder StringBuilder;

            [System.Runtime.InteropServices.FieldOffset(0)]
            internal ArrayLength Length;

            [System.Runtime.InteropServices.FieldOffset(0)]
            internal Pointer AsPointer;

            [System.Runtime.InteropServices.FieldOffset(0)]
            internal Bytes Octets;
        }

        /// <summary>
        /// This struct is an union of some basic types. Each field points to the same location.
        /// </summary>
        /// <remarks><see href="https://github.com/IllidanS4/SharpUtils/blob/fd0e8fbab9fa45a23c9b380121952ef959df85bd/Unsafe/VariadicUnion.cs"/>SharpUtils</see></remarks>
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
        public struct VariadicUnion
        {
            [System.Runtime.InteropServices.FieldOffset(0)]
            public bool AsBoolean;

            [System.Runtime.InteropServices.FieldOffset(0)]
            public byte AsByte;

            [System.Runtime.InteropServices.FieldOffset(0)]
            public short AsInt16;

            [System.Runtime.InteropServices.FieldOffset(0)]
            public int AsInt32;

            [System.Runtime.InteropServices.FieldOffset(0)]
            public long AsInt64;

            [System.Runtime.InteropServices.FieldOffset(0)]
            [System.CLSCompliant(false)]
            public sbyte AsSByte;

            [System.Runtime.InteropServices.FieldOffset(0)]
            [System.CLSCompliant(false)]
            public ushort AsUInt16;

            [System.Runtime.InteropServices.FieldOffset(0)]
            [System.CLSCompliant(false)]
            public uint AsUInt32;

            [System.Runtime.InteropServices.FieldOffset(0)]
            [System.CLSCompliant(false)]
            public ulong AsUInt64;

            [System.Runtime.InteropServices.FieldOffset(0)]
            public float AsSingle;

            [System.Runtime.InteropServices.FieldOffset(0)]
            public double AsDouble;

            [System.Runtime.InteropServices.FieldOffset(0)]
            public decimal AsDecimal;

            [System.Runtime.InteropServices.FieldOffset(0)]
            public char AsChar;

            [System.Runtime.InteropServices.FieldOffset(0)]
            public System.DateTime AsDateTime;

            [System.Runtime.InteropServices.FieldOffset(0)]
            public nint AsIntPtr;

            [System.Runtime.InteropServices.FieldOffset(0)]
            public System.Guid AsGuid;
        }

        #endregion

        //returns Index of difference
        //bool MemCmp(source, dst, offset, size) => MemCmp(source, st, offset, size) > 0
        //int MemCmp(source, dst, offset, size)

        //labs MemoryUtils has a fairly good implementation, a similar approach could be taken here using Read and Write.

        #region Conversions

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static TResult ReinterpretCast<TOriginal, TResult>(/*this*/ TOriginal orig)
        {
            return Read<TResult>(AddressOf(orig));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static TResult ReinterpretCast<TOriginal, TResult>(ref TOriginal orig)
        {
            return Read<TResult>(AddressOf<TOriginal>(ref orig));
        }

        /// <summary>
		/// Performs a direct cast on the <typeparamref name="TFrom"/> argument to the same type, represented by <typeparamref name="TTo"/>.
		/// </summary>
		/// <param name="arg">The argument to cast.</param>
		/// <returns>The same value, expressed using <typeparamref name="TTo"/>.</returns>
		/// <exception cref="System.InvalidCastException">This exception is thrown when <typeparamref name="TFrom"/> and <typeparamref name="TTo"/> do not represent the same type.</exception>
		public static TTo FastCast<TFrom, TTo>(TFrom arg)
        {
            return __refvalue(__makeref(arg), TTo);
        }

        [System.CLSCompliant(false)]
        public unsafe static bool IsNegativeZero(double* d) //ref
        {
            //Check for -0.0
            return (*((long*)(d))).Equals(Common.Binary.NegativeZeroBits);
        }

        public unsafe static bool IsNegativeZero(ref double d)
        {
            //Make a pointer to the pointer, which when dereferenced can access the result.
            nint value = Unsafe.AddressOf(ref d);

            //Check for -0.0
            return (*((long*)((int*)&value))).Equals(Common.Binary.NegativeZeroBits);
        }

        [System.CLSCompliant(false)]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static unsafe long UInt64ToInt64Bits(ulong* x) //ref
        {
            return *((long*)x);
        }

        [System.CLSCompliant(false)]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static unsafe long UInt64ToInt64Bits(ref ulong x)
        {
            return *((long*)x);
        }

        [System.CLSCompliant(false)]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static unsafe long UInt64ToInt64Bits(ulong x)
        {
            return *((long*)&x);
        }

        [System.CLSCompliant(false)]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static unsafe ulong UInt64ToInt64Bits(long* x)
        {
            return *((ulong*)x);
        }

        [System.CLSCompliant(false)]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static unsafe ulong UInt64ToInt64Bits(ref long x)
        {
            return *((ulong*)x);
        }

        [System.CLSCompliant(false)]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static unsafe ulong UInt64ToInt64Bits(long x)
        {
            return *((ulong*)&x);
        }

        [System.CLSCompliant(false)]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static unsafe int UInt32ToInt32Bits(uint* x)
        {
            return *((int*)x);
        }

        [System.CLSCompliant(false)]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static unsafe int UInt32ToInt32Bits(uint x)
        {
            return *((int*)&x);
        }

        [System.CLSCompliant(false)]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static unsafe int UInt32ToInt32Bits(ref uint x)
        {
            return *((int*)x);
        }

        [System.CLSCompliant(false)]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static unsafe short Int32ToInt16Bits(int* x) //Int15Bits
        {
            return *((short*)x);
        }

        [System.CLSCompliant(false)]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static unsafe short Int32ToInt16Bits(int x) //Int15Bits
        {
            return *((short*)&x);
        }

        [System.CLSCompliant(false)]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static unsafe short Int32ToInt16Bits(ref int x) //Int15Bits
        {
            return *((short*)x);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static unsafe int SingleToInt32Bits(float f)
        {
            return *(int*)(&f);
        }

        [System.CLSCompliant(false)]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static unsafe int SingleToInt32Bits(float* f)
        {
            return *(int*)(f);
        }

        //[System.CLSCompliant(false)]
        //[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        //public static unsafe int SingleToInt32Bits(ref float f)
        //{
        //    return *(int*)(f);
        //}

        [System.CLSCompliant(false)]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static unsafe float Int32BitsToSingle(int* i)
        {
            return *(float*)(i);
        }


        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static unsafe float Int32BitsToSingle(int i)
        {
            return *(float*)(&i);
        }

        [System.CLSCompliant(false)]
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static unsafe float Int32BitsToSingle(ref int i)
        {
            return *(float*)(i);
        }

        #endregion

        #region String

        /// <summary>
        /// Attempts to modify the given string at the given index with the new value.
        /// </summary>
        /// <param name="toModify"></param>
        /// <param name="index"></param>
        /// <param name="newValue"></param>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public unsafe static bool UnsafeTryModifyString(string toModify, int index, char newValue)
        {
            try
            {
                UnsafeModifyString(toModify, ref index, ref newValue);

                return true;
            }
            catch
            {
                return false;
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public unsafe static void UnsafeModifyString(string toModify, ref int index, ref char newValue)
        {
            fixed (char* str = toModify)
            {
                str[index] = newValue;
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void NativeModifyString(string toModify, ref int index, ref char newValue)
        {
            System.Runtime.InteropServices.Marshal.WriteInt16(toModify, index, newValue);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static void NativeReadString(string toModify, ref int index, out char value)
        {
            value = (char)System.Runtime.InteropServices.Marshal.ReadInt16(toModify, index);
        }

        //copy many chars...

        #endregion

        #region Pinnable

        /// <summary>
        /// Helper
        /// </summary>
        public sealed class Pinnable //<T>
        {
            /// <summary>
            /// As an address, relative to the fields in this object, if this field is pinned then the object and it's derivation can't be moved.
            /// </summary>
            internal byte Pin;
        }

        #endregion

        #region UnsafeList

        //https://blog.adamfurmanek.pl/blog/2018/09/29/custom-memory-allocation-in-c-part-11/

        public class UnsafeList<T> : System.Collections.IEnumerable where T : class
        {
            private readonly int _elementSize;
            private T _target;
            private int[] _storage;
            private int _currentIndex = -1;
            private readonly T[] _elements;

            public UnsafeList(int size, int elementSize)
            {
                _elementSize = elementSize + 1;
                _storage = new int[size * _elementSize];
                _elements = new T[size];
                _target = default(T);
            }

            public void Add(T item)
            {
                _currentIndex++;

                unsafe
                {
                    System.TypedReference reference = __makeref(item);
                    int* itemAddress = (int*)(*(int*)*(int*)&reference - 4);

                    for (int i = 1; i < _elementSize; ++i)
                    {
                        _storage[_currentIndex * _elementSize + i] = *itemAddress;
                        itemAddress = itemAddress + 1;
                    }

                    reference = __makeref(_storage);
                    _storage[_currentIndex * _elementSize] = *(int*)*(int*)&reference + _currentIndex * _elementSize * 4 + 16;
                    _elements[_currentIndex] = GetInternal(_currentIndex);
                }
            }

            private T GetInternal(int index)
            {
                unsafe
                {
                    System.TypedReference reference = __makeref(_target);
                    *(int*)*(int*)&reference = _storage[index * _elementSize];
                    T result = __refvalue(reference, T);
                    return result;
                }
            }

            public T Get(int index)
            {
                return _elements[index];
            }

            public System.Collections.Generic.IEnumerator<T> GetEnumerator()
            {
                return new CustomEnumerator<T>(this);
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            private class CustomEnumerator<R> : System.Collections.Generic.IEnumerator<R> where R : class
            {
                private readonly UnsafeList<R> _list;
                private int _index;
                private R _current;
                private int size;

                public CustomEnumerator(UnsafeList<R> list)
                {
                    _list = list;
                    _index = -1;
                    size = _list._currentIndex;
                }

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    if (++_index <= size)
                    {
                        _current = _list.Get(_index);
                        return true;
                    }
                    return false;
                }

                public void Reset()
                {
                    _index = -1;
                }

                public R Current
                {
                    get { return _current; }
                }

                object System.Collections.IEnumerator.Current
                {
                    get { return _current; }
                }
            }
        }

        #endregion

    }
}

#endif


