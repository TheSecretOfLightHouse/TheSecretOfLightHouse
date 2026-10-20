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
        private static readonly int AttackHash = Animator.StringToHash("Base Layer.Attack");
        private static readonly int IdleHash = Animator.StringToHash("Base Layer.Idle");
        private static readonly int SwimHash = Animator.StringToHash("Base Layer.Swim");

        [SerializeField] private Animator _animator;

        [SyncVar] private bool _isSwimming;
        [SyncVar] private bool _isDead;

        private SeaMonster _monster;
        private MonsterMotion _motion;
        private BubbleTurtleAttack _attack;
        private uint _renderedAttackSequence;
        private bool _wasAttacking;

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            _motion = GetComponent<MonsterMotion>();
            _attack = GetComponent<BubbleTurtleAttack>();

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>(true);
            }

            if (_animator != null)
            {
                _animator.applyRootMotion = false;
            }
        }

        private void OnDisable()
        {
            _renderedAttackSequence = 0;
            _wasAttacking = false;
        }

        private void LateUpdate()
        {
            if (isServer)
            {
                _isDead = _monster.IsDead;
                _isSwimming = !_isDead && _motion.IsMoving && (_attack == null || !_attack.IsAttacking);
            }

            if (isClient && _animator != null && _animator.isActiveAndEnabled && _animator.runtimeAnimatorController != null)
            {
                bool attacking = _attack != null && _attack.IsAttacking;
                _animator.speed = _isDead ? 0f : attacking ? _attack.ActivePlaybackSpeed : 1f;
                _animator.SetBool(IsSwimmingHash, _isSwimming);

                if (!_isDead && attacking && (!_wasAttacking || _renderedAttackSequence != _attack.AttackSequence))
                {
                    float elapsed = (float)System.Math.Max(0d, NetworkTime.time - _attack.AttackStartedAt);
                    _animator.PlayInFixedTime(AttackHash, 0, elapsed * _attack.ActivePlaybackSpeed);
                    _renderedAttackSequence = _attack.AttackSequence;
                }
                else if (!_isDead && !attacking && _wasAttacking)
                {
                    _animator.CrossFadeInFixedTime(_isSwimming ? SwimHash : IdleHash, 0.1f);
                }

                _wasAttacking = attacking;
            }
        }
    }
}
