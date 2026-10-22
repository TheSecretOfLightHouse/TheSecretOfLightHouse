using UnityEngine;

namespace Lighthouse.Monsters.Boss
{
    [CreateAssetMenu(fileName = "BossSettings", menuName = "TheLightHouse/Monster/Boss Settings")]
    public sealed class BossSettings : ScriptableObject
    {
        [Header("Temporary test balance")]
        [SerializeField, Min(1)] private float _testLighthouseMaxHealth = 1000;
        [SerializeField, Min(0.1f)] private float _attackInterval = 3;
        [SerializeField, Min(0.1f)] private float _specialEndingAttackThreshold = 100;

        [Header("Tentacle timing")]
        [SerializeField, Min(0.1f)] private float _wrapDuration = 9;
        [SerializeField, Min(0.1f)] private float _squeezeDuration = 2;
        [SerializeField, Range(0.01f, 1f)] private float _damageNormalizedTime = 0.5f;

        public float TestLighthouseMaxHealth => _testLighthouseMaxHealth;
        public float AttackInterval => _attackInterval;
        public float SpecialEndingAttackThreshold => _specialEndingAttackThreshold;
        public float WrapDuration => _wrapDuration;
        public float SqueezeDuration => _squeezeDuration;
        public float DamageNormalizedTime => _damageNormalizedTime;
    }
}
