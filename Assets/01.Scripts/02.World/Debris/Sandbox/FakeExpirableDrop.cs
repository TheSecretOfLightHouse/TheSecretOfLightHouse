using System;
using Lighthouse.World.Debris.Rules;

namespace Lighthouse.World.Debris.Sandbox
{
    public sealed class FakeExpirableDrop : IExpirableDrop
    {
        public FakeExpirableDrop(ulong generation)
        {
            Generation = generation;
        }

        public ulong Generation { get; set; }

        public int ExpireCount { get; private set; }

        public event Action<IExpirableDrop> Released;

        public bool TryExpire(ulong generation)
        {
            if (generation != Generation)
            {
                return false;
            }

            ExpireCount++;
            Release();
            return true;
        }

        public void Release()
        {
            Released?.Invoke(this);
        }
    }
}
