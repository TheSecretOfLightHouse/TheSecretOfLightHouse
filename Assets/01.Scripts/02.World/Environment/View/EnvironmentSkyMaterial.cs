using System;
using UnityEngine;

namespace Lighthouse.World.Environment.View
{
    public sealed class EnvironmentSkyMaterial : IDisposable
    {
        private static readonly int ZenithId = Shader.PropertyToID("_Zenith");
        private static readonly int HorizonId = Shader.PropertyToID("_Horizon");
        private static readonly int NebulaColorId = Shader.PropertyToID("_NebulaColor");
        private static readonly int ValuesId = Shader.PropertyToID("_EnvironmentValues");
        private static readonly int DaylightId = Shader.PropertyToID("_Daylight");
        private static readonly int TimeId = Shader.PropertyToID("_EnvironmentTime");

        private static EnvironmentSkyMaterial _active;

        private readonly Material _previous;
        private Material _runtime;

        private EnvironmentSkyMaterial(Material source)
        {
            _previous = RenderSettings.skybox;
            _runtime = new Material(source);
            _runtime.hideFlags = HideFlags.HideAndDontSave;
            RenderSettings.skybox = _runtime;
        }

        public static bool TryCreate(
            Material source,
            out EnvironmentSkyMaterial material)
        {
            material = null;

            if (_active != null || source == null || source.shader == null ||
                !source.shader.isSupported ||
                source.shader.name != "Lighthouse/World/EnvironmentSky")
            {
                return false;
            }

            material = new EnvironmentSkyMaterial(source);
            _active = material;
            return true;
        }

        public void Apply(in SkyEnvironmentState state, float daylight)
        {
            Apply(in state, daylight, Time.timeSinceLevelLoad);
        }

        public void Apply(
            in SkyEnvironmentState state,
            float daylight,
            float skyTime)
        {
            if (_runtime == null)
            {
                return;
            }

            _runtime.SetColor(ZenithId, state.Zenith);
            _runtime.SetColor(HorizonId, state.Horizon);
            _runtime.SetColor(NebulaColorId, state.NebulaColor);
            _runtime.SetVector(ValuesId, new Vector4(
                state.StarStrength,
                state.NebulaStrength,
                state.CloudCoverage,
                state.LightScale));
            _runtime.SetFloat(DaylightId, Mathf.Clamp01(daylight));
            _runtime.SetFloat(TimeId, skyTime);
        }

        public void Dispose()
        {
            if (_runtime != null)
            {
                if (RenderSettings.skybox == _runtime)
                {
                    RenderSettings.skybox = _previous;
                }

                UnityEngine.Object.Destroy(_runtime);
                _runtime = null;
            }

            if (ReferenceEquals(_active, this))
            {
                _active = null;
            }
        }
    }
}
