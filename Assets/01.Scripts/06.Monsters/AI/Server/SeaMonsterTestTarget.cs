using System;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [DisallowMultipleComponent]
    public sealed class SeaMonsterTestTarget : MonoBehaviour
    {
        [Header("시험용 배 상태")]
        [SerializeField] private bool _isInsideIsland;
        [SerializeField, Min(0.01f)] private float _hitRadius = 0.75f;
        [SerializeField, Min(1f)] private float _maxHealth = 100f;

        [Header("시험용 피격 결과")]
        [SerializeField] private float _currentHealth;
        [SerializeField] private int _hitCount;

        public bool IsInsideIsland => _isInsideIsland;
        public bool IsEligible => isActiveAndEnabled && !_isInsideIsland && _currentHealth > 0f
            && MonsterMotion.IsFinite(transform.position);
        public float CurrentHealth => _currentHealth;
        public int HitCount => _hitCount;
        public event Action<float> HealthChanged;

        private void Awake()
        {
            _currentHealth = _maxHealth;
        }

        public bool IntersectsCharge(Vector3 start, Vector3 end, float radius)
        {
            Vector3 point = transform.position;
            Vector3 segment = end - start;
            segment.y = 0f;
            Vector3 offset = point - start;
            offset.y = 0f;
            float t = segment.sqrMagnitude > 0f
                ? Mathf.Clamp01(Vector3.Dot(offset, segment) / segment.sqrMagnitude) : 0f;
            Vector3 nearestOffset = offset - segment * t;
            float combinedRadius = radius + _hitRadius;
            return nearestOffset.sqrMagnitude <= combinedRadius * combinedRadius;
        }

        public bool TryApplyChargeDamage(float damage)
        {            
            if (!NetworkServer.active || !IsEligible || float.IsNaN(damage)
                || float.IsInfinity(damage) || damage <= 0f)
            {
                return false;
            }

            _currentHealth = Mathf.Max(0f, _currentHealth - damage);
            _hitCount++;
            HealthChanged?.Invoke(_currentHealth);
            return true;
        }
    }
}
