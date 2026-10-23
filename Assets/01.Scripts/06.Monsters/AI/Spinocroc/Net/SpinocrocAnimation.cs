using System;
using Lighthouse.Monsters.AI.Net;
using Lighthouse.Monsters.Spinocroc.Rules;
using Lighthouse.Monsters.Spinocroc.Server;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.Spinocroc.Net
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster), typeof(SpinocrocPreparation))]
    public sealed class SpinocrocAnimation : NetworkBehaviour
    {
        private static readonly int PatrolHash = Animator.StringToHash("Base Layer.SurfacePatrol");
        private static readonly int DiveHash = Animator.StringToHash("Base Layer.Dive");
        private static readonly int ReappearHash = Animator.StringToHash("Base Layer.Reappear");
        private static readonly int WindupHash = Animator.StringToHash("Base Layer.ChargeWindup");

        [SerializeField] private Animator _animator;
        [SerializeField] private AnimationClip _patrolClip;
        [SerializeField] private AnimationClip _diveClip;
        [SerializeField] private AnimationClip _reappearClip;
        [SerializeField] private AnimationClip _windupClip;

        // 단계 훅이 같은 패킷의 시작 시각과 길이를 읽을 수 있도록 먼저 직렬화한다.
        [SyncVar] private double _stateStartedAt;
        [SyncVar] private float _stateDuration;
        [SyncVar(hook = nameof(OnPhaseChanged))] private SpinocrocPhase _phase;

        private SpinocrocPreparation _preparation;

        private void Awake()
        {
            _preparation = GetComponent<SpinocrocPreparation>();
            if (_animator != null)
            {
                // 이동은 서버 Rigidbody가 맡으므로 리그의 루트 이동이 중복 적용되지 않게 한다.
                _animator.applyRootMotion = false;
            }
        }

        public override void OnStartServer()
        {
            _preparation.PhaseChanged += HandlePhaseChanged;
            HandlePhaseChanged(_preparation.Phase);
        }

        public override void OnStopServer()
        {
            if (_preparation != null)
            {
                _preparation.PhaseChanged -= HandlePhaseChanged;
            }
        }

        public override void OnStartClient()
        {
            ApplyAnimation();
        }

        private void HandlePhaseChanged(SpinocrocPhase phase)
        {
            _stateStartedAt = phase == SpinocrocPhase.Patrol ? NetworkTime.time : _preparation.PhaseStartedAt;
            _stateDuration = _preparation.PhaseDuration;
            _phase = phase;
        }

        private void OnPhaseChanged(SpinocrocPhase oldValue, SpinocrocPhase newValue)
        {
            if (isClient)
            {
                ApplyAnimation();
            }
        }

        private void ApplyAnimation()
        {
            if (_animator == null || !_animator.isActiveAndEnabled
                || _animator.runtimeAnimatorController == null || _patrolClip == null)
            {
                return;
            }
            if (_phase == SpinocrocPhase.Dead)
            {
                _animator.speed = 0f;
                return;
            }

            AnimationClip clip = _patrolClip;
            int hash = PatrolHash;
            switch (_phase)
            {
                case SpinocrocPhase.Diving:
                    clip = _diveClip;
                    hash = DiveHash;
                    break;
                case SpinocrocPhase.Reappearing:
                    clip = _reappearClip;
                    hash = ReappearHash;
                    break;
                case SpinocrocPhase.Windup:
                case SpinocrocPhase.DirectionLocked:
                case SpinocrocPhase.ReadyForCharge:
                    clip = _windupClip;
                    hash = WindupHash;
                    break;
            }
            if (clip == null || clip.length <= 0f)
            {
                return;
            }

            float elapsed = (float)Math.Max(0d, NetworkTime.time - _stateStartedAt);
            bool holdPose = _phase == SpinocrocPhase.DirectionLocked || _phase == SpinocrocPhase.ReadyForCharge;
            float normalized = _phase == SpinocrocPhase.Patrol
                ? elapsed / clip.length % 1f
                : Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _stateDuration));
            _animator.speed = holdPose ? 0f : (_stateDuration > 0f ? clip.length / _stateDuration : 1f);
            _animator.Play(hash, 0, holdPose ? 1f : normalized);
        }
    }
}
