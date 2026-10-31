using Lighthouse.Tower.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.Boss
{
    [DefaultExecutionOrder(20000)]
    [DisallowMultipleComponent]
    public sealed class SummonTentacleCombatView : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private AnimationClip _emergeClip;
        [SerializeField] private AnimationClip _squeezeClip;
        [SerializeField] private LighthouseImpactParticles _impactParticles;
        [SerializeField, Range(0.01f, 0.99f)] private float _squeezePeakNormalizedTime = 0.3448276f;

        private BossTestEnvironment _boss;

        public void Bind(BossTestEnvironment boss)
        {
            _boss = boss;
            if (_impactParticles != null)
            {
                _impactParticles.Bind(boss);
            }
        }

        private void LateUpdate()
        {
            if (!NetworkClient.active || !NetworkClient.ready || _boss == null || _animator == null
                || _emergeClip == null || _squeezeClip == null || !_boss.HasSummoned
                || _boss.IsSpecialEnding || _boss.TowerMatch == null
                || _boss.TowerMatch.IsSummonCinematicPlaying || _boss.SqueezeDuration <= 0f)
            {
                return;
            }

            bool hasStopped = _boss.Phase == BossPhase.LighthouseDestroyed || _boss.Phase == BossPhase.BattleStopped;
            if (_boss.TowerMatch.Phase != TowerPhase.FinalBattle && !hasStopped)
            {
                return;
            }

            double now = hasStopped ? _boss.VisualStoppedAt : NetworkTime.time;
            double elapsed = now - _boss.AttackStartedAt;
            if (_boss.AttackSequence == 0 || elapsed < 0d || elapsed >= _boss.SqueezeDuration)
            {
                _emergeClip.SampleAnimation(_animator.gameObject, _emergeClip.length);
                return;
            }

            // Presentation never applies damage; its peak follows the server's hit fraction.
            float progress = Mathf.Clamp01((float)(elapsed / _boss.SqueezeDuration));
            float hit = Mathf.Clamp(_boss.DamageNormalizedTime, 0.01f, 0.99f);
            float normalizedTime = progress <= hit
                ? Mathf.Lerp(0f, _squeezePeakNormalizedTime, Mathf.SmoothStep(0f, 1f, progress / hit))
                : Mathf.Lerp(_squeezePeakNormalizedTime, 1f, Mathf.SmoothStep(0f, 1f, (progress - hit) / (1f - hit)));
            // The paused Timeline still owns the actors, so combat samples after its evaluation.
            _squeezeClip.SampleAnimation(_animator.gameObject, normalizedTime * _squeezeClip.length);
        }
    }
}
