using System;
using System.Collections.Generic;
using Lighthouse.World.Islands.View;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using Lighthouse.World.Ocean.Sandbox;
using Unity.Collections;
using UnityEngine;

namespace Lighthouse.World.Ocean.View
{
    // One owner publishes the packed field for every locally built island.
    [DefaultExecutionOrder(-80)]
    [DisallowMultipleComponent]
    public sealed class F3MapWater : MonoBehaviour, ILocalMapBuilder
    {
        private const int MaxRegions = 32;
        [SerializeField] private IslandInstanceSet _islands;
        [SerializeField] private WaterSamplerHost _waves;
        [SerializeField] private F3IslandData[] _data;
        [SerializeField] private WaterF3Surf _surfPrefab;
        [SerializeField, Min(1)] private int _cellsPerFrame = 65536;
        [SerializeField, Min(0.1f)] private float _retargetDegrees = 2f;
        private readonly List<State> _states = new List<State>();
        private readonly List<GameObject> _surfs = new List<GameObject>();
        private State[] _tiles;
        private NativeArray<Vector4> _cells;
        private NativeArray<Vector4> _geometry;
        private NativeArray<Vector4> _regions;
        private Texture2D _transmission;
        private Texture2D _geometryTexture;
        private Texture2D _coverage;
        private Texture2D _currentTexture;
        private Color32[] _coveragePixels;
        private Vector4[] _shaderRegions = new Vector4[MaxRegions];
        private Vector4 _layout;
        private int _tileSize;
        private int _size;
        private int _columns;
        private int _cursor;
        private float _cell;
        private float _extent;
        private bool _ready;
        private static F3MapWater _active;

        private sealed class State : IDisposable
        {
            public F3IslandData Data;
            public NativeArray<Vector4> Cells;
            public Color[] Geometry;
            public Color32[] Coverage;
            public F3DirectionDriver Driver;
            public void Dispose()
            {
                if (Driver != null)
                {
                    Driver.Dispose();
                }
                if (Cells.IsCreated)
                {
                    Cells.Dispose();
                }
            }
        }

        public void Begin(MapGenerationConfig config, MapLayout layout, MapSeedInfo info)
        {
            Cancel();
            if (!isActiveAndEnabled || _islands == null || _waves == null || _surfPrefab == null
                || _data == null || _data.Length == 0 || _data.Length > MaxRegions
                || layout == null || _islands.Count != layout.Islands.Count || _islands.Count == 0
                || _islands.Count > MaxRegions || (_active != null && _active != this) || F3WaveField.IsActive)
            {
                throw new InvalidOperationException("F3 local map dependencies or single-owner contract are invalid.");
            }
            _active = this;
            _tiles = new State[_islands.Count];
            _regions = new NativeArray<Vector4>(_islands.Count, Allocator.Persistent);
            _columns = Mathf.CeilToInt(Mathf.Sqrt(_data.Length));
            float angle = IncomingAngle();
            for (int i = 0; i < _islands.Count; i++)
            {
                Transform root = _islands.GetTransform(i);
                IslandPlacement placement = default;
                bool found = false;
                for (int j = 0; j < layout.Islands.Count; j++)
                {
                    if (layout.Islands[j].Index == _islands.GetIndex(i))
                    {
                        placement = layout.Islands[j];
                        found = true;
                        break;
                    }
                }
                if (!found || !config.TryGetArchetype(placement.ArchetypeId, out var archetype)
                    || placement.VariantIndex >= archetype.Variants.Count
                    || Quaternion.Angle(root.rotation, Quaternion.identity) > 0.01f
                    || root.lossyScale != Vector3.one || Mathf.Abs(root.position.y) > 0.001f)
                {
                    throw new InvalidOperationException("F3 bake requires the current unscaled, yaw-zero, sea-level island contract.");
                }
                GameObject prefab = archetype.Variants[placement.VariantIndex].Prefab;
                State state = GetState(prefab, angle);
                if (i == 0)
                {
                    _tileSize = state.Data.Geometry.width;
                    _cell = state.Data.CellSize;
                    _extent = _tileSize * _cell;
                    _size = _columns * _tileSize;
                    if (_size > SystemInfo.maxTextureSize)
                    {
                        throw new InvalidOperationException("F3 atlas exceeds the device texture size.");
                    }
                }
                if (state.Data.Geometry.width != _tileSize || !Mathf.Approximately(state.Data.CellSize, _cell))
                {
                    throw new InvalidOperationException("F3 island tiles must use the same resolution.");
                }
                _tiles[i] = state;
                Vector2 origin = state.Data.Origin + new Vector2(root.position.x, root.position.z);
                _shaderRegions[i] = new Vector4(origin.x, origin.y, _states.IndexOf(state) % _columns * _extent, _states.IndexOf(state) / _columns * _extent);
                _regions[i] = _shaderRegions[i];
            }
            _cells = new NativeArray<Vector4>(_size * _size, Allocator.Persistent);
            _geometry = new NativeArray<Vector4>(_size * _size, Allocator.Persistent);
            _coveragePixels = new Color32[_size * _size];
            _transmission = CreateTexture("F3MapTransmission", TextureFormat.RGBAFloat);
            _geometryTexture = CreateTexture("F3MapGeometry", TextureFormat.RGBAFloat);
            _coverage = CreateTexture("F3MapCoverage", TextureFormat.RGBA32);
            _currentTexture = CreateTexture("F3MapCurrent", TextureFormat.RGBAFloat);
            _layout = new Vector4(0f, 0f, _cell, _size * _cell);
            _cursor = 0;
        }

