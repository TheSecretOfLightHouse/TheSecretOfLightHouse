using System;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    [RequireComponent(typeof(MeshFilter))]
    public class WaveDebugMesh : MonoBehaviour
    {
        private const int MinGridCells = 2;
        private const int MaxGridCells = 200;
        private const float Half = 0.5f;
        private const float GizmoSphereRadius = 0.25f;
        private const string MeshName = "WaveDebugMesh";

        [SerializeField]
        private WaveParams[] _waves =
        {
            new WaveParams { Amplitude = 0.5f, Wavelength = 20f, Direction = new Vector2(1f, 0f), Steepness = 0.3f, Speed = 3f },
            new WaveParams { Amplitude = 0.25f, Wavelength = 9f, Direction = new Vector2(0.6f, 0.8f), Steepness = 0.2f, Speed = 2.2f }
        };

        [SerializeField, Range(MinGridCells, MaxGridCells)] private int _gridCells = 64;
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private float _time;
        [SerializeField] private bool _isAnimating;
        [SerializeField] private int _gizmoCount = 9;
        [SerializeField] private float _gizmoSpacing = 4f;
        [SerializeField] private float _inverseTolerance = 0.05f;
        [SerializeField] private bool _isModifierEnabled;
        [SerializeField] private WaveModifierKind _modifierKind = WaveModifierKind.Bump;
        [SerializeField] private Vector2 _modifierCenter;
        [SerializeField] private float _modifierRadius = 10f;
        [SerializeField] private float _modifierStrength = 1f;

        private readonly WaveModifierData[] _modifierBuffer = new WaveModifierData[1];
        private Mesh _mesh;
        private Vector3[] _vertices;
        private int _builtCells;

        private void Start()
        {
            RebuildMesh();
        }

        private void Update()
        {
            if (!_isAnimating || _mesh == null || _vertices == null || _waves == null)
            {
                return;
            }

            _time += Time.deltaTime;
            FillVertices();
            ApplyVertices();
        }

        private void OnDestroy()
        {
            if (_mesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_mesh);
            }
            else
            {
                DestroyImmediate(_mesh);
            }

            _mesh = null;
        }

        private void OnDrawGizmos()
        {
            if (_waves == null)
            {
                return;
            }

            ReadOnlySpan<WaveParams> waves = _waves;
            ReadOnlySpan<WaveModifierData> modifiers = GetModifiers();
            Vector3 origin = transform.position;
            float centerIndex = (_gizmoCount - 1) * Half;

            Gizmos.color = Color.yellow;

            for (int i = 0; i < _gizmoCount; i++)
            {
                float x = origin.x + (i - centerIndex) * _gizmoSpacing;
                float height = WaveModel.HeightAt(waves, modifiers, x, origin.z, _time);
                Gizmos.DrawSphere(new Vector3(x, origin.y + height, origin.z), GizmoSphereRadius);
            }

            if (_isModifierEnabled)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(new Vector3(_modifierCenter.x, origin.y, _modifierCenter.y), _modifierRadius);
            }
        }

        [ContextMenu("Wave/Rebuild Mesh")]
        private void RebuildMesh()
        {
            if (_waves == null)
            {
                Debug.LogError($"[{nameof(WaveDebugMesh)}] Waves array is null.");
                return;
            }

            if (!TryGetComponent(out MeshFilter filter))
            {
                Debug.LogError($"[{nameof(WaveDebugMesh)}] MeshFilter is missing.");
                return;
            }

            if (_mesh == null)
            {
                Mesh existing = filter.sharedMesh;
                _mesh = existing != null && existing.name == MeshName ? existing : CreateMesh();
                filter.sharedMesh = _mesh;
            }

            int cells = Mathf.Clamp(_gridCells, MinGridCells, MaxGridCells);
            WaveGrid grid = CreateGrid(cells);
            bool isStructureChanged = _vertices == null || _builtCells != cells;

            if (isStructureChanged)
            {
                _builtCells = cells;
                _vertices = new Vector3[grid.VertexCount];
            }

            FillVertices();

            if (isStructureChanged)
            {
                _mesh.Clear();
                _mesh.SetVertices(_vertices);
                _mesh.SetTriangles(WaveMeshBuilder.BuildTriangles(grid), 0);
                _mesh.RecalculateNormals();
                _mesh.RecalculateBounds();
            }
            else
            {
                ApplyVertices();
            }
        }

        [ContextMenu("Wave/Run All Tests")]
        private void RunAllTests()
        {
            if (_waves == null)
            {
                Debug.LogError($"[{nameof(WaveDebugMesh)}] Waves array is null.");
                return;
            }

            WaveGrid grid = CreateGrid(Mathf.Clamp(_gridCells, MinGridCells, MaxGridCells));
            WaveCheckContext context = new WaveCheckContext(
                _waves,
                GetModifiers(),
                grid,
                _time,
                _inverseTolerance,
                _modifierRadius,
                _modifierStrength);

            bool isAllPassed = WaveModelChecks.RunAll(context);
            isAllPassed &= WaveModifierChecks.RunAll(context);

            WaveCheckLog.Report("All", isAllPassed, "see lines above");
        }

        private WaveGrid CreateGrid(int cells)
        {
            return new WaveGrid(cells, _cellSize, transform.position);
        }

        private WaveModifierData[] GetModifiers()
        {
            if (!_isModifierEnabled)
            {
                return Array.Empty<WaveModifierData>();
            }

            _modifierBuffer[0] = new WaveModifierData(_modifierKind, _modifierCenter, _modifierRadius, _modifierStrength, 0f);
            return _modifierBuffer;
        }

        private void FillVertices()
        {
            WaveMeshBuilder.FillVertices(_vertices, CreateGrid(_builtCells), _time, _waves, GetModifiers());
        }

        private void ApplyVertices()
        {
            _mesh.SetVertices(_vertices);
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }

        private static Mesh CreateMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = MeshName;
            mesh.hideFlags = HideFlags.HideAndDontSave;
            mesh.MarkDynamic();
            return mesh;
        }
    }
}
