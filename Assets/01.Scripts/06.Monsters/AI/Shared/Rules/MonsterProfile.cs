using UnityEngine;

namespace Lighthouse.Monsters.AI.Rules
{
    [CreateAssetMenu(fileName = "MonsterProfile", menuName = "TheLightHouse/Monster/MonsterProfile")]
    public class MonsterProfile : ScriptableObject
    {
        [Header("체력")]
        [SerializeField, Min(1)] private float _maxHealth = 1f;

        [Header("이동")]
        [SerializeField, Min(0)] private float _patrolSpeed;
        [SerializeField, Min(0)] private float _chaseSpeed;

        [Header("감지 및 추적")]
        [SerializeField, Min(0)] private float _detectionRadius;
        [SerializeField, Min(0)] private float _maxChaseDistance;

        [Header("공격")]
        [SerializeField, Min(0)] private float _attackDamage;
        [SerializeField, Min(0)] private float _attackCooldown;

        public float MaxHealth => _maxHealth;
        public float PatrolSpeed => _patrolSpeed;
        public float ChaseSpeed => _chaseSpeed;
        public float DetectionRadius => _detectionRadius;
        public float MaxChaseDistance => _maxChaseDistance;
        public float AttackDamage => _attackDamage;
        public float AttackCooldown => _attackCooldown;

        protected virtual void OnValidate()
        {
            _maxHealth = Mathf.Max(1f, _maxHealth);
            _patrolSpeed = Mathf.Max(0f, _patrolSpeed);
            _chaseSpeed = Mathf.Max(0f, _chaseSpeed);
            _detectionRadius = Mathf.Max(0f, _detectionRadius);
            _maxChaseDistance = Mathf.Max(_detectionRadius, _maxChaseDistance);
            _attackDamage = Mathf.Max(0f, _attackDamage);
            _attackCooldown = Mathf.Max(0f, _attackCooldown);
        }
    }
}

