using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Islands.Rules;
using UnityEngine;

namespace Lighthouse.World.Islands.View
{
    [DisallowMultipleComponent]
    public class IslandInstanceSet : MonoBehaviour
    {
        private readonly List<short> _indices = new List<short>();
        private readonly List<GameObject> _instances = new List<GameObject>();

        public int Count => _instances.Count;

        private void OnDestroy()
        {
            Clear();
        }

        public void Build(IReadOnlyList<IslandSpawnPlan> plans)
        {
            Clear();
            for (int i = 0; i < plans.Count; i++)
            {
                Create(plans[i]);
            }
        }

        public void Create(IslandSpawnPlan plan)
        {
            if (plan.Prefab == null || plan.Archetype == null)
            {
                throw new System.ArgumentException(
                    $"Island {plan.Index}: prefab or archetype is missing.",
                    nameof(plan));
            }

            if (_indices.Contains(plan.Index))
            {
                throw new System.InvalidOperationException(
                    $"Island {plan.Index} already exists.");
            }

            GameObject instance = Instantiate(
                plan.Prefab,
                plan.Position,
                Quaternion.Euler(0f, plan.YawDegrees, 0f),
                transform);

            _indices.Add(plan.Index);
            _instances.Add(instance);
            instance.name = $"Island_{plan.Index}_{plan.Archetype.name}_v{plan.VariantIndex}";
        }

        public void Clear()
        {
            for (int i = 0; i < _instances.Count; i++)
            {
                if (_instances[i] != null)
                {
                    _instances[i].SetActive(false);
                    Destroy(_instances[i]);
                }
            }

            _indices.Clear();
            _instances.Clear();
        }

        public short GetIndex(int position)
        {
            return _indices[position];
        }

        public Transform GetTransform(int position)
        {
            return _instances[position].transform;
        }

        public bool TryGetSpots(short islandIndex, List<HarvestSpot> results)
        {
            results.Clear();
            for (int i = 0; i < _indices.Count; i++)
            {
                if (_indices[i] == islandIndex && _instances[i] != null)
                {
                    _instances[i].GetComponentsInChildren(true, results);
                    return true;
                }
            }

            return false;
        }
    }
}
