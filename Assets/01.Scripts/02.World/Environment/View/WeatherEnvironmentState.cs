using System;
using UnityEngine;

namespace Lighthouse.World.Environment.View
{
    [Serializable]
    public struct WeatherEnvironmentState
    {
        [SerializeField, ColorUsage(false, false)] private Color _tint;
        [SerializeField, Range(0f, 1f)] private float _cloudCoverage;
        [SerializeField, Range(0f, 1f)] private float _starVisibility;
        [SerializeField, Range(0f, 1f)] private float _nebulaVisibility;
        [SerializeField, Range(0f, 1f)] private float _lightScale;
        [SerializeField, Range(0f, 1f)] private float _abyssWeight;

        public Color Tint => _tint;
        public float CloudCoverage => Mathf.Clamp01(_cloudCoverage);
        public float StarVisibility => Mathf.Clamp01(_starVisibility);
        public float NebulaVisibility => Mathf.Clamp01(_nebulaVisibility);
        public float LightScale => Mathf.Clamp01(_lightScale);
        public float AbyssWeight => Mathf.Clamp01(_abyssWeight);

        public WeatherEnvironmentState(
            Color tint,
            float cloudCoverage,
            float starVisibility,
            float nebulaVisibility,
            float lightScale,
            float abyssWeight)
        {
            _tint = tint;
            _cloudCoverage = Mathf.Clamp01(cloudCoverage);
            _starVisibility = Mathf.Clamp01(starVisibility);
            _nebulaVisibility = Mathf.Clamp01(nebulaVisibility);
            _lightScale = Mathf.Clamp01(lightScale);
            _abyssWeight = Mathf.Clamp01(abyssWeight);
        }

        public static WeatherEnvironmentState Lerp(
            in WeatherEnvironmentState from,
            in WeatherEnvironmentState to,
            float t)
        {
            float blend = Mathf.Clamp01(t);

            return new WeatherEnvironmentState(
                Color.Lerp(from.Tint, to.Tint, blend),
                Mathf.Lerp(from.CloudCoverage, to.CloudCoverage, blend),
                Mathf.Lerp(from.StarVisibility, to.StarVisibility, blend),
                Mathf.Lerp(from.NebulaVisibility, to.NebulaVisibility, blend),
                Mathf.Lerp(from.LightScale, to.LightScale, blend),
                Mathf.Lerp(from.AbyssWeight, to.AbyssWeight, blend));
        }
    }
}
