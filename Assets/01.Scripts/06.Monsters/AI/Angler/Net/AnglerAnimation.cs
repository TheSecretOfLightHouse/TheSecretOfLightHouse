using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Net
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster))]
    public sealed class AnglerAnimation : NetworkBehaviour
    {
        private static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int DashHash = Animator.StringToHash("Base Layer.Dash");
        private static readonly int BiteHash = Animator.StringToHash("Base Layer.BiteAttack");

        [SerializeField] private Animator _animator;
        [SerializeField, Min(0f)] private float _transitionDuration = 0.1f;
        [SyncVar] private double _stateStartedAt;
        [SyncVar] private MonsterState _animationState;
        private SeaMonster _monster;
        private int _renderedState = -1;

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
        }

        private void OnEnable()
        {
            _monster.StateChanged += HandleStateChanged;
        }

        private void LateUpdate()
        {
            if (!isClient || _animator == null || !_animator.isActiveAndEnabled)
            {
                _renderedState = -1;
                return;
            }
            if (_renderedState != (int)_animationState)
            {
                ApplyAnimation(_renderedState < 0);
            }
        }

        private void OnDisable()
        {
            _monster.StateChanged -= HandleStateChanged;
            _renderedState = -1;
        }

        public override void OnStartServer()
        {
            _stateStartedAt = NetworkTime.time;
            _animationState = _monster.State;
        }

        private void HandleStateChanged(MonsterState state)
        {
            if (!isServer || _animationState == state)
            {
                return;
            }
            // Keep hidden visuals in sync without activating them on other observers.
            _stateStartedAt = NetworkTime.time;
            _animationState = state;
        }

        private void ApplyAnimation(bool immediately)
        {
            if (_animator.runtimeAnimatorController == null)
            {
                return;
            }
            if (_animationState == MonsterState.Dead)
            {
                _animator.speed = 0f;
            }
            else
            {
                int hash = IdleHash;
                if (_animationState == MonsterState.Attack)
                {
                    hash = BiteHash;
                }
                else if (_animationState == MonsterState.Chase || _animationState == MonsterState.Return)
                {
                    hash = DashHash;
                }
                float elapsed = (float)System.Math.Max(0d, NetworkTime.time - _stateStartedAt);
                _animator.speed = 1f;
                if (immediately)
                {
                    _animator.PlayInFixedTime(hash, 0, elapsed);
                }
                else
                {
                    _animator.CrossFadeInFixedTime(hash, _transitionDuration, 0, elapsed);
                }
            }
            _renderedState = (int)_animationState;
        }
    }
}
