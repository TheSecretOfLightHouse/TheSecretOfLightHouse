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
        [SerializeField] private Animation _attackFx;
        [SerializeField] private Transform _fxBubble;
        [SerializeField] private Transform _firePoint;
        [SerializeField] private ParticleSystem _chargeBubbleEffect;
        [SerializeField, Min(0f)] private float _fxReleaseTime = 2.4791667f;

        [SyncVar] private bool _isSwimming;
        [SyncVar] private bool _isDead;

        private SeaMonster _monster;
        private MonsterMotion _motion;
        private BubbleTurtleAttack _attack;
        private uint _renderedAttackSequence;
        private bool _wasAttacking;
        private AnimationState _fxState;

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

            if (_attackFx != null && _attackFx.clip != null)
            {
                _fxState = _attackFx[_attackFx.clip.name];
                _fxState.speed = 0f;
                _attackFx.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            _renderedAttackSequence = 0;
            _wasAttacking = false;
            if (_attackFx != null)
            {
                _attackFx.gameObject.SetActive(false);
            }
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

            UpdateAttackFx();
        }

        private void UpdateAttackFx()
        {
            if (_attackFx == null || _fxState == null)
            {
                return;
            }

            float elapsed = _attack != null ? (float)System.Math.Max(0d, NetworkTime.time - _attack.AttackStartedAt) : 0f;
            bool visible = isClient && !_isDead && _attack != null && _attack.IsAttacking
                && !_attack.HasFired && elapsed < _attack.PreparationDuration;
            if (_attackFx.gameObject.activeSelf != visible)
            {
                _attackFx.gameObject.SetActive(visible);
                if (visible)
                {
                    _attackFx.Play();
                    BubbleProjectile.SynchronizeVisual(_chargeBubbleEffect, _attack.AttackStartedAt);
                }
            }

            if (!visible)
            {
                return;
            }

            // 서버 공격 시각을 기준으로 샘플링해 늦게 관측해도 응축 진행도를 맞춘다.
            _fxState.time = Mathf.Clamp01(elapsed / _attack.PreparationDuration) * _fxReleaseTime;
            _attackFx.Sample();
            if (_fxBubble != null && _firePoint != null)
            {
                // 원본의 직선 발사 대신 실제 발사점에서 유도 투사체로 이어진다.
                _attackFx.transform.position += _firePoint.position - _fxBubble.position;
            }
        }
    }
}
