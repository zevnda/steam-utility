using System;
using System.Runtime.InteropServices;
using API.Interfaces;

namespace API.Wrappers
{
    public class SteamApps001 : NativeWrapper<ISteamApps001>
    {
        #region GetAppData
        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        private delegate int NativeGetAppData(
            IntPtr self,
            uint appId,
            IntPtr key,
            IntPtr value,
            int valueLength
        );

        public string GetAppData(uint appId, string key)
        {
            using (var nativeHandle = NativeStrings.StringToStringHandle(key))
            {
                const int valueLength = 1024;
                var valuePointer = Marshal.AllocHGlobal(valueLength);
                int result = this.Call<int, NativeGetAppData>(
                    this.NativeFunctions.GetAppData,
                    this.InstanceAddress,
                    appId,
                    nativeHandle.Handle,
                    valuePointer,
                    valueLength
                );
                var value =
                    result == 0 ? null : NativeStrings.PointerToString(valuePointer, valueLength);
                Marshal.FreeHGlobal(valuePointer);
                return value;
            }
        }
        #endregion
    }
}
