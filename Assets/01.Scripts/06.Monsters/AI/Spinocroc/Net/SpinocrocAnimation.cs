using System;
using Lighthouse.Monsters.AI.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.Spinocroc.Net
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster))]
    public sealed class SpinocrocAnimation : NetworkBehaviour
    {
        private static readonly int PatrolHash = Animator.StringToHash("Base Layer.SurfacePatrol");

        [SerializeField] private Animator _animator;
        [SerializeField] private AnimationClip _patrolClip;

        [SyncVar] private double _patrolStartedAt;
        [SyncVar(hook = nameof(OnDeathChanged))] private bool _isDead;

        private SeaMonster _monster;

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            if (_animator != null)
            {
                // 이동은 서버 Rigidbody가 맡으므로 리그의 루트 이동이 중복 적용되지 않게 한다.
                _animator.applyRootMotion = false;
            }
        }

        public override void OnStartServer()
        {
            _patrolStartedAt = NetworkTime.time;
            _isDead = _monster.IsDead;
            _monster.Died += HandleDied;
        }

        public override void OnStopServer()
        {
            if (_monster != null)
            {
                _monster.Died -= HandleDied;
            }
        }

        public override void OnStartClient()
        {
            ApplyAnimation();
        }

        private void HandleDied(MonsterBase monster)
        {
            _isDead = true;
        }

        private void OnDeathChanged(bool oldValue, bool newValue)
        {
            if (isClient)
            {
                ApplyAnimation();
            }
        }

        private void ApplyAnimation()
        {
            if (_animator == null || !_animator.isActiveAndEnabled
                || _animator.runtimeAnimatorController == null || _patrolClip == null
                || _patrolClip.length <= 0f)
            {
                return;
            }

            if (_isDead)
            {
                _animator.speed = 0f;
                return;
            }

            float elapsed = (float)Math.Max(0d, NetworkTime.time - _patrolStartedAt);
            _animator.speed = 1f;
            _animator.Play(PatrolHash, 0, elapsed / _patrolClip.length % 1f);
        }
    }
}
