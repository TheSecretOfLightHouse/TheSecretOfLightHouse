using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public class ManualWindSource : MonoBehaviour, IWindSource
    {
        [SerializeField] private Vector2 _wind = new Vector2(8f, 0f);

        public Vector2 GetWind(double time)
        {
            return _wind;
        }
    }
}
