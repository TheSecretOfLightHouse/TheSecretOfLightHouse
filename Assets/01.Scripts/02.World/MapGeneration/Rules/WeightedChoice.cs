using System.Collections.Generic;

namespace Lighthouse.World.MapGeneration.Rules
{
    public static class WeightedChoice
    {
        public static int Pick(SeededRandom random, IReadOnlyList<float> weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] > 0f)
                {
                    total += weights[i];
                }
            }

            if (total <= 0f)
            {
                return -1;
            }

            float target = random.NextFloat() * total;
            float accumulated = 0f;
            int last = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0f)
                {
                    continue;
                }

                accumulated += weights[i];
                last = i;
                if (target < accumulated)
                {
                    return i;
                }
            }

            return last;
        }
    }
}
