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
        private static readonly int TurnHash = Animator.StringToHash("Turn");

        [SerializeField] private Animator _animator;
        [SerializeField] private AnimationClip _patrolClip;
        [SerializeField] private AnimationClip _diveClip;
        [SerializeField] private AnimationClip _breachClip;
        [SerializeField] private AnimationClip _recoveryClip;
        [SerializeField, Min(1f)] private float _turnRateForFullBlend = 60f;
        [SerializeField, Min(0.01f)] private float _turnBlendResponse = 0.2f;
        [SerializeField, Min(0.1f)] private float _turnSampleResetDistance = 10f;
        [Header("공중 강제 재입수 전환 시간")]
        [SerializeField, Min(0.01f)] private float _interruptedReentryBlendDuration = 0.15f;

        private MosaWhaleCycle _cycle;
        private double _lastStartedAt = -1d;
        private MosaWhalePhase _lastPhase;
        private bool _hasApplied;
        private Vector3 _previousForward;
        private Vector3 _previousPosition;
        private bool _hasTurnSample;
        private float _smoothedAngularSpeed;

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
            _hasTurnSample = false;
            _smoothedAngularSpeed = 0f;
        }

        private void LateUpdate()
        {
            if (_cycle == null || !_cycle.isClient || _animator == null
                || !_animator.isActiveAndEnabled || _animator.runtimeAnimatorController == null)
            {
                _hasTurnSample = false;
                return;
            }
            UpdateTurnBlend(Time.deltaTime);
            if (!_hasApplied || _lastPhase != _cycle.Phase || _lastStartedAt != _cycle.PhaseStartedAt)
            {
                ApplyAnimation();
            }
        }

        private void OnDisable()
        {
            _hasTurnSample = false;
            _smoothedAngularSpeed = 0f;
            if (_animator != null)
            {
                _animator.speed = 0f;
            }
        }

        private void UpdateTurnBlend(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                _hasTurnSample = false;
                return;
            }
            Vector3 forward = transform.forward;
            forward.y = 0f;
            Vector3 position = transform.position;
            float angularSpeed = 0f;
            float displacement = (position - _previousPosition).sqrMagnitude;
            bool isPatrol = _cycle.Phase == MosaWhalePhase.Patrol && forward.sqrMagnitude > 0.0001f;

            // Sample the interpolated network root without treating attack rotations or teleports as steering.
            if (_hasTurnSample && isPatrol
                && displacement < _turnSampleResetDistance * _turnSampleResetDistance)
            {
                angularSpeed = Vector3.SignedAngle(_previousForward, forward, Vector3.up) / deltaTime;
            }
            else
            {
                _smoothedAngularSpeed = 0f;
            }
            float blend = 1f - Mathf.Exp(-deltaTime / _turnBlendResponse);
            _smoothedAngularSpeed = Mathf.Lerp(_smoothedAngularSpeed, angularSpeed, blend);
            _animator.SetFloat(TurnHash, Mathf.Clamp(_smoothedAngularSpeed / _turnRateForFullBlend, -1f, 1f));
            _previousForward = forward;
            _previousPosition = position;
            _hasTurnSample = isPatrol;
        }

        private void ApplyAnimation()
        {
            bool interruptedReentry = _hasApplied && _cycle.Phase == MosaWhalePhase.Reentering
                && _lastPhase != MosaWhalePhase.Apex;
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
            if (interruptedReentry)
            {
                _animator.CrossFadeInFixedTime(hash, _interruptedReentryBlendDuration, 0, normalized * clip.length);
            }
            else
            {
                _animator.Play(hash, 0, hold ? 1f : normalized);
            }
        }

        private static bool IsBreach(MosaWhalePhase phase)
        {
            return phase == MosaWhalePhase.Rising || phase == MosaWhalePhase.Apex
                || phase == MosaWhalePhase.Reentering;
        }
    }
}
