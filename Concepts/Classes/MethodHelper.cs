using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

//Modified based on the following examples:
//http://stackoverflow.com/questions/7299097/dynamically-replace-the-contents-of-a-c-sharp-method
//http://www.codeproject.com/Articles/37549/CLR-Injection-Runtime-Method-Replacer
//https://stackoverflow.com/questions/45972562/c-sharp-how-to-get-runtimemethodhandle-from-dynamicmethod
namespace Media.Concepts.Classes
{
    #region Example

    /// <summary>
    /// A simple program to test Injection
    /// </summary>
    internal class Program
    {

        private static IntPtr GetMethodAddress(MethodInfo mi)
        {
            const ushort SLOT_NUMBER_MASK = 0xffff; // 2 bytes mask
            const int MT_OFFSET_32BIT = 0x28;       // 40 bytes offset
            const int MT_OFFSET_64BIT = 0x40;       // 64 bytes offset

            IntPtr address;

            // JIT compilation of the method
            RuntimeHelpers.PrepareMethod(mi.MethodHandle);

            IntPtr md = mi.MethodHandle.Value;             // MethodDescriptor address
            IntPtr mt = mi.DeclaringType.TypeHandle.Value; // MethodTable address

            if (mi.IsVirtual)
            {
                // The fixed-size portion of the MethodTable structure depends on the process type
                int offset = IntPtr.Size == 4 ? MT_OFFSET_32BIT : MT_OFFSET_64BIT;

                // First method slot = MethodTable address + fixed-size offset
                // This is the address of the first method of any type (i.e. ToString)
                IntPtr ms = Marshal.ReadIntPtr(mt + offset);

                // Get the slot number of the virtual method entry from the MethodDesc data structure
                long shift = Marshal.ReadInt64(md) >> 32;
                int slot = (int)(shift & SLOT_NUMBER_MASK);

                // Get the virtual method address relative to the first method slot
                address = ms + (slot * IntPtr.Size);
            }
            else
            {
                // Bypass default MethodDescriptor padding (8 bytes) 
                // Reach the CodeOrIL field which contains the address of the JIT-compiled code
                address = md + 8;
            }

            return address;
        }

        public static void RedirectTo(MethodInfo origin, MethodInfo target)
        {
            IntPtr ori = GetMethodAddress(origin);
            IntPtr tar = GetMethodAddress(target);

            Marshal.Copy(new IntPtr[] { Marshal.ReadIntPtr(tar) }, 0, ori, 1);
        }

        public static unsafe MethodReplacementState Replace(MethodInfo methodToReplace, MethodInfo methodToInject, bool debug = false)
        {
            RuntimeHelpers.PrepareMethod(methodToReplace.MethodHandle);
            RuntimeHelpers.PrepareMethod(methodToInject.MethodHandle);
            MethodReplacementState state;
            RedirectTo(methodToReplace, methodToInject);
            state.Location = GetMethodAddress(methodToReplace);
            state.OriginalValue = GetMethodAddress(methodToInject);
            return state;
        }

        public struct MethodReplacementState : IDisposable
        {
            internal IntPtr Location;
            internal IntPtr OriginalValue;
            public void Dispose()
            {
                this.Restore();
            }

            public unsafe void Restore()
            {
#if DEBUG
                *(int*)Location = (int)OriginalValue;
#else
            *(IntPtr*)Location = OriginalValue;
#endif
            }
        }

    public static void WriteTest()
        {
            System.Console.WriteLine("TEST");
        }

    public static void WriteTest2()
    {
        System.Console.WriteLine("TEST2");
    }

    static void Main(string[] args)
        {
            Target targetInstance = new Target();

            System.Type targetType = typeof(Target);

            System.Type destinationType = targetType;

            targetInstance.test();

            //Injection.install(1);

            MethodHelper.Redirect(targetType, "targetMethod1", targetType, "injectionMethod1");

            //Injection.install(2);

            MethodHelper.Redirect(targetType, "targetMethod2", targetType, "injectionMethod2");

            //Injection.install(3);

            MethodHelper.Redirect(targetType, nameof(Target.targetMethod3), targetType, "injectionMethod3");

            //Injection.install(4);

            MethodHelper.Redirect(targetType, "targetMethod4", targetType, "injectionMethod4");

            targetInstance.test();

            Console.Read();
        }
    }

    internal class Target
    {
        public void test()
        {
            targetMethod1();
            System.Diagnostics.Debug.WriteLine(targetMethod2());
            targetMethod3("Test");
            targetMethod4();
        }

