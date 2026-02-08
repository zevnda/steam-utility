using System;
using System.Runtime.InteropServices;

namespace API.Interfaces
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ISteamApps001
    {
        public IntPtr GetAppData;
    }
}
