using System;

namespace Media.Codecs.Audio.Ac3
{
    public class BitStreamInformation
    {
        public static int GetNumberOfChannels(byte acMod, bool lfeOn)
        {
            int result = GetNumberOfFullRangeChannels(acMod);
            return lfeOn ? ++result : result;
        }

        /// <summary>
        /// Return number of channels excluding LFE
        /// </summary>
        public static int GetNumberOfFullRangeChannels(byte acMod)
        {
            return acMod switch
            {
                0 => 2,
                1 => 1,
                2 => 2,
                3 => 3,
                4 => 3,
                5 => 4,
                6 => 4,
                7 => 5,
                _ => throw new ArgumentException("Invalid audio coding mode"),
            };
        }

    }
}
