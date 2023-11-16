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

namespace Media.Common.Extensions
{
    public static class OperatingSystemExtensions
    {
        public static readonly System.PlatformID EnvironmentOsPlatformId = System.Environment.OSVersion.Platform;

        //Should be taken into account when setting socket options...

        private static readonly bool isLinux =
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Linux);

        public static bool IsLinux => isLinux;


        private static readonly bool isMac =
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.OSX);

        public static bool IsMac => IsMac;

        public static bool IsXbox
        {
            get
            {
                return EnvironmentOsPlatformId == System.PlatformID.Xbox;
            }
        }

        private static readonly bool isWindows =
            System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows);

        public static bool IsWindows => isWindows;

        //Notes these values are compiled into the library.
        //To determine at RunTime use the RuntimeExtensions.
        #region Xaramin

        public static bool IsiOS
        {
            get
            {
#if __IOS__
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsWatchOS
        {
            get
            {
#if __WATCHOS__
                return true;
#else
                return false;
#endif
            }
        }


        public static bool IsTvOS
        {
            get
            {
#if __TVOS__
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsAndroid
        {
            get
            {
#if __ANDROID__ || __ANDROID_11__
                return true;
#else
                return false;
#endif
            }
        }

        #endregion
    }
}
