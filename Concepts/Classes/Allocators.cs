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

namespace Media.Concepts.Classes
{
    /// <summary>
    /// 
    /// </summary>
    public enum Privileges
    {
        Read,
        Write,
        Execute,
        All = Read | Write | Execute
    }

    #region Interfaces

    /// <summary>
    /// Provides an interface which allows custom memory management
    /// </summary>
    public interface IStorageAllocator
    {
        /// <summary>
        /// Allocates memory
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        nint Allocate(long size);

        /// <summary>
        /// Releases memory
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="size"></param>
        void Release(nint pointer, long size);

        /// <summary>
        /// Sets the <see cref="Privileges"/> on the pointer
        /// </summary>
        /// <param name="pointer"></param>
        /// <param name="permissions"></param>
        Privileges SetPrivileges(nint pointer, Privileges privileges);
    }

    /// <summary>
    /// 
    /// </summary>
    public interface IAllocator
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        nint Allocate(int size);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="p"></param>
        /// <param name="size"></param>
        void Free(nint p, int size);
    }

    #endregion

    #region Classes

    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// <see href="http://www.codeproject.com/Articles/32125/Unmanaged-Arrays-in-C-No-Problem">CodeProject</see>
    /// </remarks>
    public class UnmanagedAllocator : IStorageAllocator
    {
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public nint Allocate(long size)
        {
            //Unsafe.AddressOf(ref size);
            return System.Runtime.InteropServices.Marshal.AllocHGlobal((int)size);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Release(nint pointer, long size)
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public Privileges SetPrivileges(nint pointer, Privileges privileges)
        {
            throw new System.NotImplementedException();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public unsafe static void Resize<T>(nint pointer, int newElementCount)
        {
            System.Runtime.InteropServices.Marshal.ReAllocHGlobal(pointer, new nint(Unsafe.ArrayOfTwoElements<T>.AddressingDifference() * newElementCount));
        }
    }

    /// <summary>
    /// Allocates aligned memory for any type
    /// </summary>
    /// <see href="http://stackoverflow.com/questions/1951290/memory-alignment-of-classes-in-c">StackOverflow</see>
    /// <typeparam name="T"></typeparam>
    public class AlignedAllocator<T> : IStorageAllocator where T : new()
    {
        public nint Allocate(long size)
        {
            System.Collections.Generic.LinkedList<T> candidates = new();

            nint pointer = nint.Zero;

            bool continue_ = true;

            size += System.Runtime.InteropServices.Marshal.SizeOf(typeof(T)) % nint.Size;

            int wide = nint.Size * 8; // 32 or 64

            while (continue_)
            {
                //If there is no size make a new object which allocated 12 or more bytes to make a gap, next allocation should be aligned.
                if (size is 0)
                {
                    object gap = new();
                }

                candidates.AddLast(new T());

                #region Unused

                //Crashed compiler... Base method is not marked unsafe?

                //unsafe{

                ////Make a local reference to the result
                //System.TypedReference trResult = __makeref(candidates.Last.Value);

                ////Make a pointer to the local reference
                //nint localReferenceResult = *(nint*)(&trResult);

                //}

                #endregion

                pointer = Concepts.Classes.CommonIntermediateLanguage.As<T, nint>(candidates.Last.Value);

                #region Unused

                //Fix the handle
                //System.Runtime.InteropServices.GCHandle handle = System.Runtime.InteropServices.GCHandle.Alloc(candidates.Last.Value, System.Runtime.InteropServices.GCHandleType.Pinned);

                //To determine if its sized correctly.
                //pointer = handle.AddrOfPinnedObject();

                #endregion

                continue_ = (pointer.ToInt64() & nint.Size - 1) != 0 || (pointer.ToInt64() % wide) == 24;

                #region Unused

                //handle.Free();

                #endregion
            }

            return pointer;
        }

        public void Release(nint pointer, long size)
        {
            //GC Managed
        }

        public Privileges SetPrivileges(nint pointer, Privileges privileges)
        {
            throw new System.NotImplementedException();
        }
    }

    /// <summary>
    /// Represents an implementation of the <see cref="IStorageAllocator"/> interface.
    /// </summary>
    public abstract class MemoryAllocator : IStorageAllocator
    {
        Privileges DefaultPrivileges;

        /// <summary>
        /// The maxmium size of bytes the MemoryAllocator will allocate before throwing a <see cref="System.OutOfMemoryException."/>
        /// </summary>
        long MaximumSize;

        /// <summary>
        /// 
        /// </summary>
        byte Alignment, Displacement;

        /// <summary>
        /// 
        /// </summary>
        public long AllocatedBytes
        {
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            get
            {
                long result = 0;

                foreach (nint pointer in Allocations.Keys)
                {

                    if (Allocations.TryGetValue(pointer, out System.Collections.Generic.IEnumerable<long> sizes))
                    {
                        foreach (long size in sizes)
                        {
                            result += size;
                        }
                    }
                }

                return result;
            }
        }

        Media.Common.Collections.Generic.ConcurrentThesaurus<nint, long> Allocations = [];

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        nint IStorageAllocator.Allocate(long size)
        {
            throw new System.NotImplementedException();

            //Allocations.Add(pointer, size);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        void IStorageAllocator.Release(nint pointer, long size)
        {
            throw new System.NotImplementedException();

            //System.Collections.Generic.IList<long> sizes;

            //if (Allocations.TryGetValueList(ref pointer, out sizes))
            //{
            //    foreach (long size in sizes)
            //    {
            //        Release(pointer, size);
            //    }
            //}
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        Privileges IStorageAllocator.SetPrivileges(nint pointer, Privileges privileges)
        {
            throw new System.NotImplementedException();
        }
    }

    /// <summary>
    /// Provides a platform aware memory allocator.
    /// </summary>
    abstract public class PlatformMemoryAllocator : MemoryAllocator
    {

    }

    //StackAllocator

    #endregion

    //https://github.com/IllidanS4/SharpUtils/blob/a3b4da490537e361e6a5debc873c303023d83bf1/Unsafe/UnmanagedInstance.cs

    //https://github.com/IllidanS4/SharpUtils/blob/a3b4da490537e361e6a5debc873c303023d83bf1/Unsafe/Pointer.cs

    //https://github.com/IllidanS4/SharpUtils/blob/a3b4da490537e361e6a5debc873c303023d83bf1/Unsafe/ObjectHandle.cs
}
