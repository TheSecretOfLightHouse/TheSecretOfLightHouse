using Lighthouse.World.MapGeneration.Rules;
using Unity.Burst;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class OpenSeaSwell
    {
        public const int TermCount = 5;

        private const float TwoPi = Mathf.PI * 2f;
        private const float MinWeight = 0.6f;
        private const float MaxWeight = 1f;

        private struct Key
        {
        }

        // Burst jobs read the configured swell through SharedStatic, managed code writes it once per configure.
        private struct Data
        {
            public Vector4 Term0;
            public Vector4 Term1;
            public Vector4 Term2;
            public Vector4 Term3;
            public Vector4 Term4;
            public Vector4 Flow;
        }

        private static readonly SharedStatic<Data> Shared = SharedStatic<Data>.GetOrCreate<Key>();
        private static readonly Vector4[] Terms = new Vector4[TermCount];

        public static float Depth => Shared.Data.Flow.z;

        public static Vector4[] TermArray => Terms;

        public static Vector4 Flow => Shared.Data.Flow;

        public static void Configure(int seed, float depth, float minWavelength, float maxWavelength, float directionDegrees, float spreadDegrees, float driftSpeed)
        {
            var random = new SeededRandom(seed);
            float baseRadians = directionDegrees * Mathf.Deg2Rad;
            float weightSquares = 0f;
            for (int i = 0; i < TermCount; i++)
            {
                float t = (i + random.NextFloat()) / TermCount;
                float wavelength = Mathf.Lerp(minWavelength, maxWavelength, random.NextFloat());
                float angle = baseRadians + (t * 2f - 1f) * spreadDegrees * Mathf.Deg2Rad;
                float k = TwoPi / wavelength;
                float weight = random.NextFloat(MinWeight, MaxWeight);
                float phase = random.NextFloat() * TwoPi;
                Terms[i] = new Vector4(Mathf.Cos(angle) * k, Mathf.Sin(angle) * k, phase, weight);
                weightSquares += weight * weight;
            }

            Shared.Data = new Data
            {
                Term0 = Terms[0],
                Term1 = Terms[1],
                Term2 = Terms[2],
                Term3 = Terms[3],
                Term4 = Terms[4],
                Flow = new Vector4(Mathf.Cos(baseRadians) * driftSpeed, Mathf.Sin(baseRadians) * driftSpeed, Mathf.Clamp(depth, 0f, 0.5f), 1f / Mathf.Sqrt(weightSquares))
            };
        }

        public static void Disable()
        {
            Shared.Data = default;
        }

        public static float FactorAt(float x, float z, float time)
        {
            ref Data data = ref Shared.Data;
            float depth = data.Flow.z;
            if (depth <= 0f)
            {
                return 1f;
            }

            float px = x - data.Flow.x * time;
            float pz = z - data.Flow.y * time;
            float sum = Term(data.Term0, px, pz) + Term(data.Term1, px, pz) + Term(data.Term2, px, pz)
                + Term(data.Term3, px, pz) + Term(data.Term4, px, pz);
            float n = sum * data.Flow.w;
            return 1f + depth * n / Mathf.Sqrt(1f + n * n);
        }

        private static float Term(in Vector4 term, float px, float pz)
        {
            return term.w * Mathf.Sin(term.x * px + term.y * pz + term.z);
        }
    }
}
