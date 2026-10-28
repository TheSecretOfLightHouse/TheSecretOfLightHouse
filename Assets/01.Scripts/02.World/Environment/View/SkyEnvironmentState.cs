using System;
using UnityEngine;

namespace Lighthouse.World.Environment.View
{
    [Serializable]
    public struct SkyEnvironmentState
    {
        [SerializeField, ColorUsage(false, true)] private Color _zenith;
        [SerializeField, ColorUsage(false, true)] private Color _horizon;
        [SerializeField, ColorUsage(false, true)] private Color _nebulaColor;
        [SerializeField, Min(0f)] private float _starStrength;
        [SerializeField, Min(0f)] private float _nebulaStrength;
        [SerializeField, Range(0f, 1f)] private float _lightScale;
        [SerializeField, Range(0f, 1f)] private float _cloudCoverage;

        public Color Zenith => _zenith;
        public Color Horizon => _horizon;
        public Color NebulaColor => _nebulaColor;
        public float StarStrength => Mathf.Max(0f, _starStrength);
        public float NebulaStrength => Mathf.Max(0f, _nebulaStrength);
        public float LightScale => Mathf.Clamp01(_lightScale);
        public float CloudCoverage => Mathf.Clamp01(_cloudCoverage);

        public SkyEnvironmentState(
            Color zenith,
            Color horizon,
            Color nebulaColor,
            float starStrength,
            float nebulaStrength,
            float lightScale,
            float cloudCoverage)
        {
            _zenith = zenith;
            _horizon = horizon;
            _nebulaColor = nebulaColor;
            _starStrength = Mathf.Max(0f, starStrength);
            _nebulaStrength = Mathf.Max(0f, nebulaStrength);
            _lightScale = Mathf.Clamp01(lightScale);
            _cloudCoverage = Mathf.Clamp01(cloudCoverage);
        }

        public static SkyEnvironmentState Lerp(
            in SkyEnvironmentState from,
            in SkyEnvironmentState to,
            float t)
        {
            float blend = Mathf.Clamp01(t);

            return new SkyEnvironmentState(
                Color.Lerp(from.Zenith, to.Zenith, blend),
                Color.Lerp(from.Horizon, to.Horizon, blend),
                Color.Lerp(from.NebulaColor, to.NebulaColor, blend),
                Mathf.Lerp(from.StarStrength, to.StarStrength, blend),
                Mathf.Lerp(from.NebulaStrength, to.NebulaStrength, blend),
                Mathf.Lerp(from.LightScale, to.LightScale, blend),
                Mathf.Lerp(from.CloudCoverage, to.CloudCoverage, blend));
        }
    }
}
