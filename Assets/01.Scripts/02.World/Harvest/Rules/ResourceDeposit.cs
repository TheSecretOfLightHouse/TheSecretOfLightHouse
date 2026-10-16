using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Harvest.Rules
{
    public sealed class ResourceDeposit
    {
        private readonly int[] _resourceIds;
        private readonly int[] _maxAmounts;
        private readonly float[] _regenRates;
        private readonly int[] _remaining;

        public ResourceDeposit(IReadOnlyList<SpotAssignment> assignments)
        {
            int count = assignments != null ? assignments.Count : 0;
            _resourceIds = new int[count];
            _maxAmounts = new int[count];
            _regenRates = new float[count];
            _remaining = new int[count];

            for (int i = 0; i < count; i++)
            {
                SpotAssignment assignment = assignments[i];
                _resourceIds[i] = assignment.ResourceId;
                _maxAmounts[i] = Mathf.Max(0, assignment.MaxAmount);
                _regenRates[i] = assignment.DailyRegenRate;
                _remaining[i] = Mathf.Clamp(assignment.InitialAmount, 0, _maxAmounts[i]);
            }
        }

        public event Action<int, int> RemainingChanged;

        public int Count => _remaining.Length;

        public bool TryGetResourceId(int localIndex, out int resourceId)
        {
            if (!IsAssigned(localIndex))
            {
                resourceId = 0;
                return false;
            }

            resourceId = _resourceIds[localIndex];
            return true;
        }

        public bool TryGetRemaining(int localIndex, out int remaining)
        {
            if (!IsAssigned(localIndex))
            {
                remaining = 0;
                return false;
            }

            remaining = _remaining[localIndex];
            return true;
        }

        public bool TryConsume(int localIndex, int requested, out ConsumeResult result)
        {
            if (!IsAssigned(localIndex))
            {
                result = default;
                return false;
            }

            int granted = requested > 0 ? Mathf.Min(_remaining[localIndex], requested) : 0;
            _remaining[localIndex] -= granted;
            result = new ConsumeResult(_resourceIds[localIndex], granted);

            if (granted > 0)
            {
                RemainingChanged?.Invoke(localIndex, _remaining[localIndex]);
            }

            return granted > 0;
        }

        public int Regenerate(IRegenPolicy policy, int days)
        {
            if (policy == null || days <= 0)
            {
                return 0;
            }

            int changed = 0;
            for (int i = 0; i < _remaining.Length; i++)
            {
                if (_resourceIds[i] == 0)
                {
                    continue;
                }

                int amount = policy.GetRegenAmount(_maxAmounts[i], _regenRates[i], _remaining[i], days);
                if (amount > 0)
                {
                    int next = Mathf.Min(_maxAmounts[i], _remaining[i] + amount);
                    if (next != _remaining[i])
                    {
                        _remaining[i] = next;
                        changed++;
                        RemainingChanged?.Invoke(i, next);
                    }
                }
            }

            return changed;
        }

        private bool IsAssigned(int localIndex)
        {
            return localIndex >= 0 && localIndex < _resourceIds.Length && _resourceIds[localIndex] != 0;
        }
    }
}
