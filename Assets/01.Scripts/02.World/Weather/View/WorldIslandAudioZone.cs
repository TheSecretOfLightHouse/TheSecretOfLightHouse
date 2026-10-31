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

        public static float GetNearest(Vector3 position, float fadeDistance, out IslandArchetype archetype, out short index)
        {
            float nearest = float.PositiveInfinity;
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
                float distance = Mathf.Max(0f, new Vector2(delta.x, delta.z).magnitude - zone._archetype.Radius);
                if (distance < nearest)
                {
                    nearest = distance;
                    archetype = zone._archetype;
                    index = zone._index;
                }
            }
            return Mathf.Clamp01(1f - nearest / Mathf.Max(1f, fadeDistance));
        }
    }
}
