using System;
using Lighthouse.Monsters.AI.Rules;
using Lighthouse.Monsters.Spawning.Server;
using Mirror;
using Unity.Behavior;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Net
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(NetworkIdentity), typeof(BehaviorGraphAgent), typeof(Rigidbody))]
    public abstract class MonsterBase : NetworkBehaviour
    {
        [Header("공통 설정")]
        [SerializeField] private MonsterProfile _profile;

        [Header("행동 트리")]
        [SerializeField] private BehaviorGraphAgent _behaviorAgent;

        private Rigidbody _body;
        private Collider[] _spawnColliders;
        private float _currentHealth;
        private MonsterState _state = MonsterState.Patrol;
        private bool _isInitialized;
        private bool _isStoppingBehavior;
        private bool _hasStartedBehavior;

        public MonsterProfile Profile => _profile;
        public float CurrentHealth => _currentHealth;
        public MonsterState State => _state;
        public bool IsInitialized => _isInitialized;
        public bool IsDead => _state == MonsterState.Dead;
        public bool IsBehaviorRunning => _hasStartedBehavior
            && _behaviorAgent != null
            && _behaviorAgent.isActiveAndEnabled
            && _behaviorAgent.Graph != null
            && _behaviorAgent.Graph.IsRunning;

        public event Action<float> HealthChanged;
        public event Action<MonsterState> StateChanged;
        public event Action<MonsterBase> Died;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _spawnColliders = GetComponentsInChildren<Collider>(true);
            SetPhysicsAuthority(false);

            if (_behaviorAgent == null)
            {
                _behaviorAgent = GetComponent<BehaviorGraphAgent>();
            }

            // Agent의 자동 Start보다 먼저 실행을 막아 클라이언트 AI를 차단한다.
            StopBehaviorInternal();
        }

        private void OnEnable()
        {
            MonsterSpawnCollisionRegistry.Register(this, _spawnColliders);
            if (netIdentity != null && isServer && _isInitialized && !IsDead)
            {
                SetPhysicsAuthority(true);
                TryStartBehavior();
            }
            else
            {
                StopBehaviorInternal();
            }
        }

        private void OnDisable()
        {
            StopBehaviorInternal();
            SetPhysicsAuthority(false);
        }

        private void OnDestroy()
        {
            // AI만 꺼도 물리 콜라이더는 활성 상태이므로 파괴될 때까지 소유권 등록을 유지한다.
            MonsterSpawnCollisionRegistry.Unregister(_spawnColliders);
        }

        public sealed override void OnStartServer()
        {
            base.OnStartServer();

            if (TryInitialize())
            {
                SetPhysicsAuthority(true);
                TryStartBehavior();
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // 리슨 호스트는 클라이언트도 시작하므로 서버의 물리 권위를 유지한다.
            SetPhysicsAuthority(isServer && _isInitialized && !IsDead);
        }

        public override void OnStopClient()
        {
            if (!isServer)
            {
                SetPhysicsAuthority(false);
            }

            base.OnStopClient();
        }

        public sealed override void OnStopServer()
        {
            // 노드 종료 콜백에서 새 행동이 시작되지 않도록 먼저 차단한다.
            _isInitialized = false;
            StopBehaviorInternal();
            SetPhysicsAuthority(false);

            base.OnStopServer();
        }

        [Server]
        public bool TryApplyDamage(float damage)
        {
            if (netIdentity == null || !isServer || !_isInitialized || IsDead || _isStoppingBehavior)
            {
                return false;
            }

            if (!IsFinite(damage) || damage <= 0f)
            {
                return false;
            }

            _currentHealth = Mathf.Max(0f, _currentHealth - damage);

            if (_currentHealth <= 0f)
            {
                Die();
            }
            else
            {
                HealthChanged?.Invoke(_currentHealth);
            }

            return true;
        }

        [Server]
        public bool TrySetBehaviorState(MonsterState nextState)
        {
            if (netIdentity == null || !isServer || !_isInitialized || IsDead || _isStoppingBehavior)
            {
                return false;
            }

            if (_state == nextState)
            {
                return false;
            }

            // 사망 상태는 공통 피격 처리에서만 진입하도록 제한한다.
            switch (nextState)
            {
                case MonsterState.Patrol:
                case MonsterState.Chase:
                case MonsterState.Attack:
                case MonsterState.Return:
                    break;

                default:
                    return false;
            }

            // 행동 선택과 취소는 BT 노드가 맡고 여기서는 관찰 상태만 반영한다.
            _state = nextState;

            StateChanged?.Invoke(_state);
            return true;
        }

        [Server]
        public bool TryStartBehavior()
        {
            if (netIdentity == null || !isServer || !_isInitialized || IsDead || _isStoppingBehavior 
                || !isActiveAndEnabled || !HasValidBehaviorAgent())
            {
                return false;
            }

            if (IsBehaviorRunning)
            {
                return true;
            }

            // 초기화와 최초 시작은 Agent에 맡기고 Update를 중복 호출하지 않는다.
            _behaviorAgent.enabled = true;
            _hasStartedBehavior = true;
            _behaviorAgent.Start();

            // 노드의 시작 콜백에서 사망하거나 비활성화될 수 있다.
            if (!_isInitialized || IsDead || !isActiveAndEnabled
                || !_hasStartedBehavior || _behaviorAgent == null || !_behaviorAgent.isActiveAndEnabled
                || !HasValidBehaviorAgent())
            {
                // 시작 콜백에서 중단된 그래프도 Start 반환 후 다시 종료 상태로 정리한다.
                _hasStartedBehavior = true;
                StopBehaviorInternal();
                return false;
            }

            return true;
        }

        [Server]
        public void StopBehavior()
        {
            if (netIdentity != null && isServer)
            {
                StopBehaviorInternal();
            }
        }

        [Server]
        private bool TryInitialize()
        {
            if (netIdentity == null || !isServer || _isInitialized)
            {
                return false;
            }

            if (_body == null)
            {
                Debug.LogError($"[{nameof(MonsterBase)}] Initialization requires a Rigidbody on the same GameObject.", this);

                return false;
            }

            if (!HasValidProfile())
            {
                Debug.LogError($"[{nameof(MonsterBase)}] Initialization requires a valid profile.", this);

                return false;
            }

            if (!HasValidBehaviorAgent())
            {
                Debug.LogError($"[{nameof(MonsterBase)}] Initialization requires a Behavior Agent with a valid graph on the same GameObject.", this);

                return false;
            }

            _currentHealth = _profile.MaxHealth;
            _state = MonsterState.Patrol;
            _isInitialized = true;

            HealthChanged?.Invoke(_currentHealth);
            StateChanged?.Invoke(_state);

            return true;
        }

        [Server]
        private void Die()
        {
            if (netIdentity == null || !isServer || !_isInitialized || IsDead)
            {
                return;
            }

            // 콜백에서 피해 요청이 다시 들어와도 사망 처리가 반복되지 않도록 먼저 확정한다.
            _currentHealth = 0f;
            _state = MonsterState.Dead;

            StopBehaviorInternal();

            StateChanged?.Invoke(_state);
            HealthChanged?.Invoke(_currentHealth);
            Died?.Invoke(this);
        }

        private void StopBehaviorInternal()
        {
            if (_behaviorAgent == null)
            {
                return;
            }

            // 자동 갱신을 차단한 뒤 실행 중인 노드를 종료한다.
            _behaviorAgent.enabled = false;

            if (_isStoppingBehavior || !_hasStartedBehavior)
            {
                return;
            }

            _hasStartedBehavior = false;
            _isStoppingBehavior = true;

            try
            {
                _behaviorAgent.End();
            }
            finally
            {
                _isStoppingBehavior = false;
            }
        }

        private bool HasValidBehaviorAgent()
        {
            return _behaviorAgent != null
                && _behaviorAgent.gameObject == gameObject
                && _behaviorAgent.Graph != null
                && _behaviorAgent.Graph.BlackboardReference != null
                && _behaviorAgent.Graph.BlackboardReference.Blackboard != null;
        }

        private void SetPhysicsAuthority(bool canSimulate)
        {
            if (_body == null)
            {
                return;
            }

            if (!canSimulate && !_body.isKinematic)
            {
                // 물리 비활성화 시 다음 서버 세션에 이전 속도가 남지 않도록 한다.
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }

            _body.isKinematic = !canSimulate;
        }

        private bool HasValidProfile()
        {
            if (_profile == null)
            {
                return false;
            }

            return IsFinite(_profile.MaxHealth)
                && _profile.MaxHealth >= 1f
                && IsFiniteNonNegative(_profile.PatrolSpeed)
                && IsFiniteNonNegative(_profile.ChaseSpeed)
                && IsFiniteNonNegative(_profile.DetectionRadius)
                && IsFiniteNonNegative(_profile.MaxChaseDistance)
                && _profile.MaxChaseDistance >= _profile.DetectionRadius
                && IsFiniteNonNegative(_profile.AttackDamage)
                && IsFiniteNonNegative(_profile.AttackCooldown);
        }

        private static bool IsFiniteNonNegative(float value)
        {
            return IsFinite(value) && value >= 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