        private State GetState(GameObject prefab, float angle)
        {
            for (int i = 0; i < _states.Count; i++)
            {
                if (_states[i].Data.Prefab == prefab)
                {
                    return _states[i];
                }
            }
            F3IslandData data = null;
            for (int i = 0; _data != null && i < _data.Length; i++)
            {
                if (_data[i] != null && _data[i].Prefab == prefab)
                {
                    data = _data[i];
                }
            }
            if (data == null || !data.IsValid)
            {
                throw new InvalidOperationException($"F3 baked data missing for {prefab.name}.");
            }
            NativeArray<byte> bytes = data.Directions.GetData<byte>();
            if (!F3DirectionalTransmission.TryReadHeader(bytes, out int size, out int count, out float baseAngle)
                || size != data.Geometry.width)
            {
                throw new InvalidOperationException("F3 directional header does not match its geometry.");
            }
            var state = new State { Data = data, Geometry = data.Geometry.GetPixels(), Coverage = data.Coverage.GetPixels32() };
            _states.Add(state);
            state.Cells = new NativeArray<Vector4>(size * size, Allocator.Persistent);
            state.Driver = new F3DirectionDriver(bytes, size, count, baseAngle, _cellsPerFrame, _retargetDegrees);
            state.Driver.ApplyImmediately(angle, state.Cells);
            return state;
        }

        public bool Step()
        {
            if (_tiles == null)
            {
                throw new InvalidOperationException("F3 local build is not active.");
            }
            if (_ready)
            {
                return true;
            }
            CopyTile(_cursor, true);
            _cursor++;
            if (_cursor < _tiles.Length)
            {
                return false;
            }
            _geometryTexture.SetPixelData(_geometry, 0);
            _geometryTexture.Apply(false, false);
            _coverage.SetPixels32(_coveragePixels);
            _coverage.Apply(false, false);
            PublishTransmission();
            F3WaveField.Register(_cells, _layout, _size, _geometry, _size, _regions, _extent);
            Shader.SetGlobalVector("_F3Layout", _layout);
            Shader.SetGlobalFloat("_F3GeometrySize", _size);
            Shader.SetGlobalVectorArray("_F3Regions", _shaderRegions);
            Shader.SetGlobalInt("_F3RegionCount", _tiles.Length);
            Shader.SetGlobalFloat("_F3RegionExtent", _extent);
            Shader.SetGlobalTexture("_F3Geometry", _geometryTexture);
            Shader.SetGlobalTexture("_F3GroundCoverage", _coverage);
            Shader.SetGlobalTexture("_F3CurrentBasis", _currentTexture);
            Shader.SetGlobalFloat("_F3Enabled", 1f);
            _ready = true;
            return true;
        }

        private void CopyTile(int index, bool includeGeometry)
        {
            State state = _tiles[index];
            Vector4 region = _regions[index];
            int startX = Mathf.RoundToInt(region.z / _cell);
            int startZ = Mathf.RoundToInt(region.w / _cell);
            WorldServices.TryGet<WaterCurrentService>(out var currents);
            var basis = includeGeometry ? new Color[_tileSize * _tileSize] : null;
            for (int z = 0; z < _tileSize; z++)
            {
                for (int x = 0; x < _tileSize; x++)
                {
                    int source = z * _tileSize + x;
                    int target = (startZ + z) * _size + startX + x;
                    _cells[target] = state.Cells[source];
                    if (!includeGeometry)
                    {
                        continue;
                    }
                    Vector4 geometry = state.Geometry[source];
                    // Ocean cells share one region across the translated copies of an island tile.
                    geometry.z = geometry.y > 0f ? 1f : geometry.z;
                    _geometry[target] = geometry;
                    _coveragePixels[target] = state.Coverage[source];
                    var world = new Vector3(region.x + (x + 0.5f) * _cell, 0f, region.y + (z + 0.5f) * _cell);
                    basis[source] = currents != null ? (Color)currents.SampleBasis(world) : new Color(1f, 0f, 0f, 1f);
                }
            }
            if (includeGeometry)
            {
                _currentTexture.SetPixels(startX, startZ, _tileSize, _tileSize, basis);
                // Surfs are built inactive, then initialized before OnEnable allocates their mesh.
                WaterF3Surf surf = Instantiate(_surfPrefab, Vector3.zero, Quaternion.identity, transform);
                surf.gameObject.SetActive(false);
                surf.InitializeWorldField(_waves, new Vector2(region.x, region.y) + Vector2.one * (_extent * 0.5f), index);
                _surfs.Add(surf.gameObject);
            }
        }

