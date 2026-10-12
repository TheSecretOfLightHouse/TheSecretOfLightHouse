using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public interface IWindSource
    {
        Vector2 GetWind(double time);
    }
}
