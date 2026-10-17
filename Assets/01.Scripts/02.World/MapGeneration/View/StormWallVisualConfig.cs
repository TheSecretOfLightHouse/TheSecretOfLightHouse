using UnityEngine;

namespace Lighthouse.World.MapGeneration.View
{
    [CreateAssetMenu(fileName = "StormWallVisualConfig",
        menuName = "TheLightHouse/World/StormWallVisualConfig")]
    public sealed class StormWallVisualConfig : ScriptableObject
    {
        [SerializeField, Min(0.1f)] private float _fogReferenceRadius = 32.50834f;
        [SerializeField, Min(0.1f)] private float _fogVerticalScale = 2f;
        [SerializeField] private float _fogHeight = 0.5f;
        [SerializeField, Min(0.1f)] private float _lightningInterval = 6f;
        [SerializeField, Min(0.01f)] private float _lightningDuration = 0.15f;
        [SerializeField, Min(1f)] private float _lightningHeight = 25f;
        [SerializeField, Min(0f)] private float _lightningJitter = 2f;
        [SerializeField, Range(3, 16)] private int _lightningPoints = 7;

        public float FogReferenceRadius => _fogReferenceRadius;
        public float FogVerticalScale => _fogVerticalScale;
        public float FogHeight => _fogHeight;
        public float LightningInterval => _lightningInterval;
        public float LightningDuration => _lightningDuration;
        public float LightningHeight => _lightningHeight;
        public float LightningJitter => _lightningJitter;
        public int LightningPoints => _lightningPoints;

        public bool IsValid =>
            IsFinite(_fogReferenceRadius) && _fogReferenceRadius > 0f
            && IsFinite(_fogVerticalScale) && _fogVerticalScale > 0f
            && IsFinite(_fogHeight)
            && IsFinite(_lightningInterval) && _lightningInterval > 0f
            && IsFinite(_lightningDuration) && _lightningDuration > 0f
            && _lightningDuration < _lightningInterval
            && IsFinite(_lightningHeight) && _lightningHeight > 0f
            && IsFinite(_lightningJitter) && _lightningJitter >= 0f
            && _lightningPoints >= 3 && _lightningPoints <= 16;

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
