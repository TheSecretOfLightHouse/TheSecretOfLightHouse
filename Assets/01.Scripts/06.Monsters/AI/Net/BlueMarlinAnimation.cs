using Lighthouse.Monsters.AI.Rules;
using Lighthouse.Monsters.AI.Server;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Net
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster), typeof(StraightAttack))]
    public sealed class BlueMarlinAnimation : NetworkBehaviour
    {
        private const byte Swim = 0;
        private const byte Chase = 1;
        private const byte Charge = 2;
        private const byte Dead = 3;
        private static readonly int SwimHash = Animator.StringToHash("Base Layer.Swim");
        private static readonly int ChaseHash = Animator.StringToHash("Base Layer.Swim_BoatDetected");
        private static readonly int ChargeHash = Animator.StringToHash("Base Layer.Dash_Charge_Thrust");

        [SerializeField] private Animator _animator;
        [SerializeField, Min(0f)] private float _transitionDuration = 0.12f;

        // 상태 변경 훅에서 해당 상태의 시작 시각을 읽도록 시각 필드를 먼저 동기화한다.
        [SyncVar] private double _stateStartedAt;
        [SyncVar(hook = nameof(OnAnimationStateChanged))] private byte _animationState;

        private SeaMonster _monster;
        private StraightAttack _attack;
        private int _renderedState = -1;

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            _attack = GetComponent<StraightAttack>();
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
            if (isServer)
            {
                byte nextState = GetServerAnimationState();
                if (_animationState != nextState)
                {
                    _stateStartedAt = NetworkTime.time;
                    _animationState = nextState;
                }
            }

            // 모델 비활성화로 적용하지 못한 상태는 모델이 다시 활성화되면 재시도한다.
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
            _animationState = GetServerAnimationState();
        }

        public override void OnStartClient()
        {
            ApplyAnimation(true);
        }

        private byte GetServerAnimationState()
        {
            if (_monster.IsDead)
            {
                return Dead;
            }

            if (_attack.IsCharging)
            {
                return Charge;
            }

            return _monster.State == MonsterState.Chase || _monster.State == MonsterState.Attack
                ? Chase : Swim;
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

            // 사망 클립이 없으므로 사망 후 수영하지 않도록 현재 자세에서 멈춘다.
            if (_animationState == Dead)
            {
                _animator.speed = 0f;
                _renderedState = Dead;
                return;
            }

            int stateHash = _animationState == Charge ? ChargeHash
                : _animationState == Chase ? ChaseHash : SwimHash;
            float elapsed = (float)System.Math.Max(0d, NetworkTime.time - _stateStartedAt);
            _animator.speed = 1f;
            if (immediately)
            {
                _animator.PlayInFixedTime(stateHash, 0, elapsed);
            }
            else
            {
                _animator.CrossFadeInFixedTime(stateHash, _transitionDuration, 0, elapsed);
            }

            _renderedState = _animationState;
        }
    }
}
