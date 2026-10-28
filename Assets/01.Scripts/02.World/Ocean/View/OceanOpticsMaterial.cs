using System;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.View
{
    public sealed class OceanOpticsMaterial : IDisposable
    {
        private static readonly int ShallowColorId =
            Shader.PropertyToID("_Shallow_Color");
        private static readonly int DeepColorId =
            Shader.PropertyToID("_Deep_Color");
        private static readonly int VeryDeepColorId =
            Shader.PropertyToID("_Very_Deep_Color");
        private static readonly int BaseOpacityId =
            Shader.PropertyToID("_Base_Opacity");
        private static readonly int ShallowsOpacityId =
            Shader.PropertyToID("_Shallows_Opacity");
        private static readonly int SmoothnessId =
            Shader.PropertyToID("_Smoothness");
        private static readonly int ClarityId =
            Shader.PropertyToID("_Clarity");
        private static readonly int SkyLightId =
            Shader.PropertyToID("_Sky_Light");
        private static readonly int ZoneRadiiId =
            Shader.PropertyToID("_Zone_Radii");
        private static readonly int ZoneTint0Id =
            Shader.PropertyToID("_Zone_Tint0");
        private static readonly int ZoneTint1Id =
            Shader.PropertyToID("_Zone_Tint1");
        private static readonly int ZoneTint2Id =
            Shader.PropertyToID("_Zone_Tint2");
        private static readonly int ZoneSkyId =
            Shader.PropertyToID("_Zone_Sky");

        private static readonly int[] RequiredProperties =
        {
            ShallowColorId,
            DeepColorId,
            VeryDeepColorId,
            BaseOpacityId,
            ShallowsOpacityId,
            SmoothnessId
        };

        private readonly Renderer[] _targets;
        private readonly Material _source;
        private Material _runtime;
        private bool _hasClarity;
        private bool _hasSkyLight;
        private bool _hasZones;

        private OceanOpticsMaterial(Renderer[] targets, Material source)
        {
            _targets = (Renderer[])targets.Clone();
            _source = source;
            _hasClarity = source.HasProperty(ClarityId);
            _hasSkyLight = source.HasProperty(SkyLightId);
            _hasZones = source.HasProperty(ZoneRadiiId);
            _runtime = new Material(source)
            {
                name = source.name + " (Ocean Runtime)",
                hideFlags = HideFlags.DontSave
            };

            for (int i = 0; i < _targets.Length; i++)
            {
                _targets[i].sharedMaterial = _runtime;
            }
        }

        public static bool TryCreate(
            Renderer[] targets,
            Material source,
            out OceanOpticsMaterial binding)
        {
            binding = null;

            if (source == null || targets == null || targets.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < RequiredProperties.Length; i++)
            {
                if (!source.HasProperty(RequiredProperties[i]))
                {
                    return false;
                }
            }

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] == null)
                {
                    return false;
                }

                Material[] materials = targets[i].sharedMaterials;

                if (materials.Length != 1 || materials[0] != source)
                {
                    return false;
                }
            }

            binding = new OceanOpticsMaterial(targets, source);
            return true;
        }

        public void Apply(in OceanOpticsState state)
        {
            if (_runtime == null)
            {
                return;
            }

            _runtime.SetColor(ShallowColorId, state.ShallowColor);
            _runtime.SetColor(DeepColorId, state.DeepColor);
            _runtime.SetColor(VeryDeepColorId, state.VeryDeepColor);
            _runtime.SetFloat(BaseOpacityId, state.BaseOpacity);
            _runtime.SetFloat(ShallowsOpacityId, state.ShallowsOpacity);
            _runtime.SetFloat(SmoothnessId, state.Smoothness);

            if (_hasClarity)
            {
                _runtime.SetFloat(ClarityId, state.Clarity);
            }

            if (_hasSkyLight)
            {
                _runtime.SetFloat(SkyLightId, state.SkyLight);
            }
        }

        public void ApplyZones(
            OceanOpticsProfile profile,
            float innerRadius,
            float middleRadius,
            float outerRadius)
        {
            if (_runtime == null || !_hasZones || profile == null)
            {
                return;
            }

            OceanZoneOptics inner = profile.InnerZone;
            OceanZoneOptics middle = profile.MiddleZone;
            OceanZoneOptics outer = profile.OuterZone;

            _runtime.SetVector(ZoneRadiiId, new Vector4(
                innerRadius,
                middleRadius,
                outerRadius,
                profile.ZoneBlendHalfWidth));
            _runtime.SetVector(ZoneTint0Id, ToVector(in inner));
            _runtime.SetVector(ZoneTint1Id, ToVector(in middle));
            _runtime.SetVector(ZoneTint2Id, ToVector(in outer));
            _runtime.SetVector(ZoneSkyId, new Vector4(
                inner.SkyScale, middle.SkyScale, outer.SkyScale, 0f));
        }

        private static Vector4 ToVector(in OceanZoneOptics zone)
        {
            Color tint = zone.Tint;
            return new Vector4(tint.r, tint.g, tint.b, zone.ClarityScale);
        }

        public void Dispose()
        {
            if (_runtime == null)
            {
                return;
            }

            for (int i = 0; i < _targets.Length; i++)
            {
                Renderer target = _targets[i];

                if (target != null && target.sharedMaterial == _runtime)
                {
                    target.sharedMaterial = _source;
                }
            }

            UnityEngine.Object.Destroy(_runtime);
            _runtime = null;
        }
    }
}
