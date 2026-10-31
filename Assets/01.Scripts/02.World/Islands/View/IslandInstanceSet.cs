using System.Collections.Generic;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.Weather.View;
using UnityEngine;

namespace Lighthouse.World.Islands.View
{
    [DisallowMultipleComponent]
    public class IslandInstanceSet : MonoBehaviour, IIslandRootSource
    {
        [SerializeField] private Material _coastalShelfMaterial;
        [SerializeField] private Material _coastalRockMaterial;
        [SerializeField, Min(0f)] private float _coastalShelfWidth = 7f;
        [SerializeField, Min(0f)] private float _lagoonRadiusScale = 0.55f;
        [SerializeField, Min(0f)] private float _lagoonMinReach = 4f;

        private readonly List<short> _indices = new List<short>();
        private readonly List<GameObject> _instances = new List<GameObject>();

        public int Count => _instances.Count;

        private void OnDestroy()
        {
            Clear();
        }

        public void Build(IReadOnlyList<IslandSpawnPlan> plans)
        {
            for (int i = 0; i < plans.Count; i++)
            {
                IslandRotationRules.GetRotation(plans[i].Prefab, plans[i].YawDegrees);
                IslandRotationRules.ValidateParent(plans[i].Prefab, transform);
            }

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

            IslandRotationRules.ValidateParent(plan.Prefab, transform);
            Quaternion rotation = IslandRotationRules.GetRotation(plan.Prefab, plan.YawDegrees);

            GameObject instance = Instantiate(
                plan.Prefab,
                plan.Position,
                rotation,
                transform);

            _indices.Add(plan.Index);
            _instances.Add(instance);
            instance.name = $"Island_{plan.Index}_{plan.Archetype.name}_v{plan.VariantIndex}";
            var audioZone = instance.GetComponent<WorldIslandAudioZone>();
            if (audioZone == null)
            {
                audioZone = instance.AddComponent<WorldIslandAudioZone>();
            }
            audioZone.Initialize(plan.Archetype, plan.Index);

            if (!instance.TryGetComponent<TerrainCoastWaveView>(out _) && instance.GetComponentInChildren<Terrain>() != null)
            {
                instance.AddComponent<TerrainCoastWaveView>();
            }

            if (_coastalShelfMaterial != null && _coastalShelfWidth > 0f && !HasOwnCoastWaves(instance))
            {
                if (!CoastalShelfView.Create(instance, _coastalShelfMaterial, _coastalShelfWidth, _coastalRockMaterial, _lagoonRadiusScale, _lagoonMinReach))
                {
                    Debug.LogWarning($"[{nameof(IslandInstanceSet)}] Coastal shelf could not be built for {instance.name}.");
                }
            }
        }

        private static bool HasOwnCoastWaves(GameObject instance)
        {
            return instance.TryGetComponent<IslandCoastWaveView>(out _)
                || instance.TryGetComponent<TerrainCoastWaveView>(out _);
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

        public bool TryGetRoot(short islandIndex, out Transform root)
        {
            for (int i = 0; i < _indices.Count; i++)
            {
                if (_indices[i] == islandIndex && _instances[i] != null)
                {
                    root = _instances[i].transform;
                    return true;
                }
            }

            root = null;
            return false;
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
