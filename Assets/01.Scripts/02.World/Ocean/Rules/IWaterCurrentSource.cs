using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public interface IWaterCurrentSource
    {
        Vector2 GetCurrent(Vector3 worldPos);
    }
}
