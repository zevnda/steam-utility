using System;

namespace API
{
    public interface INativeWrapper
    {
        void Initialize(IntPtr instanceAddress);
    }
}
