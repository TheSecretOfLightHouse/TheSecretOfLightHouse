using Unity.Collections;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    // Incompressible potential flow around land: two unit-drive solutions (x and z) that a drive vector combines linearly.
    public static class WaterCurrentSolver
    {
        private const int MaxIterations = 4000;
        private const float Tolerance = 2e-3f;
        private const float MaxBasisSpeed = 2.5f;
        private const float DepthFadeStart = 0.1f;
        private const float DepthFadeEnd = 1.5f;
        private const float WaterFraction = 0.5f;
        private const int BasisCount = 2;

        public static WaterCurrentField Bake(NativeArray<Vector4> geometry, int geometrySize, Vector2 origin, float geometryCell, float coarseCell, float maxSpeed)
        {
            int factor = Mathf.Max(1, Mathf.RoundToInt(coarseCell / geometryCell));
            int size = geometrySize / factor;
            float cell = geometryCell * factor;
            if (size < 3)
            {
                return null;
            }

            bool[] isWater = new bool[size * size];
            float[] depth = new float[size * size];
            Downsample(geometry, geometrySize, factor, size, isWater, depth);
            bool[] isFlow = FloodFromBorder(isWater, size);

            Vector2[][] velocity = new Vector2[BasisCount][];
            for (int basis = 0; basis < BasisCount; basis++)
            {
                float[] potential = Solve(isFlow, size, cell, basis);
                velocity[basis] = Differentiate(potential, isFlow, size, cell);
            }

            Vector4[] result = new Vector4[size * size];
            for (int i = 0; i < result.Length; i++)
            {
                if (!isFlow[i])
                {
                    continue;
                }

                float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(DepthFadeStart, DepthFadeEnd, depth[i]));
                Vector2 x = Clamp(velocity[0][i]) * fade;
                Vector2 z = Clamp(velocity[1][i]) * fade;
                result[i] = new Vector4(x.x, x.y, z.x, z.y);
            }

            return new WaterCurrentField(result, size, origin, cell, maxSpeed);
        }

        private static Vector2 Clamp(Vector2 value)
        {
            return Vector2.ClampMagnitude(value, MaxBasisSpeed);
        }

        private static void Downsample(NativeArray<Vector4> geometry, int geometrySize, int factor, int size, bool[] isWater, float[] depth)
        {
            float weight = 1f / (factor * factor);
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    float water = 0f;
                    float depthSum = 0f;
                    for (int dz = 0; dz < factor; dz++)
                    {
                        for (int dx = 0; dx < factor; dx++)
                        {
                            Vector4 texel = geometry[(z * factor + dz) * geometrySize + x * factor + dx];
                            if (texel.y > 0f && texel.w > 0f)
                            {
                                water += weight;
                                depthSum += texel.x * weight;
                            }
                        }
                    }

                    int index = z * size + x;
                    isWater[index] = water >= WaterFraction;
                    depth[index] = water > 0f ? depthSum / water : 0f;
                }
            }
        }

        private static bool[] FloodFromBorder(bool[] isWater, int size)
        {
            bool[] reached = new bool[isWater.Length];
            int[] queue = new int[isWater.Length];
            int head = 0;
            int tail = 0;
            for (int i = 0; i < size; i++)
            {
                Enqueue(i, isWater, reached, queue, ref tail);
                Enqueue((size - 1) * size + i, isWater, reached, queue, ref tail);
                Enqueue(i * size, isWater, reached, queue, ref tail);
                Enqueue(i * size + size - 1, isWater, reached, queue, ref tail);
            }

            while (head < tail)
            {
                int index = queue[head++];
                int x = index % size;
                int z = index / size;
                if (x > 0)
                {
                    Enqueue(index - 1, isWater, reached, queue, ref tail);
                }
                if (x < size - 1)
                {
                    Enqueue(index + 1, isWater, reached, queue, ref tail);
                }
                if (z > 0)
                {
                    Enqueue(index - size, isWater, reached, queue, ref tail);
                }
                if (z < size - 1)
                {
                    Enqueue(index + size, isWater, reached, queue, ref tail);
                }
            }

            return reached;
        }

        private static void Enqueue(int index, bool[] isWater, bool[] reached, int[] queue, ref int tail)
        {
            if (isWater[index] && !reached[index])
            {
                reached[index] = true;
                queue[tail++] = index;
            }
        }

        private static float[] Solve(bool[] isFlow, int size, float cell, int basis)
        {
            float[] potential = new float[size * size];
            float center = (size - 1) * 0.5f;
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    potential[z * size + x] = ((basis == 0 ? x : z) - center) * cell;
                }
            }

            float relaxation = 2f / (1f + Mathf.Sin(Mathf.PI / size));
            for (int iteration = 0; iteration < MaxIterations; iteration++)
            {
                float maxDelta = 0f;
                for (int color = 0; color < 2; color++)
                {
                    for (int z = 1; z < size - 1; z++)
                    {
                        for (int x = 1 + ((z + color) & 1); x < size - 1; x += 2)
                        {
                            int index = z * size + x;
                            if (!isFlow[index])
                            {
                                continue;
                            }

                            float sum = 0f;
                            int count = 0;
                            if (isFlow[index - 1])
                            {
                                sum += potential[index - 1];
                                count++;
                            }
                            if (isFlow[index + 1])
                            {
                                sum += potential[index + 1];
                                count++;
                            }
                            if (isFlow[index - size])
                            {
                                sum += potential[index - size];
                                count++;
                            }
                            if (isFlow[index + size])
                            {
                                sum += potential[index + size];
                                count++;
                            }
                            if (count == 0)
                            {
                                continue;
                            }

                            float delta = relaxation * (sum / count - potential[index]);
                            potential[index] += delta;
                            maxDelta = Mathf.Max(maxDelta, Mathf.Abs(delta));
                        }
                    }
                }

                if (maxDelta < Tolerance)
                {
                    break;
                }
            }

            return potential;
        }

        private static Vector2[] Differentiate(float[] potential, bool[] isFlow, int size, float cell)
        {
            Vector2[] velocity = new Vector2[size * size];
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    int index = z * size + x;
                    if (!isFlow[index])
                    {
                        continue;
                    }

                    bool hasLeft = x > 0 && isFlow[index - 1];
                    bool hasRight = x < size - 1 && isFlow[index + 1];
                    bool hasDown = z > 0 && isFlow[index - size];
                    bool hasUp = z < size - 1 && isFlow[index + size];
                    velocity[index] = new Vector2(
                        Gradient(hasLeft ? potential[index - 1] : potential[index], hasRight ? potential[index + 1] : potential[index], hasLeft, hasRight, cell),
                        Gradient(hasDown ? potential[index - size] : potential[index], hasUp ? potential[index + size] : potential[index], hasDown, hasUp, cell));
                }
            }

            return velocity;
        }

        private static float Gradient(float lower, float upper, bool hasLower, bool hasUpper, float cell)
        {
            if (hasLower && hasUpper)
            {
                return (upper - lower) / (2f * cell);
            }

            return hasLower || hasUpper ? (upper - lower) / cell : 0f;
        }
    }
}
