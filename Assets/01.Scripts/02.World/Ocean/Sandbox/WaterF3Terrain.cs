using System;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using Unity.Collections;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class WaterF3Terrain : MonoBehaviour
    {
        [SerializeField]
        private Texture2D _transmission;
        [SerializeField]
        private Texture2D _geometry;
        [SerializeField] private Texture2D _groundCoverage;
        [SerializeField] private TextAsset _directional;
        [SerializeField] private WaterSamplerHost _waveSource;
        [SerializeField] private int _cellsPerFrame = 65536;
        [SerializeField] private float _retargetDegrees = 2f;
        [SerializeField]
        private Vector2 _origin;
        [SerializeField]
        private float _cellSize = 0.25f;
        [SerializeField]
        private Vector2 _incoming = new Vector2(-0.8f, 0.6f);
        [SerializeField] private float _currentSpeed = 0.35f;
        [SerializeField] private float _currentMaxSpeed = 0.8f;
        [SerializeField] private float _currentCell = 1f;
        private WaterCurrentField _current;
        private Texture2D _currentTexture;
        private NativeArray<Vector4> _cells;
        private NativeArray<Vector4> _geometryCells;
        private Color[] _geometryPixels;
        private Color32[] _groundPixels;
        private Vector4 _layout;
        private F3DirectionDriver _driver;
        private Texture2D _runtimeTransmission;
        private int _size;
        public Texture2D Geometry => _geometry;
        public Texture2D Transmission => _runtimeTransmission != null ? _runtimeTransmission : _transmission;
        public Vector4 Layout => _layout;
        public int Size => _size > 0 ? _size : (_transmission != null ? _transmission.width : 0);
        public float AppliedAngle => _driver != null ? _driver.AppliedAngle : F3DirectionalTransmission.AngleOf(_incoming);
        public bool IsBlending => _driver != null && _driver.IsBlending;
        public float GeometryCellSize => _geometry != null ? _cellSize * Size / _geometry.width : _cellSize;
        private void OnEnable()
        {
            if (_geometry == null || (_transmission == null && _directional == null))
            {
                return;
            }

            if (_directional != null && !TryCreateDirectional())
            {
                return;
            }
            else if (_directional == null)
            {
                _size = _transmission.width;
                Color[] pixels = _transmission.GetPixels();
                _cells = new NativeArray<Vector4>(pixels.Length, Allocator.Persistent);
                for (int i = 0; i < pixels.Length; i++)
                {
                    _cells[i] = pixels[i];
                }
            }

            _geometryPixels = _geometry.GetPixels();
            _groundPixels = _groundCoverage != null ? _groundCoverage.GetPixels32() : null;
            _geometryCells = new NativeArray<Vector4>(_geometryPixels.Length, Allocator.Persistent);
            for (int i = 0; i < _geometryPixels.Length; i++)
            {
                _geometryCells[i] = _geometryPixels[i];
            }

            _layout = new Vector4(_origin.x, _origin.y, _cellSize, _size * _cellSize);
            F3WaveField.Register(_cells, _layout, _size, _geometryCells, _geometry.width);
            Shader.SetGlobalFloat("_F3RegionExtent", 0f);
            Shader.SetGlobalInt("_F3RegionCount", 0);
            Shader.SetGlobalTexture("_F3Transmission", Transmission);
            Shader.SetGlobalTexture("_F3Geometry", _geometry);
            Shader.SetGlobalTexture("_F3GroundCoverage", _groundCoverage != null ? _groundCoverage : Texture2D.whiteTexture);
            Shader.SetGlobalVector("_F3Layout", _layout);
            Shader.SetGlobalFloat("_F3GeometrySize", _geometry.width);
            BakeCurrent();
            PublishCurrent(_driver != null ? F3DirectionalTransmission.DirectionOf(_driver.AppliedAngle) : _incoming.normalized);
            Shader.SetGlobalFloat("_F3Enabled", 1f);
        }

        private void OnDisable()
        {
            if (_cells.IsCreated)
            {
                F3WaveField.Unregister(_cells);
                _cells.Dispose();
                _geometryCells.Dispose();
            }

            Shader.SetGlobalFloat("_F3Enabled", 0f);
            if (_current != null)
            {
                WorldServices.Unregister<IWaterCurrentSource>(_current);
                _current = null;
            }

            if (_currentTexture != null)
            {
                Destroy(_currentTexture);
                _currentTexture = null;
            }

            if (_driver != null)
            {
                _driver.Dispose();
                _driver = null;
            }

            if (_runtimeTransmission != null)
            {
                Destroy(_runtimeTransmission);
                _runtimeTransmission = null;
            }

            _size = 0;
            _geometryPixels = null;
            _groundPixels = null;
        }

        private void Update()
        {
            if (_driver != null && _driver.Tick(F3DirectionalTransmission.AngleOf(ResolveIncoming()), _cells))
            {
                PublishDirection();
            }
        }

        // Applies the blend for this direction before returning; the field also follows the wave source afterwards when one is set.
        public void ApplyIncomingImmediately(Vector2 travelDirection)
        {
            _incoming = travelDirection.normalized;
            if (_driver != null)
            {
                _driver.ApplyImmediately(F3DirectionalTransmission.AngleOf(_incoming), _cells);
                PublishDirection();
            }
        }

        public Vector4 Sample(Vector2 position)
        {
            return _cells.IsCreated ? F3WaveField.Sample(_cells, _layout, Size, position.x, position.y, _geometryCells, _geometry.width) : new Vector4(1f, 0f, 0f, 0f);
        }

        public Vector4 GeometryAt(Vector2 position)
        {
            if (_geometryPixels == null)
            {
                return Vector4.zero;
            }

            int size = _geometry.width;
            float cell = GeometryCellSize;
            int x = Mathf.FloorToInt((position.x - _origin.x) / cell);
            int z = Mathf.FloorToInt((position.y - _origin.y) / cell);
            Vector4 data = x >= 0 && z >= 0 && x < size && z < size ? (Vector4)_geometryPixels[z * size + x] : new Vector4(4f, 1f, 1f, 0f);
            Vector4 filtered = F3WaveField.GeometryAt(_geometryCells, _layout, size, position.x, position.y);
            data.x = filtered.x;
            data.w = filtered.w;
            return data;
        }

        private bool TryCreateDirectional()
        {
            NativeArray<byte> bytes = _directional.GetData<byte>();
            if (!F3DirectionalTransmission.TryReadHeader(bytes, out int size, out int count, out float baseAngle) || size != _geometry.width)
            {
                Debug.LogError($"[{nameof(WaterF3Terrain)}] Directional transmission data does not match the geometry texture.");
                return false;
            }

            _size = size;
            _cells = new NativeArray<Vector4>(size * size, Allocator.Persistent);
            _runtimeTransmission = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true)
            {
                name = "F3DirectionalTransmission",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _driver = new F3DirectionDriver(bytes, size, count, baseAngle, _cellsPerFrame, _retargetDegrees);
            _driver.ApplyImmediately(F3DirectionalTransmission.AngleOf(_incoming.normalized), _cells);
            _runtimeTransmission.SetPixelData(_cells, 0);
            _runtimeTransmission.Apply(false, false);
            return true;
        }

        private void PublishDirection()
        {
            _runtimeTransmission.SetPixelData(_cells, 0);
            _runtimeTransmission.Apply(false, false);
            PublishCurrent(F3DirectionalTransmission.DirectionOf(_driver.AppliedAngle));
        }

        private void BakeCurrent()
        {
            _current = WaterCurrentSolver.Bake(_geometryCells, _geometry.width, _origin, GeometryCellSize, _currentCell, _currentMaxSpeed);
            if (_current == null)
            {
                return;
            }

            _currentTexture = new Texture2D(_current.Size, _current.Size, TextureFormat.RGBAFloat, false, true)
            {
                name = "F3CurrentBasis",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _currentTexture.SetPixelData(_current.Basis, 0);
            _currentTexture.Apply(false, false);
            Shader.SetGlobalTexture("_F3CurrentBasis", _currentTexture);
            Shader.SetGlobalVector("_F3CurrentLayout", new Vector4(_current.Origin.x, _current.Origin.y, _current.Extent, _current.Size));
            WorldServices.Register<IWaterCurrentSource>(_current);
        }

        private void PublishCurrent(Vector2 direction)
        {
            Shader.SetGlobalVector("_F3Flow", new Vector4(direction.x, direction.y, _currentSpeed, _currentMaxSpeed));
            if (_current != null)
            {
                _current.SetDrive(direction, _currentSpeed);
            }
        }

        // Amplitude-weighted mean travel direction of the live waves; falls back to the serialized direction.
        private Vector2 ResolveIncoming()
        {
            if (_waveSource == null)
            {
                return _incoming;
            }

            ReadOnlySpan<WaveParams> waves = _waveSource.Inputs.Waves;
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < waves.Length; i++)
            {
                sum += waves[i].Direction.normalized * Mathf.Abs(waves[i].Amplitude);
            }

            return sum.sqrMagnitude > Mathf.Epsilon ? sum.normalized : _incoming;
        }

        public bool HasGroundAt(Vector2 position)
        {
            if (_groundPixels == null || _groundCoverage == null)
            {
                return true;
            }
            int size = _groundCoverage.width;
            int x = Mathf.FloorToInt((position.x - _layout.x) / _layout.w * size);
            int z = Mathf.FloorToInt((position.y - _layout.y) / _layout.w * size);
            return x >= 0 && z >= 0 && x < size && z < size && _groundPixels[z * size + x].r > 127;
        }

        public bool CanTransmitWake(Vector2 from, Vector2 to)
        {
            Vector4 source = GeometryAt(from);
            Vector4 target = GeometryAt(to);
            if (source.x <= 0f || target.x <= 0f || source.z != target.z)
            {
                return false;
            }

            int steps = Mathf.CeilToInt(Vector2.Distance(from, to) / (GeometryCellSize * 0.5f));
            for (int i = 1; i < steps; i++)
            {
                if (GeometryAt(Vector2.Lerp(from, to, (float)i / steps)).x <= 0f)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
