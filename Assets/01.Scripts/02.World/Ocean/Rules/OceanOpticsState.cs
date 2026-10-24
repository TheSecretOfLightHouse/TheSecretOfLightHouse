using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    [Serializable]
    public struct OceanOpticsState
    {
        [SerializeField] private Color _shallowColor;
        [SerializeField] private Color _deepColor;
        [SerializeField] private Color _veryDeepColor;
        [SerializeField, Range(0f, 1f)] private float _baseOpacity;
        [SerializeField, Range(0f, 1f)] private float _shallowsOpacity;
        [SerializeField, Range(0f, 1f)] private float _smoothness;

        public Color ShallowColor => _shallowColor;
        public Color DeepColor => _deepColor;
        public Color VeryDeepColor => _veryDeepColor;
        public float BaseOpacity => Mathf.Clamp01(_baseOpacity);
        public float ShallowsOpacity => Mathf.Clamp01(_shallowsOpacity);
        public float Smoothness => Mathf.Clamp01(_smoothness);

        public OceanOpticsState(
            Color shallowColor,
            Color deepColor,
            Color veryDeepColor,
            float baseOpacity,
            float shallowsOpacity,
            float smoothness)
        {
            _shallowColor = shallowColor;
            _deepColor = deepColor;
            _veryDeepColor = veryDeepColor;
            _baseOpacity = Mathf.Clamp01(baseOpacity);
            _shallowsOpacity = Mathf.Clamp01(shallowsOpacity);
            _smoothness = Mathf.Clamp01(smoothness);
        }

        public static OceanOpticsState Lerp(
            in OceanOpticsState from,
            in OceanOpticsState to,
            float t)
        {
            float blend = Mathf.Clamp01(t);

            return new OceanOpticsState(
                Color.Lerp(from.ShallowColor, to.ShallowColor, blend),
                Color.Lerp(from.DeepColor, to.DeepColor, blend),
                Color.Lerp(from.VeryDeepColor, to.VeryDeepColor, blend),
                Mathf.Lerp(from.BaseOpacity, to.BaseOpacity, blend),
                Mathf.Lerp(from.ShallowsOpacity, to.ShallowsOpacity, blend),
                Mathf.Lerp(from.Smoothness, to.Smoothness, blend));
        }
    }
}
