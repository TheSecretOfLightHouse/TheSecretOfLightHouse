using UnityEngine;

namespace Lighthouse.World.MapGeneration.Rules
{
    public interface ISampleDomain
    {
        Rect Bounds { get; }

        bool Contains(Vector2 point);
    }
}
