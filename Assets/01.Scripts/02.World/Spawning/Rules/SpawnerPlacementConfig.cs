using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Spawning.Rules
{
    [CreateAssetMenu(fileName = "SpawnerPlacementConfig",
        menuName = "TheLightHouse/World/SpawnerPlacementConfig")]
    public sealed class SpawnerPlacementConfig : ScriptableObject
    {
        [SerializeField, Range(0.1f, 0.8f)] private float _spreadFactor = 0.6f;
        [SerializeField, Min(1f)] private float _minSpacingFloor = 25f;
        [SerializeField, Min(1f)] private float _queryCellSize = 50f;
        [SerializeField, Min(0f)] private float _obstacleClearance = 15f;
        [SerializeField, Min(0f)] private float _safeRadius = 50f;
        [SerializeField] private SpawnerAllocation[] _allocations;

        public float SpreadFactor => _spreadFactor;
        public float MinSpacingFloor => _minSpacingFloor;
        public float QueryCellSize => _queryCellSize;
        public float ObstacleClearance => _obstacleClearance;
        public float SafeRadius => _safeRadius;

        public IReadOnlyList<SpawnerAllocation> Allocations =>
            _allocations ?? Array.Empty<SpawnerAllocation>();
    }
}
