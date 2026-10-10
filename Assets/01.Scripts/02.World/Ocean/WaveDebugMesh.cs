using System;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean
{
    [RequireComponent(typeof(MeshFilter))]
    public class WaveDebugMesh : MonoBehaviour
    {
        private const int MinGridCells = 2;
        private const int MaxGridCells = 200;
        private const float Half = 0.5f;
        private const float GizmoSphereRadius = 0.25f;
        private const float BoundEpsilon = 0.0001f;
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
            Vector3 origin = transform.position;
            float centerIndex = (_gizmoCount - 1) * Half;

            Gizmos.color = Color.yellow;

            for (int i = 0; i < _gizmoCount; i++)
            {
                float x = origin.x + (i - centerIndex) * _gizmoSpacing;
                float height = WaveModel.HeightAt(waves, ReadOnlySpan<WaveModifierData>.Empty, x, origin.z, _time);
                Gizmos.DrawSphere(new Vector3(x, origin.y + height, origin.z), GizmoSphereRadius);
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
            bool isStructureChanged = _vertices == null || _builtCells != cells;

            if (isStructureChanged)
            {
                _builtCells = cells;
                _vertices = new Vector3[(cells + 1) * (cells + 1)];
            }

            FillVertices();

            if (isStructureChanged)
            {
                _mesh.Clear();
                _mesh.SetVertices(_vertices);
                _mesh.SetTriangles(BuildTriangles(cells), 0);
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

            ReadOnlySpan<WaveParams> waves = _waves;
            bool isAllPassed = true;

            isAllPassed &= CheckValidity(waves);
            isAllPassed &= CheckDeterminism(waves);
            isAllPassed &= CheckEmptyWaves();
            isAllPassed &= CheckAmplitudeBound(waves);
            isAllPassed &= CheckInverse(waves);

            Report("All", isAllPassed, "see lines above");
        }

        private bool CheckValidity(ReadOnlySpan<WaveParams> waves)
        {
            float totalSteepness = 0f;
            for (int i = 0; i < waves.Length; i++)
            {
                totalSteepness += waves[i].Steepness;
            }

            bool isPassed = WaveModel.IsValid(waves);
            Report("Validity", isPassed, $"waves={waves.Length}, totalSteepness={totalSteepness:F3}, max={WaveModel.MaxTotalSteepness}");
            return isPassed;
        }

        private bool CheckDeterminism(ReadOnlySpan<WaveParams> waves)
        {
            int cells = Mathf.Clamp(_gridCells, MinGridCells, MaxGridCells);
            Vector3 origin = transform.position;
            int mismatches = 0;

            for (int iz = 0; iz <= cells; iz++)
            {
                for (int ix = 0; ix <= cells; ix++)
                {
                    GetRestPosition(ix, iz, cells, origin, out float x, out float z);

                    float heightA = WaveModel.HeightAt(waves, ReadOnlySpan<WaveModifierData>.Empty, x, z, _time);
                    float heightB = WaveModel.HeightAt(waves, ReadOnlySpan<WaveModifierData>.Empty, x, z, _time);
                    Vector3 normalA = WaveModel.NormalAt(waves, ReadOnlySpan<WaveModifierData>.Empty, x, z, _time);
                    Vector3 normalB = WaveModel.NormalAt(waves, ReadOnlySpan<WaveModifierData>.Empty, x, z, _time);

                    bool isHeightSame = heightA == heightB;
                    bool isNormalSame = normalA.x == normalB.x && normalA.y == normalB.y && normalA.z == normalB.z;

                    if (!isHeightSame || !isNormalSame)
                    {
                        mismatches++;
                    }
                }
            }

            bool isPassed = mismatches == 0;
            Report("Determinism", isPassed, $"mismatches={mismatches}");
            return isPassed;
        }

        private bool CheckEmptyWaves()
        {
            ReadOnlySpan<WaveParams> none = ReadOnlySpan<WaveParams>.Empty;
            Vector3 origin = transform.position;

            float height = WaveModel.HeightAt(none, ReadOnlySpan<WaveModifierData>.Empty, origin.x + 3f, origin.z - 7f, _time);
            Vector3 normal = WaveModel.NormalAt(none, ReadOnlySpan<WaveModifierData>.Empty, origin.x + 3f, origin.z - 7f, _time);

            bool isPassed = Mathf.Approximately(height, 0f) && Mathf.Approximately(normal.y, 1f);
            Report("EmptyWaves", isPassed, $"height={height}, normal={normal}");
            return isPassed;
        }

        private bool CheckAmplitudeBound(ReadOnlySpan<WaveParams> waves)
        {
            float amplitudeSum = 0f;
            for (int i = 0; i < waves.Length; i++)
            {
                amplitudeSum += waves[i].Amplitude;
            }

            int cells = Mathf.Clamp(_gridCells, MinGridCells, MaxGridCells);
            Vector3 origin = transform.position;
            float maxAbsHeight = 0f;

            for (int iz = 0; iz <= cells; iz++)
            {
                for (int ix = 0; ix <= cells; ix++)
                {
                    GetRestPosition(ix, iz, cells, origin, out float x, out float z);
                    float height = WaveModel.HeightAt(waves, ReadOnlySpan<WaveModifierData>.Empty, x, z, _time);
                    maxAbsHeight = Mathf.Max(maxAbsHeight, Mathf.Abs(height));
                }
            }

            bool isPassed = maxAbsHeight <= amplitudeSum + BoundEpsilon;
            Report("AmplitudeBound", isPassed, $"maxAbsHeight={maxAbsHeight:F4}, amplitudeSum={amplitudeSum:F4}");
            return isPassed;
        }

        private bool CheckInverse(ReadOnlySpan<WaveParams> waves)
        {
            int cells = Mathf.Clamp(_gridCells, MinGridCells, MaxGridCells);
            Vector3 origin = transform.position;
            float maxError = 0f;

            for (int iz = 0; iz <= cells; iz++)
            {
                for (int ix = 0; ix <= cells; ix++)
                {
                    GetRestPosition(ix, iz, cells, origin, out float restX, out float restZ);
                    Vector3 displacement = WaveModel.DisplacementAt(waves, restX, restZ, _time);
                    float height = WaveModel.HeightAt(
                        waves,
                        ReadOnlySpan<WaveModifierData>.Empty,
                        restX + displacement.x,
                        restZ + displacement.z,
                        _time);

                    maxError = Mathf.Max(maxError, Mathf.Abs(height - displacement.y));
                }
            }

            bool isPassed = maxError <= _inverseTolerance;
            Report("Inverse", isPassed, $"maxError={maxError:F5}, tolerance={_inverseTolerance}, iterations={WaveModel.InverseIterations}");
            return isPassed;
        }

        private void GetRestPosition(int ix, int iz, int cells, Vector3 origin, out float x, out float z)
        {
            x = origin.x + (ix - cells * Half) * _cellSize;
            z = origin.z + (iz - cells * Half) * _cellSize;
        }

        private void FillVertices()
        {
            ReadOnlySpan<WaveParams> waves = _waves;
            Vector3 origin = transform.position;
            int side = _builtCells + 1;

            for (int iz = 0; iz < side; iz++)
            {
                for (int ix = 0; ix < side; ix++)
                {
                    float localX = (ix - _builtCells * Half) * _cellSize;
                    float localZ = (iz - _builtCells * Half) * _cellSize;
                    Vector3 displacement = WaveModel.DisplacementAt(waves, origin.x + localX, origin.z + localZ, _time);
                    _vertices[iz * side + ix] = new Vector3(localX + displacement.x, displacement.y, localZ + displacement.z);
                }
            }
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

        private static int[] BuildTriangles(int cells)
        {
            int side = cells + 1;
            int[] triangles = new int[cells * cells * 6];
            int index = 0;

            for (int iz = 0; iz < cells; iz++)
            {
                for (int ix = 0; ix < cells; ix++)
                {
                    int corner = iz * side + ix;
                    triangles[index++] = corner;
                    triangles[index++] = corner + side;
                    triangles[index++] = corner + 1;
                    triangles[index++] = corner + 1;
                    triangles[index++] = corner + side;
                    triangles[index++] = corner + side + 1;
                }
            }

            return triangles;
        }

        private static void Report(string testName, bool isPassed, string detail)
        {
            string message = $"[{nameof(WaveDebugMesh)}] {testName}: {(isPassed ? "PASS" : "FAIL")} ({detail})";

            if (isPassed)
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogError(message);
            }
        }
    }
}
