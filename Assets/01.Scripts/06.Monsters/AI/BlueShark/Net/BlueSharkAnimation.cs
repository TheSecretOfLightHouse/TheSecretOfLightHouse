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
        private static readonly int BiteHash = Animator.StringToHash("Base Layer.Bite");
        private static readonly int DashHash = Animator.StringToHash("Base Layer.Dash");
        private static readonly int TurnHash = Animator.StringToHash("Turn");

        [SerializeField] private Animator _animator;
        [SerializeField, Min(0f)] private float _transitionDuration = 0.1f;
        [SerializeField, Min(0f)] private float _attackOpeningDuration = 0.4f;
        [SerializeField] private AnimationClip _biteClip;
        [SerializeField, Min(1f)] private float _turnRateForFullBlend = 60f;
        [SerializeField, Min(0.01f)] private float _turnBlendResponse = 0.2f;
        [SerializeField, Min(0.1f)] private float _turnSampleResetDistance = 10f;
        [SyncVar] private double _stateStartedAt;
        [SyncVar(hook = nameof(OnAnimationStateChanged))] private byte _animationState;

        private SeaMonster _monster;
        private int _renderedState = -1;
        private Vector3 _previousForward;
        private Vector3 _previousPosition;
        private bool _hasTurnSample;
        private float _smoothedAngularSpeed;
        private double _biteEndsAt;
        private bool _isBiting;

        public float BiteDuration => _biteClip != null ? _biteClip.length : 0f;

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
            if (isClient && _isBiting && (_animationState == Dead || NetworkTime.time >= _biteEndsAt))
            {
                _isBiting = false;
                _renderedState = -1;
            }
            if (isClient && !_isBiting && _renderedState != _animationState)
            {
                ApplyAnimation(_renderedState < 0);
            }
            if (isClient)
            {
                UpdateTurnBlend(Time.deltaTime);
            }
        }

        private void OnDisable()
        {
            _renderedState = -1;
            _hasTurnSample = false;
            _isBiting = false;
        }

        public override void OnStartServer()
        {
            _stateStartedAt = NetworkTime.time;
        }

        public override void OnStartClient()
        {
            _hasTurnSample = false;
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

        [Server]
        public void PlayBite()
        {
            if (_monster != null && !_monster.IsDead)
            {
                RpcPlayBite(NetworkTime.time);
            }
        }

        [Server]
        public void CancelBite()
        {
            RpcCancelBite();
        }

        [ClientRpc]
        private void RpcCancelBite()
        {
            if (_isBiting)
            {
                _isBiting = false;
                _renderedState = -1;
            }
        }

        [ClientRpc]
        private void RpcPlayBite(double startedAt)
        {
            if (_animator == null || !_animator.isActiveAndEnabled
                || _animator.runtimeAnimatorController == null || _biteClip == null
                || _animationState == Dead)
            {
                return;
            }

            float elapsed = (float)System.Math.Max(0d, NetworkTime.time - startedAt);
            if (elapsed >= _biteClip.length)
            {
                return;
            }

            // Keep the same clip position on clients receiving the impact at different times.
            _biteEndsAt = startedAt + _biteClip.length;
            _isBiting = true;
            _animator.speed = 1f;
            _animator.PlayInFixedTime(BiteHash, 0, elapsed);
        }

        private void OnAnimationStateChanged(byte oldValue, byte newValue)
        {
            if (isClient && (!_isBiting || newValue == Dead))
            {
                if (newValue == Dead)
                {
                    _isBiting = false;
                }
                ApplyAnimation(false);
            }
        }

        private void UpdateTurnBlend(float deltaTime)
        {
            if (_animator == null || !_animator.isActiveAndEnabled
                || _animator.runtimeAnimatorController == null || deltaTime <= 0f)
            {
                _hasTurnSample = false;
                return;
            }
            Vector3 forward = transform.forward;
            forward.y = 0f;
            Vector3 position = transform.position;
            float angularSpeed = 0f;
            float displacement = (position - _previousPosition).sqrMagnitude;
            // 원격 보간 결과에서 실제 선회를 읽으며 순간이동과 공격 회전은 선회 자세에 섞지 않는다.
            if (_hasTurnSample && _animationState == Idle
                && displacement < _turnSampleResetDistance * _turnSampleResetDistance)
            {
                angularSpeed = Vector3.SignedAngle(_previousForward, forward, Vector3.up) / deltaTime;
            }
            else
            {
                _smoothedAngularSpeed = 0f;
            }
            // 물리 갱신 사이의 0도 표본과 회전 표본을 먼저 평균 내야 호스트의 선회 강도가 줄어들지 않는다.
            float blend = 1f - Mathf.Exp(-deltaTime / _turnBlendResponse);
            _smoothedAngularSpeed = Mathf.Lerp(_smoothedAngularSpeed, angularSpeed, blend);
            float turn = Mathf.Clamp(_smoothedAngularSpeed / _turnRateForFullBlend, -1f, 1f);
            _animator.SetFloat(TurnHash, turn);
            _previousForward = forward;
            _previousPosition = position;
            _hasTurnSample = true;
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
