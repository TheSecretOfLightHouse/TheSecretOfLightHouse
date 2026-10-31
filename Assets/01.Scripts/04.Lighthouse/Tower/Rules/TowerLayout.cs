using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.Tower.Rules
{
    // Floors and module slots of the lighthouse, plus the repair percents where rewards start.
    // Repair is counted per installed module and floors fill bottom-up (2026-10-19 trial, not yet in the decision
    // doc), so the layout is data and can be swapped while the trial runs.
    [CreateAssetMenu(fileName = "TowerLayout", menuName = "TheLightHouse/Lighthouse/TowerLayout")]
    public sealed class TowerLayout : ScriptableObject
    {
        [Tooltip("Display name per floor, bottom first. Slot floors index into this.")]
        [SerializeField]
        private string[] _floorNames = new string[0];
        [Tooltip("Module slots ordered bottom floor first. A floor opens only once every slot below it is filled.")]
        [SerializeField]
        private TowerSlot[] _slots = new TowerSlot[0];
        [Tooltip("Repair percent at which each reward tier starts, ascending, below 100. Reaching 100% opens the ritual instead.")]
        [SerializeField]
        private int[] _rewardPercents = { 25, 50, 75 };
        [Tooltip("What each reward tier gives, in the same order as the percents (game design v4 6.1). Shown by the"
            + " reward window and the unlock notice.")]
        [SerializeField]
        private string[] _rewardNames = { "배 이동 속도 상승", "자원 파밍량 증가", "배 대포 피해 상승" };
        [Tooltip("On while the layout is a play-test trial, so UI and docs do not present it as final.")]
        [SerializeField]
        private bool _isTestValue = true;

        public int FloorCount => _floorNames != null ? _floorNames.Length : 0;
        public int SlotCount => _slots != null ? _slots.Length : 0;
        public int RewardTierCount => _rewardPercents != null ? _rewardPercents.Length : 0;
        public bool IsTestValue => _isTestValue;

        public TowerSlot this[int index] => _slots[index];

        public string GetFloorName(int floor)
        {
            return floor >= 0 && floor < FloorCount ? _floorNames[floor] : string.Empty;
        }

        public int GetRewardPercent(int tier)
        {
            return _rewardPercents[tier];
        }

        public string GetRewardName(int tier)
        {
            return _rewardNames != null && tier >= 0 && tier < _rewardNames.Length && !string.IsNullOrEmpty(_rewardNames[tier])
                ? _rewardNames[tier] : $"보상 {tier + 1}";
        }

        public float GetRepairRatio(int installedCount)
        {
            return SlotCount > 0 ? Mathf.Clamp01((float)installedCount / SlotCount) : 0f;
        }

        // Tiers reached so far. Compared in whole numbers so 2 of 8 slots counts as exactly 25%.
        public int GetRewardTier(int installedCount)
        {
            int tier = 0;

            for (int i = 0; i < RewardTierCount; i++)
            {
                if (installedCount * 100 >= _rewardPercents[i] * SlotCount)
                {
                    tier = i + 1;
                }
            }

            return tier;
        }

        // Every slot below this floor is filled. Slots are ordered by floor, so the scan stops at the floor itself.
        public bool IsFloorOpen(int floor, IReadOnlyList<bool> installed)
        {
            for (int i = 0; i < SlotCount && i < installed.Count; i++)
            {
                if (_slots[i].Floor >= floor)
                {
                    break;
                }

                if (!installed[i])
                {
                    return false;
                }
            }

            return true;
        }

        // Returns the first problem, or null.
        public string Validate()
        {
            if (FloorCount == 0 || SlotCount == 0)
            {
                return $"{name}: needs floors and slots.";
            }

            for (int i = 0; i < SlotCount; i++)
            {
                TowerSlot slot = _slots[i];

                if (!slot.Recipe || slot.RecipeId == 0)
                {
                    return $"{name}: slot {i} has no recipe.";
                }

                if (slot.Floor >= FloorCount)
                {
                    return $"{name}: slot {i} is on floor {slot.Floor}, but there are {FloorCount} floors.";
                }

                if (i > 0 && slot.Floor < _slots[i - 1].Floor)
                {
                    return $"{name}: slots must be ordered bottom floor first (slot {i}).";
                }
            }

            for (int i = 0; i < RewardTierCount; i++)
            {
                int percent = _rewardPercents[i];

                if (percent <= 0 || percent >= 100 || (i > 0 && percent <= _rewardPercents[i - 1]))
                {
                    return $"{name}: reward percents must rise between 1 and 99.";
                }
            }

            return null;
        }
    }
}
