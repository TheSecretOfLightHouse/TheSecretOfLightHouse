using Lighthouse.World.GhostShip.Net;
using Lighthouse.World.GhostShip.Rules;
using UnityEngine;

namespace Lighthouse.World.GhostShip.View
{
    // Shows the ship only while it is out. Its fog bank fades out over a second the moment it starts sinking, rather
    // than lingering on the water while the particles live out their lifetime.
    // The sea is see-through, so under the surface the lamp dims with depth and the ship is hidden once fully under;
    // otherwise a rising or sinking ship glows from deep water far away.
    [DisallowMultipleComponent]
    public sealed class GhostShipView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private GhostShipNet _ship;
        [SerializeField] private GameObject _visualRoot;
        [SerializeField] private Light _lamp;
        [SerializeField] private ParticleSystem[] _fog;
        [Tooltip("Depth below the sea at which the lamp is fully off.")]
        [SerializeField, Min(0.1f)] private float _lampFadeDepth = 6f;
        [Tooltip("Depth below the sea at which the whole ship is hidden (the hull and masts are under by then).")]
        [SerializeField, Min(0f)] private float _hideDepth = 10f;
        [Tooltip("Seconds the fog takes to fade out once the ship starts sinking.")]
        [SerializeField, Min(0.01f)] private float _fogFadeSeconds = 1f;

        private bool _isOut;
        private float _lampIntensity;
        private ParticleSystemRenderer[] _fogRenderers;
        private Color[] _fogColors;
        private MaterialPropertyBlock _fogBlock;
        // Seconds into the fog fade; below zero when not fading.
        private float _fogFadeTime = -1f;

        private void Awake()
        {
            _lampIntensity = _lamp != null ? _lamp.intensity : 0f;
            CacheFog();
            Apply(GhostShipPhase.Hidden);
        }

        private void OnEnable()
        {
            if (_ship != null)
            {
                _ship.PhaseChanged += HandlePhaseChanged;
            }
        }

        private void OnDisable()
        {
            if (_ship != null)
            {
                _ship.PhaseChanged -= HandlePhaseChanged;
            }
        }

        private void LateUpdate()
        {
            if (_isOut)
            {
                ApplyDepth();
            }

            if (_fogFadeTime >= 0f)
            {
                StepFogFade();
            }
        }

        private void HandlePhaseChanged(GhostShipPhase phase)
        {
            Apply(phase);
        }

        private void Apply(GhostShipPhase phase)
        {
            _isOut = phase != GhostShipPhase.Hidden;
            ApplyDepth();

            if (_fog == null)
            {
                return;
            }

            bool hasFog = phase == GhostShipPhase.Rising || phase == GhostShipPhase.Roaming;

            if (hasFog)
            {
                _fogFadeTime = -1f;
                SetFogAlpha(1f);

                for (int i = 0; i < _fog.Length; i++)
                {
                    _fog[i].Play(true);
                }

                return;
            }

            for (int i = 0; i < _fog.Length; i++)
            {
                _fog[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (phase == GhostShipPhase.Sinking)
            {
                _fogFadeTime = 0f;
            }
            else
            {
                ClearFog();
            }
        }

        private void ApplyDepth()
        {
            float seaLevel = _ship != null ? _ship.SeaLevel : 0f;
            float depth = seaLevel - transform.position.y;
            bool isShown = _isOut && depth < _hideDepth;

            if (_visualRoot != null && _visualRoot.activeSelf != isShown)
            {
                _visualRoot.SetActive(isShown);
            }

            if (_lamp != null)
            {
                float intensity = _lampIntensity * (1f - Mathf.Clamp01(depth / _lampFadeDepth));
                _lamp.intensity = intensity;
                _lamp.enabled = isShown && intensity > 0f;
            }
        }

        private void StepFogFade()
        {
            _fogFadeTime += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(_fogFadeTime / _fogFadeSeconds);
            SetFogAlpha(alpha);

            if (alpha <= 0f)
            {
                ClearFog();
            }
        }

        private void ClearFog()
        {
            _fogFadeTime = -1f;

            for (int i = 0; i < _fog.Length; i++)
            {
                _fog[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            SetFogAlpha(1f);
        }

        private void CacheFog()
        {
            int count = _fog != null ? _fog.Length : 0;
            _fogRenderers = new ParticleSystemRenderer[count];
            _fogColors = new Color[count];
            _fogBlock = new MaterialPropertyBlock();

            for (int i = 0; i < count; i++)
            {
                _fogRenderers[i] = _fog[i] != null ? _fog[i].GetComponent<ParticleSystemRenderer>() : null;
                Material material = _fogRenderers[i] != null ? _fogRenderers[i].sharedMaterial : null;
                _fogColors[i] = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
            }
        }

        // Scales each fog renderer's base color alpha without touching the shared material.
        private void SetFogAlpha(float alpha)
        {
            for (int i = 0; i < _fogRenderers.Length; i++)
            {
                if (_fogRenderers[i] == null)
                {
                    continue;
                }

                Color color = _fogColors[i];
                color.a *= alpha;
                _fogRenderers[i].GetPropertyBlock(_fogBlock);
                _fogBlock.SetColor(BaseColorId, color);
                _fogRenderers[i].SetPropertyBlock(_fogBlock);
            }
        }
    }
}
