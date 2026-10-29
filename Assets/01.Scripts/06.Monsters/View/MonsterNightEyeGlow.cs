using System.Collections;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.Monsters.View
{
    public sealed class MonsterNightEyeGlow : MonoBehaviour
    {
        [SerializeField] private float _refreshInterval = 0.2f;

        private Renderer[] _renderers;
        private ParticleSystem[] _particles;
        private TrailRenderer[] _trails;
        private bool _isVisible;
        private bool _hasAppliedVisibility;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _particles = GetComponentsInChildren<ParticleSystem>(true);
            _trails = GetComponentsInChildren<TrailRenderer>(true);
        }

        private void OnEnable()
        {
            RefreshVisibility();
            StartCoroutine(RefreshLoop());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            SetVisibility(false);
            _hasAppliedVisibility = false;
        }

        private IEnumerator RefreshLoop()
        {
            var delay = new WaitForSecondsRealtime(Mathf.Max(0.05f, _refreshInterval));
            while (true)
            {
                yield return delay;
                RefreshVisibility();
            }
        }

        private void RefreshVisibility()
        {
            bool isNight = WorldServices.TryGet<IGameClock>(out IGameClock clock)
                && clock.Phase == DayPhase.Night;
            SetVisibility(isNight);
        }

        private void SetVisibility(bool isVisible)
        {
            if (_hasAppliedVisibility && _isVisible == isVisible)
            {
                return;
            }

            _isVisible = isVisible;
            _hasAppliedVisibility = true;
            foreach (Renderer effectRenderer in _renderers)
            {
                effectRenderer.enabled = isVisible;
            }

            foreach (ParticleSystem particle in _particles)
            {
                if (isVisible)
                {
                    particle.Play(false);
                }
                else
                {
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            if (!isVisible)
            {
                foreach (TrailRenderer trail in _trails)
                {
                    trail.Clear();
                }
            }
        }
    }
}
