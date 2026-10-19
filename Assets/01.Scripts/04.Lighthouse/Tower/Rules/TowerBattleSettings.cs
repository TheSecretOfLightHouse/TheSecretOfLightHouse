using Lighthouse.Workbench.Rules;
using UnityEngine;

namespace Lighthouse.Tower.Rules
{
    // Ritual, summon health and final-battle relight values. Ritual length and the health formula follow the
    // win/vote decisions (sections 3~5); relighting the lantern room with abyss crystals is the 2026-10-19 trial.
    [CreateAssetMenu(fileName = "TowerBattleSettings", menuName = "TheLightHouse/Lighthouse/TowerBattleSettings")]
    public sealed class TowerBattleSettings : ScriptableObject
    {
        [Header("Ritual")]
        [SerializeField, Min(1f)]
        private float _ritualSeconds = 60f;
        [Tooltip("Citizens who must point at the mafia when the ritual ends.")]
        [SerializeField, Min(1)]
        private int _ritualRequiredVotes = 2;

        [Header("Health at summon: base + (max - base) x repair ratio")]
        [SerializeField, Min(1f)]
        private float _baseHealth = 300f;
        [SerializeField, Min(1f)]
        private float _maxHealth = 1000f;
        [Tooltip("A summoned attack power at or above this ends the match for the mafia at once.")]
        [SerializeField, Min(0.1f)]
        private float _specialEndingAttack = 100f;

        [Header("Relighting the lantern room")]
        [SerializeField, Min(1)]
        private int _ignitionsToWin = 4;
        [SerializeField, Min(0)]
        private int _crystalsPerIgnition = 1;
        [Tooltip("Base resources taken from the igniting player's inventory on top of the crystals.")]
        [SerializeField]
        private RecipeIngredient[] _ignitionCost = new RecipeIngredient[0];
        [SerializeField, Min(1f)]
        private float _crystalRespawnSeconds = 20f;

        [Tooltip("On while the numbers are play-test values.")]
        [SerializeField]
        private bool _isTestValue = true;

        public float RitualSeconds => _ritualSeconds;
        public int RitualRequiredVotes => _ritualRequiredVotes;
        public float BaseHealth => _baseHealth;
        public float MaxHealth => _maxHealth;
        public float SpecialEndingAttack => _specialEndingAttack;
        public int IgnitionsToWin => _ignitionsToWin;
        public int CrystalsPerIgnition => _crystalsPerIgnition;
        public int IgnitionCostCount => _ignitionCost != null ? _ignitionCost.Length : 0;
        public float CrystalRespawnSeconds => _crystalRespawnSeconds;
        public bool IsTestValue => _isTestValue;

        public RecipeIngredient GetIgnitionCost(int index)
        {
            return _ignitionCost[index];
        }

        public float GetSummonHealth(float repairRatio)
        {
            return _baseHealth + (_maxHealth - _baseHealth) * Mathf.Clamp01(repairRatio);
        }

        // Returns the first problem, or null.
        public string Validate()
        {
            if (_maxHealth < _baseHealth)
            {
                return $"{name}: max health is below base health.";
            }

            for (int i = 0; i < IgnitionCostCount; i++)
            {
                if (_ignitionCost[i].ResourceId == 0 || _ignitionCost[i].Amount <= 0)
                {
                    return $"{name}: ignition cost {i} is empty.";
                }
            }

            return null;
        }
    }
}
