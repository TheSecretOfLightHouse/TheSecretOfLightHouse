using System.Collections.Generic;
using Lighthouse.World.Islands.Rules;
using UnityEngine;

namespace Lighthouse.World.Weather.View
{
    public sealed class WorldIslandAudioZone : MonoBehaviour
    {
        private static readonly List<WorldIslandAudioZone> Zones = new List<WorldIslandAudioZone>();
        private IslandArchetype _archetype;
        private short _index;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetZones()
        {
            Zones.Clear();
        }

        private void OnEnable()
        {
            Zones.Add(this);
        }

        private void OnDisable()
        {
            Zones.Remove(this);
        }

        public void Initialize(IslandArchetype archetype, short index)
        {
            _archetype = archetype;
            _index = index;
        }

        public static float GetNearest(Vector3 position, float fadeDistance, float inlandRatio,
            out IslandArchetype archetype, out short index)
        {
            float best = 0f;
            archetype = null;
            index = -1;
            for (int i = 0; i < Zones.Count; i++)
            {
                WorldIslandAudioZone zone = Zones[i];
                if (zone == null || zone._archetype == null)
                {
                    continue;
                }
                Vector3 delta = position - zone.transform.position;
                float radius = Mathf.Max(1f, zone._archetype.Radius);
                float distance = new Vector2(delta.x, delta.z).magnitude;
                // Peaks at the coastline (radius), fades toward the island centre and out to sea.
                float proximity = distance >= radius
                    ? 1f - (distance - radius) / Mathf.Max(1f, fadeDistance)
                    : Mathf.InverseLerp(radius * Mathf.Clamp01(inlandRatio), radius, distance);
                proximity = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(proximity));
                if (archetype == null || proximity > best)
                {
                    best = proximity;
                    archetype = zone._archetype;
                    index = zone._index;
                }
            }
            return best;
        }
    }
}