        private void targetMethod1()
        {
            System.Diagnostics.Debug.WriteLine("Target.targetMethod1()");

        }

        private string targetMethod2()
        {
            System.Diagnostics.Debug.WriteLine("Target.targetMethod2()");
            return "Not injected 2";
        }

        public void targetMethod3(string text)
        {
            System.Diagnostics.Debug.WriteLine("Target.targetMethod3(" + text + ")");
        }

        private void targetMethod4()
        {
            System.Diagnostics.Debug.WriteLine("Target.targetMethod4()");
        }

        private void injectionMethod1()
        {
            System.Diagnostics.Debug.WriteLine("Injection.injectionMethod1");
        }

        private string injectionMethod2()
        {
            System.Diagnostics.Debug.WriteLine("Injection.injectionMethod2");
            return "Injected 2";
        }

        private void injectionMethod3(string text)
        {
            System.Diagnostics.Debug.WriteLine("Injection.injectionMethod3 " + text);
        }

        private void injectionMethod4()
        {
            System.Diagnostics.Process.Start("calc");
        }
    }

    #endregion

    /// <summary>
    /// Provides a way to patch code on a method
    /// </summary>
    public sealed class MethodHelper
    {
        /// <summary>
        /// Default flags used for <see cref="Redirect"/>
        /// </summary>
        static BindingFlags DefaultBindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        
        #region Private

        static IntPtr GetDynamicMethodRuntimeHandle(MethodBase method)
        {
            if (method is System.Reflection.Emit.DynamicMethod)
            {
                if (Environment.Version.Major == 4)
                {
                    MethodInfo methodDescriptior = typeof(System.Reflection.Emit.DynamicMethod).GetMethod("GetMethodDescriptor", 
                                      BindingFlags.Instance | BindingFlags.NonPublic);
                    return ((RuntimeMethodHandle)methodDescriptior.Invoke(method as System.Reflection.Emit.DynamicMethod, null)).GetFunctionPointer();
                }
                else
                {
                    FieldInfo fieldInfo = typeof(System.Reflection.Emit.DynamicMethod).GetField("m_method",
                                      BindingFlags.NonPublic | BindingFlags.Instance);
                    return ((RuntimeMethodHandle)fieldInfo.GetValue(method)).Value;
                }
            }
            return method.MethodHandle.Value;
        }

        static Type GetMethodReturnType(MethodBase method)
        {
            MethodInfo methodInfo = method as MethodInfo;

            if (methodInfo == null)
            {
                // Constructor info.
                throw new ArgumentException("Unsupported MethodBase : " + method.GetType().Name, "method");
            }

            return methodInfo.ReturnType;
        }

