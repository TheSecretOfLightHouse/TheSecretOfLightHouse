using Lighthouse.Monsters.AI.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.AI.View
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster))]
    public sealed class MantaAnimation : NetworkBehaviour
    {
        private const byte Idle = 0;
        private const byte Dash = 1;
        private const byte Dead = 2;
        private static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int DashHash = Animator.StringToHash("Base Layer.Dash");

        [SerializeField] private Animator _animator;
        [SerializeField, Min(0f)] private float _transitionDuration = 0.15f;

        [SyncVar] private double _stateStartedAt;
        [SyncVar] private byte _animationState;
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
            if (isServer)
            {
                byte state = _monster.IsDead ? Dead
                    : _monster.State == MonsterState.Patrol ? Idle : Dash;
                if (_animationState != state)
                {
                    _stateStartedAt = NetworkTime.time;
                    _animationState = state;
                }
            }
            // 서버에서도 꼬리 뼈를 피해 경로에 사용하므로 동일한 애니메이션을 갱신한다.
            if ((isServer || isClient) && _renderedState != _animationState)
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
            if (_animator != null)
            {
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            ApplyAnimation(true);
        }

        public override void OnStartClient()
        {
            ApplyAnimation(true);
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
            _animator.speed = 1f;
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
