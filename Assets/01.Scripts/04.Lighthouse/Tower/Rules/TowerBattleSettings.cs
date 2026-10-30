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

        [Tooltip("Server-owned summon presentation window. Temporary until the Timeline length is set.")]
        [SerializeField, Min(0.1f)]
        private float _summonCinematicSeconds = 10f;

        [SerializeField] private string _summonCinematicScene = "Assets/00.Scenes/TaeWook/SummonCinematic.unity";
        [SerializeField, Min(0.1f)] private float _cinematicLoadTimeout = 10f;
        [SerializeField, Min(0.1f)] private float _cinematicReturnTimeout = 5f;
        [SerializeField, Min(0.1f)] private float _cinematicStartLead = 0.5f;
        [SerializeField, Min(0.1f)] private float _cinematicReturnGrace = 0.5f;

        [Tooltip("On while the numbers are play-test values.")]
        [SerializeField]
        private bool _isTestValue = true;

        public float RitualSeconds => _ritualSeconds;
        public int RitualRequiredVotes => _ritualRequiredVotes;
        public float MaxHealth => _maxHealth;
        public float SummonCinematicSeconds => _summonCinematicSeconds;
        public string SummonCinematicScene => _summonCinematicScene;
        public float CinematicLoadTimeout => _cinematicLoadTimeout;
        public float CinematicReturnTimeout => _cinematicReturnTimeout;
        public float CinematicStartLead => _cinematicStartLead;
        public float CinematicReturnGrace => _cinematicReturnGrace;
        public bool IsTestValue => _isTestValue;

        // Returns the first problem, or null.
        public string Validate()
        {
            foreach (float duration in new[] { _cinematicLoadTimeout, _cinematicReturnTimeout, _cinematicStartLead, _cinematicReturnGrace })
            {
                if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0f)
                {
                    return $"{name}: cinematic timeouts must be positive and finite.";
                }
            }
            if (float.IsNaN(_summonCinematicSeconds) || float.IsInfinity(_summonCinematicSeconds)
                || _summonCinematicSeconds <= 0f)
            {
                return $"{name}: summon cinematic duration must be positive and finite.";
            }
            return _maxHealth >= 1f ? null : $"{name}: max health is below 1.";
        }
    }
}
