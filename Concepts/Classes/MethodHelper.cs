using System;
using System.Reflection;
using System.Runtime.CompilerServices;

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

    public static unsafe MethodReplacementState Replace(MethodInfo methodToReplace, MethodInfo methodToInject)
        {
            RuntimeHelpers.PrepareMethod(methodToReplace.MethodHandle);
            RuntimeHelpers.PrepareMethod(methodToInject.MethodHandle);
            MethodReplacementState state;

            IntPtr tar = methodToReplace.MethodHandle.Value;
            if (!methodToReplace.IsVirtual)
                tar += 8;
            else
            {
                var index = (int)(((*(long*)tar) >> 32) & 0xFF);
                var classStart = *(IntPtr*)(methodToReplace.DeclaringType.TypeHandle.Value + (IntPtr.Size == 4 ? 40 : 64));
                tar = classStart + IntPtr.Size * index;
            }
            var inj = methodToInject.MethodHandle.Value + 8;
            if (System.Diagnostics.Debugger.IsAttached)
            {
                tar = *(IntPtr*)tar + 1;
                inj = *(IntPtr*)inj + 1;
                state.Location = tar;
                state.OriginalValue = new IntPtr(*(int*)tar);

                *(int*)tar = *(int*)inj + (int)(long)inj - (int)(long)tar;
                return state;

            }
            state.Location = tar;
            state.OriginalValue = *(IntPtr*)tar;
            *(IntPtr*)tar = *(IntPtr*)inj;
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

            MethodHelper.Redirect(targetType, "targetMethod3", targetType, "injectionMethod3");

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
            if ((method is System.Reflection.Emit.DynamicMethod))
            {             
                unsafe
                {
                    byte* ptr = (byte*)GetDynamicMethodRuntimeHandle(method).ToPointer();
                    if (IntPtr.Size == 8)
                    {
                        ulong* address = (ulong*)ptr;
                        address += 6;
                        return new IntPtr(address);
                    }
                    else
                    {
                        uint* address = (uint*)ptr;
                        address += 6;
                        return new IntPtr(address);
                    }
                }
            }

            RuntimeHelpers.PrepareMethod(method.MethodHandle);

            unsafe
            {
                IntPtr tar = method.MethodHandle.Value;
                if (!method.IsVirtual)
                    tar += 8;
                else
                {
                    var index = (int)(((*(long*)tar) >> 32) & 0xFF);
                    var classStart = *(IntPtr*)(method.DeclaringType.TypeHandle.Value + (IntPtr.Size == 4 ? 40 : 64));
                    tar = classStart + IntPtr.Size * index;
                }
                return tar;
            }
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
            if (codeSize > 0) System.Buffer.MemoryCopy((void*)srcAdr, (void*)GetMethodAddress(dest), codeSize, codeSize);
        }
        
        /// <summary>
        /// Redirects a method to another method
        /// </summary>
        /// <param name="source"></param>
        /// <param name="destination"></param>
        public static void Redirect(MethodInfo source, MethodInfo destination)
        {
            if (source == null) throw new InvalidOperationException("source must be specified");

            if (destination == null) throw new InvalidOperationException("destination must be specified");

            RuntimeHelpers.PrepareMethod(source.MethodHandle);

            RuntimeHelpers.PrepareMethod(destination.MethodHandle);

            unsafe
            {
                IntPtr tar = source.MethodHandle.Value;
                if (!source.IsVirtual)
                    tar += 8;
                else
                {
                    var index = (int)(((*(long*)tar) >> 32) & 0xFF);
                    var classStart = *(IntPtr*)(source.DeclaringType.TypeHandle.Value + (IntPtr.Size == 4 ? 40 : 64));
                    tar = classStart + IntPtr.Size * index;
                }
                var inj = destination.MethodHandle.Value + 8;
#if DEBUG
                    tar = *(IntPtr*)tar + 1;
                    inj = *(IntPtr*)inj + 1;
                    *(int*)tar = *(int*)inj + (int)(long)inj - (int)(long)tar;
                    return;

#else
                *(IntPtr*)tar = *(IntPtr*)inj;
                return;
#endif
            }
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
