using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace API
{
    public abstract class NativeWrapper<TNativeFunctions> : INativeWrapper
    {
        protected IntPtr InstanceAddress;
        protected TNativeFunctions NativeFunctions;

        private readonly Dictionary<IntPtr, Delegate> m_delegateCache = new();

        public override string ToString()
        {
            return $"Steam Interface<{typeof(TNativeFunctions).Name}> [0x{InstanceAddress.ToInt64():X}]";
        }

        public void Initialize(IntPtr instanceAddress)
        {
            InstanceAddress = instanceAddress;

            NativeClass nativeInstance = (NativeClass)
                Marshal.PtrToStructure(InstanceAddress, typeof(NativeClass));

            NativeFunctions = (TNativeFunctions)
                Marshal.PtrToStructure(nativeInstance.VTablePointer, typeof(TNativeFunctions));
        }

        protected Delegate GetDelegate<TDelegate>(IntPtr functionPointer)
        {
            if (!m_delegateCache.TryGetValue(functionPointer, out Delegate cachedDelegate))
            {
                cachedDelegate = Marshal.GetDelegateForFunctionPointer(
                    functionPointer,
                    typeof(TDelegate)
                );
                m_delegateCache[functionPointer] = cachedDelegate;
            }
            return cachedDelegate;
        }

        protected TDelegate GetFunction<TDelegate>(IntPtr functionPointer)
            where TDelegate : class
        {
            return GetDelegate<TDelegate>(functionPointer) as TDelegate;
        }

        protected void Call<TDelegate>(IntPtr functionPointer, params object[] arguments)
        {
            GetDelegate<TDelegate>(functionPointer).DynamicInvoke(arguments);
        }

        protected TReturn Call<TReturn, TDelegate>(
            IntPtr functionPointer,
            params object[] arguments
        )
        {
            return (TReturn)GetDelegate<TDelegate>(functionPointer).DynamicInvoke(arguments);
        }
    }
}
