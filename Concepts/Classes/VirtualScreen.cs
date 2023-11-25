using System;

namespace Media.Concepts.Classes
{
    public class VirtualScreen
    {
        private readonly TimeSpan RefreshRate;
        private readonly bool VerticalSync;
        private readonly int Width, Height;
        private readonly Common.MemorySegment DisplayMemory, BackBuffer, DisplayBuffer;
    }
}
