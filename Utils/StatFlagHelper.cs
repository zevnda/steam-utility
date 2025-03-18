using System;

namespace SteamUtility.Utils
{
    [Flags]
    public enum StatFlags
    {
        None = 0,
        IncrementOnly = 1 << 0,
        Protected = 1 << 1,
        UnknownPermission = 1 << 2,
    }

    public static class StatFlagHelper
    {
        public static StatFlags GetFlags(int permission, bool isIncrementOnly)
        {
            var flags = StatFlags.None;
            flags |= !isIncrementOnly ? 0 : StatFlags.IncrementOnly;
            flags |= (permission & 2) != 0 ? StatFlags.Protected : 0;
            flags |= (permission & ~2) != 0 ? StatFlags.UnknownPermission : 0;
            return flags;
        }
    }
}
