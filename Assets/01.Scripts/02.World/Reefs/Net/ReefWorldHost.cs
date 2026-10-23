using System;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Reefs.Rules;
using Lighthouse.World.Reefs.View;
using UnityEngine;

namespace Lighthouse.World.Reefs.Net
{
    [DisallowMultipleComponent]
    public sealed class ReefWorldHost : MonoBehaviour, ILocalMapBuilder
    {
        private const int ReefsPerStep = 4;

        [SerializeField] private ReefPrefabSet _prefabSet;
        [SerializeField] private ReefInstanceSet _instances;

        private MapLayout _layout;
        private int _nextIndex;
        private bool _isBuilding;

        private void Awake()
        {
            if (_prefabSet == null || _instances == null)
            {
                Debug.LogError($"[{nameof(ReefWorldHost)}] ReefPrefabSet and ReefInstanceSet are required.");
                enabled = false;
            }
        }

        private void OnDisable()
        {
            Cancel();
        }

        public void Begin(MapGenerationConfig config, MapLayout layout, MapSeedInfo info)
        {
            Cancel();

            if (!isActiveAndEnabled)
            {
                throw new InvalidOperationException("Reef builder is unavailable.");
            }

            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            if (!_prefabSet.IsValid)
            {
                Debug.LogWarning($"[{nameof(ReefWorldHost)}] ReefPrefabSet is empty or has missing prefabs. Reefs are skipped.");
                _layout = new MapLayout(layout.Seed, layout.Islands, Array.Empty<ReefPlacement>());
            }
            else
            {
                _layout = layout;
            }

            _nextIndex = 0;
            _isBuilding = true;
        }

        public bool Step()
        {
            if (!_isBuilding || !isActiveAndEnabled)
            {
                throw new InvalidOperationException("Reef build is not active.");
            }

            int end = Mathf.Min(_nextIndex + ReefsPerStep, _layout.Reefs.Count);
            for (; _nextIndex < end; _nextIndex++)
            {
                CreateReef(_nextIndex);
            }

            if (_nextIndex < _layout.Reefs.Count)
            {
                return false;
            }

            _isBuilding = false;
            return true;
        }

        public void Cancel()
        {
            _isBuilding = false;
            _nextIndex = 0;
            _layout = null;

            if (_instances != null)
            {
                _instances.Clear();
            }
        }

        private void CreateReef(int index)
        {
            ReefPlacement placement = _layout.Reefs[index];
            ReefVariant variant = ReefPlacementMath.Pick(_layout.Seed, index, _prefabSet.Count);

            if (!_prefabSet.TryGet(variant.PrefabIndex, out GameObject prefab))
            {
                throw new InvalidOperationException($"Reef {index}: prefab {variant.PrefabIndex} is missing.");
            }

            _instances.Create(
                prefab,
                ReefPlacementMath.ToWorld(placement),
                variant.YawDegrees,
                $"Reef_{index}_{placement.Zone}");
        }
    }
}
