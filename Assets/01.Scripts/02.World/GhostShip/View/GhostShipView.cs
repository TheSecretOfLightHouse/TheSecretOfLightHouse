using Lighthouse.Lantern.View;
using Lighthouse.World.GhostShip.Net;
using Lighthouse.World.GhostShip.Rules;
using UnityEngine;

namespace Lighthouse.World.GhostShip.View
{
    // Shows the ship only while it is out. Its fog bank stays off through the diagonal rise and slowly fades in once the
    // ship levels out and starts roaming (2026-10-31); it fades out over a second the moment the ship starts sinking,
    // rather than lingering on the water while the particles live out their lifetime.
    // The sea is see-through, so under the surface the lamp dims with depth and the ship is hidden once fully under;
    // otherwise a rising or sinking ship glows from deep water far away.
    [DisallowMultipleComponent]
    public sealed class GhostShipView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private GhostShipNet _ship;
        [SerializeField] private GameObject _visualRoot;
        [SerializeField] private Light _lamp;
        [Tooltip("Deck lanterns; they dim with depth like the lamp.")]
        [SerializeField] private LanternFlameFlicker[] _lanterns;
        [SerializeField] private ParticleSystem[] _fog;
        [Tooltip("Depth below the sea at which the lamp is fully off.")]
        [SerializeField, Min(0.1f)] private float _lampFadeDepth = 6f;
        [Tooltip("Depth below the sea at which the whole ship is hidden (the hull and masts are under by then).")]
        [SerializeField, Min(0f)] private float _hideDepth = 10f;
        [Tooltip("Seconds the fog takes to fade out once the ship starts sinking.")]
        [SerializeField, Min(0.01f)] private float _fogFadeSeconds = 1f;
        [Tooltip("Seconds the fog takes to fade in once the ship has risen and levelled out.")]
        [SerializeField, Min(0.01f)] private float _fogFadeInSeconds = 4f;

        private bool _isOut;
        private float _lampIntensity;
        private ParticleSystemRenderer[] _fogRenderers;
        private Color[] _fogColors;
        private MaterialPropertyBlock _fogBlock;
        // Seconds into the fog fade; below zero when not fading.
        private float _fogFadeTime = -1f;
        private bool _isFogFadingIn;

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

            if (phase == GhostShipPhase.Roaming)
            {
                _fogFadeTime = 0f;
                _isFogFadingIn = true;
                SetFogAlpha(0f);

                for (int i = 0; i < _fog.Length; i++)
                {
                    _fog[i].Play(true);
                }

                return;
            }

            if (phase == GhostShipPhase.Sinking)
            {
                for (int i = 0; i < _fog.Length; i++)
                {
                    _fog[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }

                // A fade-in still under way carries on down from where it is.
                _fogFadeTime = _isFogFadingIn ? (1f - FogFadeInAlpha()) * _fogFadeSeconds : 0f;
                _isFogFadingIn = false;
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

            float lampScale = 1f - Mathf.Clamp01(depth / _lampFadeDepth);

            if (_lamp != null)
            {
                float intensity = _lampIntensity * lampScale;
                _lamp.intensity = intensity;
                _lamp.enabled = isShown && intensity > 0f;
            }

            if (_lanterns != null)
            {
                for (int i = 0; i < _lanterns.Length; i++)
                {
                    if (_lanterns[i] != null)
                    {
                        _lanterns[i].Brightness = lampScale;
                    }
                }
            }
        }

        private void StepFogFade()
        {
            _fogFadeTime += Time.deltaTime;

            if (_isFogFadingIn)
            {
                float fadeIn = FogFadeInAlpha();
                SetFogAlpha(fadeIn);

                if (fadeIn >= 1f)
                {
                    _fogFadeTime = -1f;
                    _isFogFadingIn = false;
                }

                return;
            }

            float alpha = 1f - Mathf.Clamp01(_fogFadeTime / _fogFadeSeconds);
            SetFogAlpha(alpha);

            if (alpha <= 0f)
            {
                ClearFog();
            }
        }

        private float FogFadeInAlpha()
        {
            return Mathf.Clamp01(_fogFadeTime / _fogFadeInSeconds);
        }

        private void ClearFog()
        {
            _fogFadeTime = -1f;
            _isFogFadingIn = false;

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
