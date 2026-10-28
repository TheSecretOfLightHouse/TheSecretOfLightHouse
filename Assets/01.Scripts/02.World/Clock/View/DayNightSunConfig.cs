using UnityEngine;

namespace Lighthouse.World.Clock.View
{
    [CreateAssetMenu(
        fileName = "DayNightSunConfig",
        menuName = "TheLightHouse/World/DayNightSunConfig")]
    public sealed class DayNightSunConfig : ScriptableObject
    {
        [SerializeField, Min(0f)] private float _dayIntensity = 1f;
        [SerializeField, Min(0f)] private float _nightIntensity = 0f;
        [SerializeField, Range(0.01f, 1f)] private float _horizonBlend = 0.15f;
        [SerializeField, Range(0f, 360f)] private float _azimuth = 0f;
        [SerializeField, Range(1f, 45f)] private float _skyTwilightElevation = 22f;

        public float DayIntensity => _dayIntensity;
        public float NightIntensity => _nightIntensity;
        public float HorizonBlend => _horizonBlend;
        public float Azimuth => _azimuth;
        public float SkyTwilightElevation => _skyTwilightElevation;

        private void OnValidate()
        {
            _dayIntensity = Mathf.Max(0f, _dayIntensity);
            _nightIntensity = Mathf.Max(0f, _nightIntensity);
            _horizonBlend = Mathf.Clamp(_horizonBlend, 0.01f, 1f);
            _skyTwilightElevation = Mathf.Clamp(_skyTwilightElevation, 1f, 45f);
        }
    }
}
