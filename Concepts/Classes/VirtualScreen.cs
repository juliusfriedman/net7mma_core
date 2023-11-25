using System;

namespace Media.Concepts.Classes
{
    public class VirtualScreen
    {
        TimeSpan RefreshRate;

        bool VerticalSync;

        int Width, Height;

        Common.MemorySegment DisplayMemory, BackBuffer, DisplayBuffer;
    }
}
