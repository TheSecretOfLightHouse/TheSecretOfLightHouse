using UnityEngine;

namespace Lighthouse.Tower.Rules
{
    // Ritual and final-battle values. Ritual length and votes follow the win/vote decisions (sections 3·4); the final
    // battle follows the JH04 repair-carryover proposal (2026-10-22): the summon keeps the repair and starts every
    // lighthouse at the same fixed health, whatever the repair reached.
    [CreateAssetMenu(fileName = "TowerBattleSettings", menuName = "TheLightHouse/Lighthouse/TowerBattleSettings")]
    public sealed class TowerBattleSettings : ScriptableObject
    {
        [Header("Ritual")]
        [SerializeField, Min(1f)]
        private float _ritualSeconds = 60f;
        [Tooltip("Citizens who must point at the mafia when the ritual ends.")]
        [SerializeField, Min(1)]
        private int _ritualRequiredVotes = 2;

        [Header("Final battle")]
        [Tooltip("Starting and maximum health after a summon. Installs never heal it or raise it.")]
        [SerializeField, Min(1f)]
        private float _maxHealth = 1000f;

        [Tooltip("On while the numbers are play-test values.")]
        [SerializeField]
        private bool _isTestValue = true;

        public float RitualSeconds => _ritualSeconds;
        public int RitualRequiredVotes => _ritualRequiredVotes;
        public float MaxHealth => _maxHealth;
        public bool IsTestValue => _isTestValue;

        // Returns the first problem, or null.
        public string Validate()
        {
            return _maxHealth >= 1f ? null : $"{name}: max health is below 1.";
        }
    }
}
