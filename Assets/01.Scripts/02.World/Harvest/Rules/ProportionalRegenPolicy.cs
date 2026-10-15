using UnityEngine;

namespace Lighthouse.World.Harvest.Rules
{
    public sealed class ProportionalRegenPolicy : IRegenPolicy
    {
        public int GetRegenAmount(int maxAmount, float dailyRegenRate, int current, int days)
        {
            if (days <= 0 || maxAmount <= 0 || dailyRegenRate <= 0f || current >= maxAmount)
            {
                return 0;
            }

            long perDay = Mathf.RoundToInt(maxAmount * Mathf.Clamp01(dailyRegenRate));
            long total = perDay * days;
            long room = maxAmount - current;

            return (int)System.Math.Min(total, room);
        }
    }
}
