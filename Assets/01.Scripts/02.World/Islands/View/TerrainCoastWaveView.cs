using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using Unity.Collections;
using UnityEngine;

namespace Lighthouse.World.Islands.View
{
    // Terrain islands have no Ground_ContinuousClosed mesh for CoastalShelfView to scan.
    // Rebuild the outer shoreline once per spawn so terrain edits reach CPU and GPU together.
    [DisallowMultipleComponent]
    public sealed class TerrainCoastWaveView : MonoBehaviour
    {
        [SerializeField] private Terrain _terrain;
        [SerializeField] private float _seaLevel;
        [SerializeField, Min(0.01f)] private float _shoreHeight = 0.1f;
        [SerializeField, Min(0.1f)] private float _waveBlendWidth = 7f;
        [SerializeField, Min(0f)] private float _shoreOverlap = 1.5f;
        [SerializeField, Min(0f)] private float _shoreLift = 0.25f;

        private int _slot = -1;
        private WaterCurrentService _currents;
        private WaterCurrentField _current;

        private void Start()
        {
            RegisterCurrent();
        }

        private void OnEnable()
        {
            if (_terrain == null)
            {
                _terrain = GetComponentInChildren<Terrain>();
            }
            if (!_terrain || !_terrain.terrainData)
            {
                Debug.LogError($"[{nameof(TerrainCoastWaveView)}] Terrain is required.", this);
                return;
            }

            if (!TryGetComponent<IslandCoastWaveView>(out var existing) || !existing.enabled)
            {
                Vector3[] contour = TerrainCoastContour.Compute(_terrain, _seaLevel, _shoreHeight, _shoreOverlap);
                _slot = CoastWaveRegistry.Register(contour, _waveBlendWidth, default, 4f, Mathf.Max(7f, _waveBlendWidth) * 5f, _shoreLift);
                if (_slot < 0)
                {
                    Debug.LogError($"[{nameof(TerrainCoastWaveView)}] Coast wave capacity exceeded.", this);
                }
            }

            RegisterCurrent();
        }

        private void OnDisable()
        {
            if (_currents != null && _current != null)
            {
                _currents.RemoveRegion(_current);
            }
            _currents = null;
            _current = null;
            CoastWaveRegistry.Unregister(_slot);
            _slot = -1;
        }

        private void RegisterCurrent()
        {
            if (_current != null || _terrain == null || _terrain.terrainData == null
                || !WorldServices.TryGet(out _currents))
            {
                return;
            }

            const float Cell = 1f;
            const float Padding = 8f;
            Vector3 terrainOrigin = _terrain.transform.position;
            Vector3 terrainSize = _terrain.terrainData.size;
            Vector2 origin = new Vector2(terrainOrigin.x - Padding, terrainOrigin.z - Padding);
            int size = Mathf.CeilToInt((Mathf.Max(terrainSize.x, terrainSize.z) + Padding * 2f) / Cell);
            var geometry = new NativeArray<Vector4>(size * size, Allocator.Temp);
            try
            {
                for (int z = 0; z < size; z++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        Vector3 point = new Vector3(origin.x + (x + 0.5f) * Cell, 0f, origin.y + (z + 0.5f) * Cell);
                        float localX = point.x - terrainOrigin.x;
                        float localZ = point.z - terrainOrigin.z;
                        float depth = 4f;
                        if (localX >= 0f && localZ >= 0f && localX < terrainSize.x && localZ < terrainSize.z)
                        {
                            int hx = Mathf.Clamp((int)(localX / terrainSize.x * _terrain.terrainData.holesResolution), 0, _terrain.terrainData.holesResolution - 1);
                            int hz = Mathf.Clamp((int)(localZ / terrainSize.z * _terrain.terrainData.holesResolution), 0, _terrain.terrainData.holesResolution - 1);
                            if (!_terrain.terrainData.IsHole(hx, hz))
                            {
                                depth = _seaLevel - terrainOrigin.y - _terrain.SampleHeight(point);
                            }
                        }
                        geometry[z * size + x] = new Vector4(depth, depth > 0f ? 1f : 0f, 1f, depth > 0f ? depth : 0f);
                    }
                }
                _current = WaterCurrentSolver.Bake(geometry, size, origin, Cell, Cell, 0.8f);
            }

            finally
            {
                geometry.Dispose();
            }

            if (!_currents.TryAddRegion(_current))
            {
                Debug.LogError($"[{nameof(TerrainCoastWaveView)}] Current region capacity exceeded.", this);
                _current = null;
            }
        }
    }
}
