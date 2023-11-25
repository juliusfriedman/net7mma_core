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

namespace Media.Concepts.Classes
{

    public sealed class CentralProcessingUnit //IProcessor
    {
        #region References

        //https://git.kernel.org/cgit/linux/kernel/git/stable/linux-stable.git/tree/arch/x86/include/asm/cpufeature.h?id=refs/tags/v4.1.3        

        //http://wiki.osdev.org/X86-64

        #endregion

        //Todo, provide a was to associate from VendorString to this enum with parse.

        //InstructionSet, EPIC, CISC, RISC, LEGACY, FPU, MMX, etc

        /// <summary>
        /// 
        /// </summary>
        public enum Vendor : byte
        {
            Unknown,
            Centaur,
            Advanced,
            Intel,
            MicroDevices,
            Motorola,
            VIA,
            Cyrix = VIA,
            Transmeta,
            NationalSemiConductor,
            NSC = NationalSemiConductor,
            KVM,
            MSVM,
            XenHVM,
            NexGen,
            Rise,
            SiS,
            UMC,
            Vortex,
            AMD = Advanced | MicroDevices,
            AdvancedMicroDevices = AMD,
            ReducedInstructionSet,
            ARM = Advanced | ReducedInstructionSet,
            AdvancedReducedInstructionSet = ARM,
            Microsoft,
            Parallels,
            VMware,
            Xen
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static Vendor GetVendor(string vendorString)
        {
            return Concepts.Hardware.Intrinsics.CpuId.GetVendorString() switch
            {
                // Actual Hardware
                Concepts.Hardware.Intrinsics.CpuId.VendorStrings.GenuineIntel => Vendor.Intel,
                //early engineering samples of AMD K5 processor
                Concepts.Hardware.Intrinsics.CpuId.VendorStrings.AMDisbetter_ or Concepts.Hardware.Intrinsics.CpuId.VendorStrings.AuthenticAMD => Vendor.AMD,
                Concepts.Hardware.Intrinsics.CpuId.VendorStrings.CentaurHauls => Vendor.Centaur,
                Concepts.Hardware.Intrinsics.CpuId.VendorStrings.CyrixInstead => Vendor.Cyrix,
                //Transmets
                Concepts.Hardware.Intrinsics.CpuId.VendorStrings.TransmetaCPU or Concepts.Hardware.Intrinsics.CpuId.VendorStrings.GenuineTMx86 => Vendor.Transmeta,
                Concepts.Hardware.Intrinsics.CpuId.VendorStrings.Geode_by_NSC => Vendor.NationalSemiConductor,
                Concepts.Hardware.Intrinsics.CpuId.VendorStrings.NexGen => Vendor.NexGen,
                Concepts.Hardware.Intrinsics.CpuId.VendorStrings.Vortext86_SoC => Vendor.Vortex,
                //Virtual Machines
                Concepts.Hardware.Intrinsics.CpuId.VendorStrings.KVMKVMKVM => Vendor.KVM,
                Concepts.Hardware.Intrinsics.CpuId.VendorStrings.Microsoft_Hv => Vendor.Microsoft,
                Concepts.Hardware.Intrinsics.CpuId.VendorStrings._lrpepyh_vr => Vendor.Parallels,
                Concepts.Hardware.Intrinsics.CpuId.VendorStrings.VMwareVMware => Vendor.VMware,
                Concepts.Hardware.Intrinsics.CpuId.VendorStrings.XenVMMXenVMM => Vendor.Xen,
                //case null:
                //case "":
                _ => Vendor.Unknown,
            };
        }


        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static long GetTimestampCounter(int processor, int core)
        {
            //Determine the best method and return the result
            throw new System.NotImplementedException();
        }

        //Mode

        //CurrentMode

        //SetMode(Mode)

        //EnterLongMode => SetMode(Long)

        //ChangeByteOrder

        //...

        //Enable, Disable

        //Intrinsic, Interrupt, Component, Bios, Coprocessor (FPU), Ram, etc

        //HasByteOrder, HasPeripherials, AmHardware
        ////bool Common.Interfaces.Has.Has
        ////{
        ////    get { return true; }
        ////}
    }
}
