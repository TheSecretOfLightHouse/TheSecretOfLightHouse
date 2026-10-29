using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class CoastWaveRegistry
    {
        public const int Capacity = 32;
        public const int Segments = 96;
        public const float ReachWidthScale = 2.2f;
        public const float DefaultLagoonWidthScale = 11f / 7f;
        public const float LagoonCoverageMargin = 0.9f;
        private static readonly Vector4[] BoundsBuffer = new Vector4[Capacity];
        private static readonly Vector4[] EdgeBuffer = new Vector4[Capacity * Segments];
        private static readonly Vector4[] ProfileBuffer = new Vector4[Capacity];
        private static readonly Vector4[] LagoonBuffer = new Vector4[Capacity];
        private static readonly float[] DesiredLagoon = new float[Capacity];
        private static readonly float[] RawRadius = new float[Capacity];
        public static ReadOnlySpan<Vector4> Profiles => ProfileBuffer;
        public static ReadOnlySpan<Vector4> Bounds => BoundsBuffer;
        public static ReadOnlySpan<Vector4> Lagoons => LagoonBuffer;
        public static ReadOnlySpan<Vector4> Edges => EdgeBuffer;
        public static int Version { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Array.Clear(BoundsBuffer, 0, BoundsBuffer.Length);
            Array.Clear(EdgeBuffer, 0, EdgeBuffer.Length);
            Array.Clear(ProfileBuffer, 0, ProfileBuffer.Length);
            Array.Clear(LagoonBuffer, 0, LagoonBuffer.Length);
            Array.Clear(DesiredLagoon, 0, DesiredLagoon.Length);
            Array.Clear(RawRadius, 0, RawRadius.Length);
            Version++;
        }

        public static int Register(Vector3[] contour, float width, Vector4 profile = default, float lagoonReach = 0f, float waveReach = 0f, float shoreLift = 0f)
        {
            if (contour.Length != Segments || width <= 0f)
            {
                return -1;
            }
            int slot = -1;
            for (int i = 0; i < Capacity; i++)
            {
                if (BoundsBuffer[i].w == 0f)
                {
                    slot = i;
                    break;
                }
            }
            if (slot < 0)
            {
                return -1;
            }
            Vector3 center = Vector3.zero;
            for (int i = 0; i < Segments; i++)
            {
                center += contour[i];
            }
            center /= Segments;
            float radius = 0f;
            for (int i = 0; i < Segments; i++)
            {
                Vector3 a = contour[i];
                Vector3 b = contour[(i + 1) % Segments];
                EdgeBuffer[slot * Segments + i] = new Vector4(a.x, a.z, b.x, b.z);
                radius = Mathf.Max(radius, Vector2.Distance(new Vector2(a.x, a.z), new Vector2(center.x, center.z)));
            }
            ProfileBuffer[slot] = profile;
            BoundsBuffer[slot] = new Vector4(center.x, center.z, radius + Mathf.Max(width * ReachWidthScale, waveReach), width);
            RawRadius[slot] = radius;
            float desired = lagoonReach > 0f ? lagoonReach : width * DefaultLagoonWidthScale;
            DesiredLagoon[slot] = Mathf.Min(desired, width * ReachWidthScale * LagoonCoverageMargin);
            LagoonBuffer[slot].y = Mathf.Max(0f, shoreLift);
            RefreshLagoons();
            Version++;
            return slot;
        }

        public static void Unregister(int slot)
        {
            if ((uint)slot < Capacity)
            {
                BoundsBuffer[slot] = Vector4.zero;
                RefreshLagoons();
                Version++;
            }
        }

        private static void RefreshLagoons()
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (BoundsBuffer[i].w == 0f)
                {
                    LagoonBuffer[i] = Vector4.zero;
                    continue;
                }
                float reach = DesiredLagoon[i];
                for (int j = 0; j < Capacity; j++)
                {
                    if (j == i || BoundsBuffer[j].w == 0f)
                    {
                        continue;
                    }
                    reach = CoastLagoonModel.LimitByNeighbour(
                        reach,
                        new Vector2(BoundsBuffer[i].x, BoundsBuffer[i].y), RawRadius[i],
                        new ReadOnlySpan<Vector4>(EdgeBuffer, i * Segments, Segments),
                        new Vector2(BoundsBuffer[j].x, BoundsBuffer[j].y), RawRadius[j],
                        new ReadOnlySpan<Vector4>(EdgeBuffer, j * Segments, Segments));
                }
                LagoonBuffer[i] = new Vector4(reach, LagoonBuffer[i].y, 0f, 0f);
            }
        }
    }
}
