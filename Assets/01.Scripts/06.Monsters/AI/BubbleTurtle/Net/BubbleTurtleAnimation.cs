using Lighthouse.Monsters.AI.Server;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Net
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster), typeof(MonsterMotion))]
    public sealed class BubbleTurtleAnimation : NetworkBehaviour
    {
        private static readonly int IsSwimmingHash = Animator.StringToHash("IsSwimming");

        [SerializeField] private Animator _animator;

        [SyncVar] private bool _isSwimming;
        [SyncVar] private bool _isDead;

        private SeaMonster _monster;
        private MonsterMotion _motion;

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            _motion = GetComponent<MonsterMotion>();
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
                _isDead = _monster.IsDead;
                _isSwimming = !_isDead && _motion.IsMoving;
            }

            if (isClient && _animator != null && _animator.isActiveAndEnabled
                && _animator.runtimeAnimatorController != null)
            {
                _animator.speed = _isDead ? 0f : 1f;
                _animator.SetBool(IsSwimmingHash, _isSwimming);
            }
        }
    }
}
