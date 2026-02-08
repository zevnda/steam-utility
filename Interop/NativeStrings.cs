using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace API
{
    internal class NativeStrings
    {
        public sealed class StringHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            internal StringHandle(IntPtr existingHandle, bool ownsHandle)
                : base(ownsHandle)
            {
                SetHandle(existingHandle);
            }

            public IntPtr Handle => handle;

            protected override bool ReleaseHandle()
            {
                if (handle == IntPtr.Zero)
                    return false;

                Marshal.FreeHGlobal(handle);
                handle = IntPtr.Zero;
                return true;
            }
        }

        public static unsafe StringHandle StringToStringHandle(string value)
        {
            if (value == null)
                return new StringHandle(IntPtr.Zero, true);

            byte[] utf8Bytes = Encoding.UTF8.GetBytes(value);
            int byteCount = utf8Bytes.Length;

            IntPtr nativePtr = Marshal.AllocHGlobal(byteCount + 1);
            Marshal.Copy(utf8Bytes, 0, nativePtr, utf8Bytes.Length);
            ((byte*)nativePtr)[byteCount] = 0; // Null terminator

            return new StringHandle(nativePtr, true);
        }

        public static unsafe string PointerToString(sbyte* ptr)
        {
            if (ptr == null)
                return null;

            if (*ptr == 0)
                return string.Empty;

            int charCount = 0;
            sbyte* current = ptr;
            while (*current != 0)
            {
                charCount++;
                current++;
            }

            return new string(ptr, 0, charCount, Encoding.UTF8);
        }

        public static unsafe string PointerToString(byte* ptr)
        {
            return PointerToString((sbyte*)ptr);
        }

        public static unsafe string PointerToString(IntPtr ptr)
        {
            return PointerToString((sbyte*)ptr.ToPointer());
        }

        public static unsafe string PointerToString(sbyte* ptr, int maxLength)
        {
            if (ptr == null)
                return null;

            if (maxLength == 0 || *ptr == 0)
                return string.Empty;

            int charCount = 0;
            sbyte* current = ptr;
            while (*current != 0 && charCount < maxLength)
            {
                charCount++;
                current++;
            }

            return new string(ptr, 0, charCount, Encoding.UTF8);
        }

        public static unsafe string PointerToString(byte* ptr, int maxLength)
        {
            return PointerToString((sbyte*)ptr, maxLength);
        }

        public static unsafe string PointerToString(IntPtr ptr, int maxLength)
        {
            return PointerToString((sbyte*)ptr.ToPointer(), maxLength);
        }
    }
}