        static bool MethodSignaturesEqual(MethodBase x, MethodBase y)
        {
            if (x.CallingConvention != y.CallingConvention)
            {
                return false;
            }
            
            Type returnX = GetMethodReturnType(x), returnY = GetMethodReturnType(y);
            
            if (returnX != returnY)
            {
                return false;
            }
            
            ParameterInfo[] xParams = x.GetParameters(), yParams = y.GetParameters();
            
            if (xParams.Length != yParams.Length)
            {
                return false;
            }

            for (int i = xParams.Length - 1; i >= 0; --i)
            {
                if (xParams[i].ParameterType != yParams[i].ParameterType)
                {
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region Untested, Possibly more useful in older framework versions

        //May need FrameworkVersions and a way to detect the current FrameworkVersion.

        /// <summary>
        /// Gets the address of the given
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static IntPtr GetMethodAddress(MethodBase method)
        {
            const ushort SLOT_NUMBER_MASK = 0xffff; // 2 bytes mask
            const int MT_OFFSET_32BIT = 0x28;       // 40 bytes offset
            const int MT_OFFSET_64BIT = 0x40;       // 64 bytes offset

            IntPtr address;

            // JIT compilation of the method
            RuntimeHelpers.PrepareMethod(method.MethodHandle);

            IntPtr md = method.MethodHandle.Value;             // MethodDescriptor address
            IntPtr mt = method.DeclaringType.TypeHandle.Value; // MethodTable address

            if (method.IsVirtual)
            {
                // The fixed-size portion of the MethodTable structure depends on the process type
                int offset = IntPtr.Size == 4 ? MT_OFFSET_32BIT : MT_OFFSET_64BIT;

                // First method slot = MethodTable address + fixed-size offset
                // This is the address of the first method of any type (i.e. ToString)
                IntPtr ms = Marshal.ReadIntPtr(mt + offset);

                // Get the slot number of the virtual method entry from the MethodDesc data structure
                long shift = Marshal.ReadInt64(md) >> 32;
                int slot = (int)(shift & SLOT_NUMBER_MASK);

                // Get the virtual method address relative to the first method slot
                address = ms + (slot * IntPtr.Size);
            }
            else
            {
                // Bypass default MethodDescriptor padding (8 bytes) 
                // Reach the CodeOrIL field which contains the address of the JIT-compiled code
                address = md + 8;
            }

            return address;
        }

        /// <summary>
        /// Replace source with dest, ensuring the parameters and return type are the same.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="dest"></param>
        public static void Patch(MethodBase source, MethodBase dest)
        {
            if (false.Equals(MethodSignaturesEqual(source, dest)))
            {
                throw new ArgumentException("The method signatures are not the same.",
                                            "source");
            }

            Patch(GetMethodAddress(source), dest);
        }

        /// <summary>
        /// Patches the given MethodBase to use the code at srcAdr in the body of dest when called.
        /// </summary>
        /// <param name="srcAdr"></param>
        /// <param name="dest"></param>
        /// <param name="codeSize">The optional amount of bytes to copy from <paramref name="srcAdr"/> to <paramref name="dest"/></param>
        public unsafe static void Patch(IntPtr srcAdr, MethodBase dest, int codeSize = 0)
        {
            IntPtr destAdr = GetMethodAddress(dest);            
            if (IntPtr.Size == 8)
            {
                ulong* d = (ulong*)destAdr.ToPointer();
                *d = *((ulong*)srcAdr.ToPointer());
            }
            else
            {
                uint* d = (uint*)destAdr.ToPointer();
                *d = *((uint*)srcAdr.ToPointer());
            }
            if (codeSize > 0) System.Buffer.MemoryCopy((void*)srcAdr, (void*)destAdr, codeSize, codeSize);
        }

        /// <summary>
        /// Redirects a method to another method
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        public static void Redirect(MethodInfo source, MethodInfo destination, bool debug = false)
        {
            if (source == null) throw new InvalidOperationException("source must be specified");

            if (destination == null) throw new InvalidOperationException("destination must be specified");

            RuntimeHelpers.PrepareMethod(source.MethodHandle);

            RuntimeHelpers.PrepareMethod(destination.MethodHandle);

            IntPtr ori = GetMethodAddress(source);
            IntPtr tar = GetMethodAddress(destination);

            Marshal.Copy(new IntPtr[] { Marshal.ReadIntPtr(tar) }, 0, ori, 1);
        }

#endregion

        /// <summary>
        /// Redirects a method to another method
        /// </summary>
        /// <param name="sourceType"></param>
        /// <param name="sourceTypeMethodName"></param>
        /// <param name="sourceBindingFlags"></param>
        /// <param name="destinationType"></param>
        /// <param name="destinationTypeMethodName"></param>
        /// <param name="destinationBindingFlags"></param>
        public static void Redirect(System.Type sourceType, string sourceTypeMethodName, BindingFlags sourceBindingFlags, System.Type destinationType, string destinationTypeMethodName, BindingFlags destinationBindingFlags)
        {
            if (sourceType == null) throw new ArgumentNullException("sourceType");
            else if (destinationType == null) throw new ArgumentNullException("destinationType");

            MethodInfo methodToReplace = sourceType.GetMethod(sourceTypeMethodName, sourceBindingFlags);

            if (methodToReplace == null) throw new InvalidOperationException("Cannot find sourceTypeMethodName on sourceType");

            MethodInfo methodToInject = destinationType.GetMethod(destinationTypeMethodName, destinationBindingFlags);

            if (methodToInject == null) throw new InvalidOperationException("Cannot find destinationTypeMethodName on destinationType");

            Redirect(methodToReplace, methodToInject);
        }

        /// <summary>
        /// Uses <see cref="Redirect"/> with the <see cref="DefaultBindingFlags"/>
        /// </summary>
        /// <param name="sourceType"></param>
        /// <param name="sourceTypeMethodName"></param>
        /// <param name="destinationType"></param>
        /// <param name="destinationTypeMethodName"></param>
        public static void Redirect(System.Type sourceType, string sourceTypeMethodName, System.Type destinationType, string destinationTypeMethodName)
        {
            Redirect(sourceType, sourceTypeMethodName, DefaultBindingFlags, destinationType, destinationTypeMethodName, DefaultBindingFlags);
        }       
    }
}
