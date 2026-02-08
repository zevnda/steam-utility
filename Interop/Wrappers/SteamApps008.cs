using System;
using System.Runtime.InteropServices;
using API.Interfaces;

namespace API.Wrappers
{
    public class SteamApps008 : NativeWrapper<ISteamApps008>
    {
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool IsSubscribedAppNative(IntPtr thisPtr, uint gameId);

        public bool IsSubscribedApp(uint gameId)
        {
            return Call<bool, IsSubscribedAppNative>(
                NativeFunctions.IsSubscribedApp,
                InstanceAddress,
                gameId
            );
        }
    }
}
