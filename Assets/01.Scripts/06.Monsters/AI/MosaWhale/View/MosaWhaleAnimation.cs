using System;
using Lighthouse.Monsters.MosaWhale.Rules;
using Lighthouse.Monsters.MosaWhale.Server;
using UnityEngine;
using Mirror;

namespace Lighthouse.Monsters.MosaWhale.View
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MosaWhaleCycle))]
    public sealed class MosaWhaleAnimation : MonoBehaviour
    {
        private static readonly int PatrolHash = Animator.StringToHash("Base Layer.UnderwaterPatrol");
        private static readonly int DiveHash = Animator.StringToHash("Base Layer.DeepDive");
        private static readonly int BreachHash = Animator.StringToHash("Base Layer.BreachAttackReentry");
        private static readonly int RecoveryHash = Animator.StringToHash("Base Layer.UnderwaterRecover");

        [SerializeField] private Animator _animator;
        [SerializeField] private AnimationClip _patrolClip;
        [SerializeField] private AnimationClip _diveClip;
        [SerializeField] private AnimationClip _breachClip;
        [SerializeField] private AnimationClip _recoveryClip;

        private MosaWhaleCycle _cycle;
        private double _lastStartedAt = -1d;
        private MosaWhalePhase _lastPhase;
        private bool _hasApplied;

        private void Awake()
        {
            _cycle = GetComponent<MosaWhaleCycle>();
            if (_animator != null)
            {
                // FBX 리그의 크기·이동 곡선이 네트워크 루트에 중복 적용되지 않도록 한다.
                _animator.applyRootMotion = false;
            }
        }

        private void OnEnable()
        {
            _hasApplied = false;
        }

        private void LateUpdate()
        {
            if (_cycle == null || !_cycle.isClient || _animator == null
                || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null)
            {
                return;
            }
            if (!_hasApplied || _lastPhase != _cycle.Phase || _lastStartedAt != _cycle.PhaseStartedAt)
            {
                ApplyAnimation();
            }
        }

        private void OnDisable()
        {
            if (_animator != null)
            {
                _animator.speed = 0f;
            }
        }

        private void ApplyAnimation()
        {
            _lastPhase = _cycle.Phase;
            _lastStartedAt = _cycle.PhaseStartedAt;
            _hasApplied = true;
            if (_lastPhase == MosaWhalePhase.Dead)
            {
                _animator.speed = 0f;
                return;
            }
            AnimationClip clip = _patrolClip;
            int hash = PatrolHash;
            float duration = _cycle.PhaseDuration;
            double startedAt = _cycle.PhaseStartedAt;
            bool hold = false;
            bool loop = _lastPhase == MosaWhalePhase.Patrol;
            if (IsBreach(_lastPhase))
            {
                // 단계가 바뀌어도 클립을 처음부터 재생하지 않고 공통 경과시간에 맞춘다.
                clip = _breachClip;
                hash = BreachHash;
                duration = _cycle.BreachDuration;
                startedAt = _cycle.BreachStartedAt;
            }
            else if (_lastPhase == MosaWhalePhase.Diving || _lastPhase == MosaWhalePhase.Telegraph)
            {
                clip = _diveClip;
                hash = DiveHash;
                hold = _lastPhase == MosaWhalePhase.Telegraph;
            }
            else if (_lastPhase == MosaWhalePhase.Recovering)
            {
                clip = _recoveryClip;
                hash = RecoveryHash;
            }
            if (clip == null || clip.length <= 0f)
            {
                return;
            }
            float elapsed = (float)Math.Max(0d, NetworkTime.time - startedAt);
            float normalized = loop ? elapsed / clip.length % 1f
                : Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            _animator.speed = hold ? 0f : (loop ? 1f : clip.length / Mathf.Max(0.01f, duration));
            _animator.Play(hash, 0, hold ? 1f : normalized);
        }

        private static bool IsBreach(MosaWhalePhase phase)
        {
            return phase == MosaWhalePhase.Rising || phase == MosaWhalePhase.Apex
                || phase == MosaWhalePhase.Reentering;
        }
    }
}
