using System;
using Lighthouse.World.Environment.View;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.View
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1101)]
    public sealed class OceanAppearanceView : MonoBehaviour
    {
        private const int ZoneCount = 3;
        private const float MoonEpsilon = 0.002f;

        private const float StormFloorLoss = 0.45f;
        private const float AbyssFloorLoss = 0.5f;

        private static readonly int OceanMoonId = Shader.PropertyToID("_OceanMoon");
        private static readonly int OceanNightFloorId = Shader.PropertyToID("_OceanNightFloor");
        private static readonly Vector3 MoonDirection = new Vector3(0.4f, 0.55f, 0.7f).normalized;

        [SerializeField] private EnvironmentSkyView _environment;
        [SerializeField] private OceanOpticsProfile _profile;
        [SerializeField] private MapGenerationConfig _mapConfig;
        [SerializeField] private Material _sourceMaterial;
        [SerializeField] private Renderer[] _renderers;

        private OceanOpticsMaterial _material;
        private float _moonWeight = -1f;
        private float _floorWeight = -1f;

        private void OnEnable()
        {
            if (!TryPrepare())
            {
                Debug.LogError(
                    $"[{nameof(OceanAppearanceView)}] Check optics profile and single-material ocean renderers.");
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            EnvironmentSkyView environment = _environment != null
                ? _environment : EnvironmentSkyView.Active;

            if (_material == null || environment == null ||
                !environment.HasState)
            {
                return;
            }

            EnvironmentWeights weights = environment.Weights;

            OceanOpticsState state = OceanOpticsModel.Evaluate(
                _profile, weights.Daylight, weights.Storm, weights.Abyss);

            _material.Apply(in state);
            ApplyMoon(weights.Night * (1f - weights.Storm) * (1f - weights.Abyss));
            ApplyNightFloor(weights.Night * (1f - StormFloorLoss * weights.Storm) * (1f - AbyssFloorLoss * weights.Abyss));
        }

        // The silhouette floor outlives the moon: storms and the abyss at night must still separate hulls and shores from the water.
        private void ApplyNightFloor(float weight)
        {
            if (Mathf.Abs(weight - _floorWeight) < MoonEpsilon &&
                (weight > 0f) == (_floorWeight > 0f))
            {
                return;
            }

            _floorWeight = weight;
            Shader.SetGlobalVector(OceanNightFloorId, new Vector4(weight, 0f, 0f, 0f));
        }

        private void ApplyMoon(float weight)
        {
            if (Mathf.Abs(weight - _moonWeight) < MoonEpsilon &&
                (weight > 0f) == (_moonWeight > 0f))
            {
                return;
            }

            _moonWeight = weight;
            Shader.SetGlobalVector(OceanMoonId, new Vector4(
                MoonDirection.x, MoonDirection.y, MoonDirection.z, weight));
        }

        private void OnDisable()
        {
            _moonWeight = -1f;
            _floorWeight = -1f;
            Shader.SetGlobalVector(OceanMoonId, Vector4.zero);
            Shader.SetGlobalVector(OceanNightFloorId, Vector4.zero);

            if (_material != null)
            {
                _material.Dispose();
                _material = null;
            }
        }

        private bool TryPrepare()
        {
            if (_profile == null)
            {
                return false;
            }

            if (!OceanOpticsMaterial.TryCreate(
                    _renderers, _sourceMaterial, out _material))
            {
                return false;
            }

            ApplyZones();
            return true;
        }

        private void ApplyZones()
        {
            if (_mapConfig == null || _mapConfig.Zones.Count != ZoneCount ||
                !(_mapConfig.RadiusScale > 0f))
            {
                return;
            }

            Span<float> radii = stackalloc float[ZoneCount];
            float previous = 0f;

            for (int i = 0; i < ZoneCount; i++)
            {
                ZoneConfig zone = _mapConfig.Zones[i];

                if (zone == null)
                {
                    return;
                }

                radii[i] = zone.OuterRadius * _mapConfig.RadiusScale;

                if (!(radii[i] > previous) || float.IsInfinity(radii[i]))
                {
                    return;
                }

                previous = radii[i];
            }

            _material.ApplyZones(_profile, radii[0], radii[1], radii[2]);
        }
    }
}
