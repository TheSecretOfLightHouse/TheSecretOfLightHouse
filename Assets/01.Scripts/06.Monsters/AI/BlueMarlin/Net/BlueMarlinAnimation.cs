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
        private static readonly int TurnHash = Animator.StringToHash("Turn");

        [SerializeField] private Animator _animator;
        [SerializeField, Min(0f)] private float _transitionDuration = 0.12f;
        [SerializeField, Min(1f)] private float _turnRateForFullBlend = 60f;
        [SerializeField, Min(0.01f)] private float _turnBlendResponse = 0.2f;
        [SerializeField, Min(0.1f)] private float _turnSampleResetDistance = 10f;

        // 상태 변경 훅에서 해당 상태의 시작 시각을 읽도록 시각 필드를 먼저 동기화한다.
        [SyncVar] private double _stateStartedAt;
        [SyncVar(hook = nameof(OnAnimationStateChanged))] private byte _animationState;

        private SeaMonster _monster;
        private StraightAttack _attack;
        private int _renderedState = -1;
        private Vector3 _previousForward;
        private Vector3 _previousPosition;
        private bool _hasTurnSample;
        private float _smoothedAngularSpeed;

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
            if (isClient)
            {
                UpdateTurnBlend(Time.deltaTime);
            }
        }

        private void OnDisable()
        {
            _renderedState = -1;
            _hasTurnSample = false;
        }

        public override void OnStartServer()
        {
            _stateStartedAt = NetworkTime.time;
            _animationState = GetServerAnimationState();
        }

        public override void OnStartClient()
        {
            _hasTurnSample = false;
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
            // 원격에 표시된 회전에서 선회를 읽고 추적·돌진·순간이동 표본은 제외한다.
            if (_hasTurnSample && _animationState == Swim
                && displacement < _turnSampleResetDistance * _turnSampleResetDistance)
            {
                angularSpeed = Vector3.SignedAngle(_previousForward, forward, Vector3.up) / deltaTime;
            }
            else
            {
                _smoothedAngularSpeed = 0f;
            }
            // 정규화 전에 각속도를 평균 내 물리 틱 사이의 정지 표본으로 강도가 줄어들지 않게 한다.
            float blend = 1f - Mathf.Exp(-deltaTime / _turnBlendResponse);
            _smoothedAngularSpeed = Mathf.Lerp(_smoothedAngularSpeed, angularSpeed, blend);
            _animator.SetFloat(TurnHash, Mathf.Clamp(_smoothedAngularSpeed / _turnRateForFullBlend, -1f, 1f));
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
