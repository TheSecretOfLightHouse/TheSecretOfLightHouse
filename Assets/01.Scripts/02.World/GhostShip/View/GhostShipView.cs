using Lighthouse.World.GhostShip.Net;
using Lighthouse.World.GhostShip.Rules;
using UnityEngine;

namespace Lighthouse.World.GhostShip.View
{
    // Shows the ship only while it is out, and lets its fog bank fade out on its own while it sinks.
    // The sea is see-through, so under the surface the lamp dims with depth and the ship is hidden once fully under;
    // otherwise a rising or sinking ship glows from deep water far away.
    [DisallowMultipleComponent]
    public sealed class GhostShipView : MonoBehaviour
    {
        [SerializeField] private GhostShipNet _ship;
        [SerializeField] private GameObject _visualRoot;
        [SerializeField] private Light _lamp;
        [SerializeField] private ParticleSystem[] _fog;
        [Tooltip("Depth below the sea at which the lamp is fully off.")]
        [SerializeField, Min(0.1f)] private float _lampFadeDepth = 6f;
        [Tooltip("Depth below the sea at which the whole ship is hidden (the hull and masts are under by then).")]
        [SerializeField, Min(0f)] private float _hideDepth = 10f;

        private bool _isOut;
        private float _lampIntensity;

        private void Awake()
        {
            _lampIntensity = _lamp != null ? _lamp.intensity : 0f;
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
        }

        private void HandlePhaseChanged(GhostShipPhase phase)
        {
            Apply(phase);
        }

        private void Apply(GhostShipPhase phase)
        {
            _isOut = phase != GhostShipPhase.Hidden;
            ApplyDepth();

            bool hasFog = phase == GhostShipPhase.Rising || phase == GhostShipPhase.Roaming;
            if (_fog == null)
            {
                return;
            }

            for (int i = 0; i < _fog.Length; i++)
            {
                if (hasFog)
                {
                    _fog[i].Play(true);
                }
                else
                {
                    _fog[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
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
    }
}
