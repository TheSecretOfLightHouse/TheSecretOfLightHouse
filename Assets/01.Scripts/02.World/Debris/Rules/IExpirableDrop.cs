using System;

namespace Lighthouse.World.Debris.Rules
{
    public interface IExpirableDrop
    {
        ulong Generation { get; }

        event Action<IExpirableDrop> Released;

        bool TryExpire(ulong generation);
    }
}
