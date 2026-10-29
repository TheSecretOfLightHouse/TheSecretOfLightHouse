using Lighthouse.World.GhostShip.Net;
using Lighthouse.World.GhostShip.Rules;
using UnityEngine;

namespace Lighthouse.World.GhostShip.View
{
    // Shows the ship only while it is out, and lets its fog bank fade out on its own while it sinks.
    [DisallowMultipleComponent]
    public sealed class GhostShipView : MonoBehaviour
    {
        [SerializeField] private GhostShipNet _ship;
        [SerializeField] private GameObject _visualRoot;
        [SerializeField] private Light _lamp;
        [SerializeField] private ParticleSystem[] _fog;

        private void Awake()
        {
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

        private void HandlePhaseChanged(GhostShipPhase phase)
        {
            Apply(phase);
        }

        private void Apply(GhostShipPhase phase)
        {
            bool isVisible = phase != GhostShipPhase.Hidden;
            if (_visualRoot != null)
            {
                _visualRoot.SetActive(isVisible);
            }

            if (_lamp != null)
            {
                _lamp.enabled = isVisible;
            }

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
    }
}
