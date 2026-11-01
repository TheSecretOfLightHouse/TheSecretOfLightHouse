using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.UI.SharedMap.View
{
    // The generated island registers its authored chart on both the host and clients.
    public sealed class SharedMapTable : MonoBehaviour
    {
        private static readonly HashSet<SharedMapTable> Tables = new HashSet<SharedMapTable>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Tables.Clear();
        }

        private void OnEnable()
        {
            Tables.Add(this);
        }

        private void OnDisable()
        {
            Tables.Remove(this);
        }

        public static bool TryGetPosition(Vector2 lighthouse, out Vector3 position)
        {
            position = default;
            float nearest = float.PositiveInfinity;
            bool found = false;
            foreach (SharedMapTable table in Tables)
            {
                if (!table || !table.isActiveAndEnabled)
                {
                    continue;
                }
                Vector3 candidate = table.transform.position;
                float distance = (new Vector2(candidate.x, candidate.z) - lighthouse).sqrMagnitude;
                if (distance < nearest)
                {
                    nearest = distance;
                    position = candidate;
                    found = true;
                }
            }
            return found;
        }
    }
}
