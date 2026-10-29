using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Harvest.Rules
{
    [CreateAssetMenu(fileName = "WorldDepositBudget", menuName = "TheLightHouse/World/WorldDepositBudget")]
    public class WorldDepositBudget : ScriptableObject
    {
        [SerializeField] private ResourceBudgetEntry[] _entries;

        public IReadOnlyList<ResourceBudgetEntry> Entries => _entries ?? Array.Empty<ResourceBudgetEntry>();

        public void CollectTotals(Dictionary<int, int> results)
        {
            results.Clear();
            IReadOnlyList<ResourceBudgetEntry> entries = Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                ResourceBudgetEntry entry = entries[i];
                if (entry == null || entry.Resource == null || entry.TotalAmount <= 0 || results.ContainsKey(entry.Resource.Id))
                {
                    continue;
                }

                results.Add(entry.Resource.Id, entry.TotalAmount);
            }
        }
    }
}
