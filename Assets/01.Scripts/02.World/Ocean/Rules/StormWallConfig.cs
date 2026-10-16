using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    [CreateAssetMenu(fileName = "StormWallConfig",
        menuName = "TheLightHouse/World/StormWallConfig")]
    public sealed class StormWallConfig : ScriptableObject
    {
        [SerializeField, Min(1f)] private float _width = 20f;
        [SerializeField, Min(0f)] private float _pushForce = 5000f;
        [SerializeField, Min(0f)] private float _outwardDamping = 1000f;

        public float Width => _width;
        public float PushForce => _pushForce;
        public float OutwardDamping => _outwardDamping;

        public bool IsValid =>
            IsFinite(_width) && _width >= 1f
            && IsFinite(_pushForce) && _pushForce >= 0f
            && IsFinite(_outwardDamping) && _outwardDamping >= 0f;

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