        private void Update()
        {
            if (!_ready)
            {
                return;
            }
            bool changed = false;
            float angle = IncomingAngle();
            for (int i = 0; i < _states.Count; i++)
            {
                changed |= _states[i].Driver.Tick(angle, _states[i].Cells);
            }
            if (changed)
            {
                for (int i = 0; i < _tiles.Length; i++)
                {
                    CopyTile(i, false);
                }
                PublishTransmission();
            }
            if (WorldServices.TryGet<WaterCurrentService>(out var currents))
            {
                Vector2 drive = currents.Drive;
                Shader.SetGlobalVector("_F3Flow", new Vector4(drive.normalized.x, drive.normalized.y, drive.magnitude, currents.MaxSpeed));
            }
        }

        private void PublishTransmission()
        {
            _transmission.SetPixelData(_cells, 0);
            _transmission.Apply(false, false);
            Shader.SetGlobalTexture("_F3Transmission", _transmission);
            if (!_ready)
            {
                _currentTexture.Apply(false, false);
                for (int i = 0; i < _surfs.Count; i++)
                {
                    _surfs[i].SetActive(true);
                }
            }
        }

        private float IncomingAngle()
        {
            Vector2 sum = Vector2.zero;
            ReadOnlySpan<WaveParams> waves = _waves.Inputs.Waves;
            for (int i = 0; i < waves.Length; i++)
            {
                sum += waves[i].Direction.normalized * Mathf.Abs(waves[i].Amplitude);
            }
            return F3DirectionalTransmission.AngleOf(sum.sqrMagnitude > Mathf.Epsilon ? sum : Vector2.right);
        }

        private Texture2D CreateTexture(string textureName, TextureFormat format)
        {
            return new Texture2D(_size, _size, format, false, true)
            {
                name = textureName, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
        }

        public static bool OwnsPoint(int index, Vector2 point)
        {
            if (_active == null || !_active._ready)
            {
                return false;
            }
            return F3WaveField.FindRegion(point.x, point.y, _active._regions, _active._extent) == index;
        }

        public static bool HasGroundAt(Vector2 point)
        {
            if (_active == null || !_active._ready)
            {
                return false;
            }
            float x = point.x;
            float z = point.y;
            if (!F3WaveField.MapPoint(ref x, ref z, _active._regions, _active._extent))
            {
                return false;
            }
            int ix = Mathf.Clamp(Mathf.FloorToInt(x / _active._cell), 0, _active._size - 1);
            int iz = Mathf.Clamp(Mathf.FloorToInt(z / _active._cell), 0, _active._size - 1);
            return _active._coveragePixels[iz * _active._size + ix].r > 127;
        }

        public void Cancel()
        {
            _ready = false;
            if (_active == this)
            {
                if (_cells.IsCreated)
                {
                    F3WaveField.Unregister(_cells);
                }
                Shader.SetGlobalFloat("_F3Enabled", 0f);
                Shader.SetGlobalFloat("_F3RegionExtent", 0f);
                Shader.SetGlobalInt("_F3RegionCount", 0);
                _active = null;
            }
            for (int i = 0; i < _surfs.Count; i++)
            {
                if (_surfs[i] != null)
                {
                    _surfs[i].SetActive(false);
                    Destroy(_surfs[i]);
                }
            }
            _surfs.Clear();
            for (int i = 0; i < _states.Count; i++)
            {
                _states[i].Dispose();
            }
            _states.Clear();
            if (_cells.IsCreated)
            {
                _cells.Dispose();
            }
            if (_geometry.IsCreated)
            {
                _geometry.Dispose();
            }
            if (_regions.IsCreated)
            {
                _regions.Dispose();
            }
            if (_transmission != null)
            {
                Destroy(_transmission);
            }
            if (_geometryTexture != null)
            {
                Destroy(_geometryTexture);
            }
            if (_coverage != null)
            {
                Destroy(_coverage);
            }
            if (_currentTexture != null)
            {
                Destroy(_currentTexture);
            }
            _tiles = null;
            _coveragePixels = null;
        }

        private void OnDisable()
        {
            Cancel();
        }
    }
}
