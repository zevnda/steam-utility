using System;
using System.Runtime.InteropServices;

namespace API
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    internal struct NativeClass
    {
        public IntPtr VTablePointer;
    }
}
