using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Net
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster))]
    public sealed class BlueSharkAnimation : NetworkBehaviour
    {
        public const byte Idle = 0;
        public const byte DashAttack = 1;
        public const byte Dash = 2;
        private const byte Dead = 3;
        private static readonly int StateHash = Animator.StringToHash("AnimationState");
        private static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int AttackStartHash = Animator.StringToHash("Base Layer.DashAttackStart");
        private static readonly int AttackLoopHash = Animator.StringToHash("Base Layer.DashAttackLoop");
        private static readonly int DashHash = Animator.StringToHash("Base Layer.Dash");

        [SerializeField] private Animator _animator;
        [SerializeField, Min(0f)] private float _transitionDuration = 0.1f;
        [SerializeField, Min(0f)] private float _attackOpeningDuration = 0.4f;
        [SyncVar] private double _stateStartedAt;
        [SyncVar(hook = nameof(OnAnimationStateChanged))] private byte _animationState;

        private SeaMonster _monster;
        private int _renderedState = -1;

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>(true);
            }
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
            }
        }

        private void LateUpdate()
        {
            if (isServer && _monster.IsDead && _animationState != Dead)
            {
                SetAnimationState(Dead);
            }
            if (isClient && _renderedState != _animationState)
            {
                ApplyAnimation(_renderedState < 0);
            }
        }

        private void OnDisable()
        {
            _renderedState = -1;
        }

        public override void OnStartServer()
        {
            _stateStartedAt = NetworkTime.time;
        }

        public override void OnStartClient()
        {
            ApplyAnimation(true);
        }

        [Server]
        public void SetAnimationState(byte state)
        {
            if (_monster.IsDead)
            {
                state = Dead;
            }
            if (state > Dead || _animationState == state)
            {
                return;
            }
            // 원격 클라이언트가 알맞은 시점부터 재생하도록 상태보다 시작 시각을 먼저 동기화한다.
            _stateStartedAt = NetworkTime.time;
            _animationState = state;
        }

        private void OnAnimationStateChanged(byte oldValue, byte newValue)
        {
            if (isClient)
            {
                ApplyAnimation(false);
            }
        }

        private void ApplyAnimation(bool immediately)
        {
            if (_animator == null || !_animator.isActiveAndEnabled
                || _animator.runtimeAnimatorController == null)
            {
                return;
            }
            if (_animationState == Dead)
            {
                _animator.speed = 0f;
                _renderedState = Dead;
                return;
            }

            float elapsed = (float)System.Math.Max(0d, NetworkTime.time - _stateStartedAt);
            int hash = _animationState == Dash ? DashHash : IdleHash;
            if (_animationState == DashAttack)
            {
                hash = elapsed < _attackOpeningDuration ? AttackStartHash : AttackLoopHash;
                if (elapsed >= _attackOpeningDuration)
                {
                    elapsed -= _attackOpeningDuration;
                }
            }
            _animator.speed = 1f;
            _animator.SetInteger(StateHash, _animationState);
            if (immediately)
            {
                _animator.PlayInFixedTime(hash, 0, elapsed);
            }
            else
            {
                _animator.CrossFadeInFixedTime(hash, _transitionDuration, 0, elapsed);
            }
            _renderedState = _animationState;
        }
    }
}
