using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    public static class CoastWaveRegistry
    {
        public const int Capacity = 32;
        public const int Segments = 96;
        public const float ReachWidthScale = 2.2f;
        private static readonly Vector4[] BoundsBuffer = new Vector4[Capacity];
        private static readonly Vector4[] EdgeBuffer = new Vector4[Capacity * Segments];
        private static readonly Vector4[] ProfileBuffer = new Vector4[Capacity];
        public static ReadOnlySpan<Vector4> Profiles => ProfileBuffer;
        public static ReadOnlySpan<Vector4> Bounds => BoundsBuffer;
        public static ReadOnlySpan<Vector4> Edges => EdgeBuffer;
        public static int Version { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Array.Clear(BoundsBuffer, 0, BoundsBuffer.Length);
            Array.Clear(EdgeBuffer, 0, EdgeBuffer.Length);
            Array.Clear(ProfileBuffer, 0, ProfileBuffer.Length);
            Version++;
        }

        public static int Register(Vector3[] contour, float width, Vector4 profile = default)
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
            BoundsBuffer[slot] = new Vector4(center.x, center.z, radius + width * ReachWidthScale, width);
            Version++;
            return slot;
        }

        public static void Unregister(int slot)
        {
            if ((uint)slot < Capacity)
            {
                BoundsBuffer[slot] = Vector4.zero;
                Version++;
            }
        }
    }
}
