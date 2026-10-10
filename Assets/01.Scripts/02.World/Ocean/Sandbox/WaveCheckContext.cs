using System;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public sealed class WaveCheckContext
    {
        public WaveCheckContext(
            WaveParams[] waves,
            WaveModifierData[] modifiers,
            WaveGrid grid,
            float time,
            float inverseTolerance,
            float modifierRadius,
            float modifierStrength)
        {
            Waves = waves ?? throw new ArgumentNullException(nameof(waves));
            Modifiers = modifiers ?? throw new ArgumentNullException(nameof(modifiers));
            Grid = grid;
            Time = time;
            InverseTolerance = inverseTolerance;
            ModifierRadius = modifierRadius;
            ModifierStrength = modifierStrength;

            Center = new Vector2(grid.Origin.x, grid.Origin.z);
            BumpOnly = new[] { new WaveModifierData(WaveModifierKind.Bump, Center, modifierRadius, modifierStrength, 0f) };
            VortexOnly = new[] { new WaveModifierData(WaveModifierKind.Vortex, Center, modifierRadius, modifierStrength, 0f) };

            SamplePoints = new Vector2[grid.VertexCount];
            for (int i = 0; i < SamplePoints.Length; i++)
            {
                SamplePoints[i] = grid.GetWorldPoint(i);
            }
        }

        public WaveParams[] Waves { get; }

        public WaveModifierData[] Modifiers { get; }

        public WaveModifierData[] BumpOnly { get; }

        public WaveModifierData[] VortexOnly { get; }

        public WaveGrid Grid { get; }

        public Vector2[] SamplePoints { get; }

        public Vector2 Center { get; }

        public float Time { get; }

        public float InverseTolerance { get; }

        public float ModifierRadius { get; }

        public float ModifierStrength { get; }
    }
}
