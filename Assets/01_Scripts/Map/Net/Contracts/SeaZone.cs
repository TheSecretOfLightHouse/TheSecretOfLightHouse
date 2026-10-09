using System;

namespace Lighthouse.Map.Net.Contracts
{
    [Flags]
    public enum SeaZone
    {
        None = 0,
        Inner = 1 << 0,
        Middle = 1 << 1,
        Outer = 1 << 2,
        All = Inner | Middle | Outer
    }
}
