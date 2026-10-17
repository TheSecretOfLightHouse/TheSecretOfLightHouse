using UnityEngine;

namespace Lighthouse.Monsters.Boss
{
    [CreateAssetMenu(fileName = "BossSettings", menuName = "Lighthouse/Monsters/Boss Settings")]
    public sealed class BossSettings : ScriptableObject
    {
        [Header("Temporary test balance")]
        [SerializeField, Min(1)] private float _testLighthouseMaxHealth = 1000;
        [SerializeField, Min(0.1f)] private float _attackInterval = 3;
        [SerializeField, Min(0.1f)] private float _specialEndingAttackThreshold = 100;

        public float TestLighthouseMaxHealth => _testLighthouseMaxHealth;
        public float AttackInterval => _attackInterval;
        public float SpecialEndingAttackThreshold => _specialEndingAttackThreshold;
    }
}
