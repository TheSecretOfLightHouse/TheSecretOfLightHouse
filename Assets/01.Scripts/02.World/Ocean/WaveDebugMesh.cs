using System;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Ocean.Sandbox;
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
        private const float FiniteDifferenceStep = 0.05f;
        private const float NormalDotThreshold = 0.999f;
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
            isAllPassed &= CheckModifiers(waves);
            isAllPassed &= CheckNormals(waves);

            WaveCheckLog.Report("All", isAllPassed, "see lines above");
        }

        private ReadOnlySpan<WaveModifierData> GetModifiers()
        {
            if (!_isModifierEnabled)
            {
                return ReadOnlySpan<WaveModifierData>.Empty;
            }

            _modifierBuffer[0] = new WaveModifierData(_modifierKind, _modifierCenter, _modifierRadius, _modifierStrength, 0f);
            return _modifierBuffer;
        }

        private bool CheckValidity(ReadOnlySpan<WaveParams> waves)
        {
            float totalSteepness = 0f;
            for (int i = 0; i < waves.Length; i++)
            {
                totalSteepness += waves[i].Steepness;
            }

            bool isPassed = WaveModel.IsValid(waves);
            WaveCheckLog.Report("Validity", isPassed, $"waves={waves.Length}, totalSteepness={totalSteepness:F3}, max={WaveModel.MaxTotalSteepness}");
            return isPassed;
        }

        private bool CheckDeterminism(ReadOnlySpan<WaveParams> waves)
        {
            ReadOnlySpan<WaveModifierData> modifiers = GetModifiers();
            int cells = Mathf.Clamp(_gridCells, MinGridCells, MaxGridCells);
            Vector3 origin = transform.position;
            int mismatches = 0;

            for (int iz = 0; iz <= cells; iz++)
            {
                for (int ix = 0; ix <= cells; ix++)
                {
                    GetRestPosition(ix, iz, cells, origin, out float x, out float z);

                    float heightA = WaveModel.HeightAt(waves, modifiers, x, z, _time);
                    float heightB = WaveModel.HeightAt(waves, modifiers, x, z, _time);
                    Vector3 normalA = WaveModel.NormalAt(waves, modifiers, x, z, _time);
                    Vector3 normalB = WaveModel.NormalAt(waves, modifiers, x, z, _time);

                    bool isHeightSame = heightA == heightB;
                    bool isNormalSame = normalA.x == normalB.x && normalA.y == normalB.y && normalA.z == normalB.z;

                    if (!isHeightSame || !isNormalSame)
                    {
                        mismatches++;
                    }
                }
            }

            bool isPassed = mismatches == 0;
            WaveCheckLog.Report("Determinism", isPassed, $"mismatches={mismatches}, modifier={_isModifierEnabled}");
            return isPassed;
        }

        private bool CheckEmptyWaves()
        {
            ReadOnlySpan<WaveParams> none = ReadOnlySpan<WaveParams>.Empty;
            Vector3 origin = transform.position;

            float height = WaveModel.HeightAt(none, ReadOnlySpan<WaveModifierData>.Empty, origin.x + 3f, origin.z - 7f, _time);
            Vector3 normal = WaveModel.NormalAt(none, ReadOnlySpan<WaveModifierData>.Empty, origin.x + 3f, origin.z - 7f, _time);

            bool isPassed = Mathf.Approximately(height, 0f) && Mathf.Approximately(normal.y, 1f);
            WaveCheckLog.Report("EmptyWaves", isPassed, $"height={height}, normal={normal}");
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
            WaveCheckLog.Report("AmplitudeBound", isPassed, $"maxAbsHeight={maxAbsHeight:F4}, amplitudeSum={amplitudeSum:F4}");
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
            WaveCheckLog.Report("Inverse", isPassed, $"maxError={maxError:F5}, tolerance={_inverseTolerance}, iterations={WaveModel.InverseIterations}");
            return isPassed;
        }

        private bool CheckModifiers(ReadOnlySpan<WaveParams> waves)
        {
            Vector3 origin = transform.position;
            Vector2 center = new Vector2(origin.x, origin.z);
            WaveModifierData[] bump = { new WaveModifierData(WaveModifierKind.Bump, center, _modifierRadius, _modifierStrength, 0f) };
            WaveModifierData[] vortex = { new WaveModifierData(WaveModifierKind.Vortex, center, _modifierRadius, _modifierStrength, 0f) };
            ReadOnlySpan<WaveModifierData> none = ReadOnlySpan<WaveModifierData>.Empty;

            float baseCenter = WaveModel.HeightAt(waves, none, center.x, center.y, _time);
            float bumpDelta = WaveModel.HeightAt(waves, bump, center.x, center.y, _time) - baseCenter;
            float vortexDelta = WaveModel.HeightAt(waves, vortex, center.x, center.y, _time) - baseCenter;
            bool isCenterOk = Mathf.Abs(bumpDelta - _modifierStrength) <= BoundEpsilon
                && Mathf.Abs(vortexDelta + _modifierStrength) <= BoundEpsilon;

            int cells = Mathf.Clamp(_gridCells, MinGridCells, MaxGridCells);
            float radiusSqr = _modifierRadius * _modifierRadius;
            int outsideMismatches = 0;
            int insideMismatches = 0;
            int outsideCount = 0;

            for (int iz = 0; iz <= cells; iz++)
            {
                for (int ix = 0; ix <= cells; ix++)
                {
                    GetRestPosition(ix, iz, cells, origin, out float x, out float z);

                    float baseHeight = WaveModel.HeightAt(waves, none, x, z, _time);
                    float bumpOffset = WaveModel.HeightAt(waves, bump, x, z, _time) - baseHeight;
                    float vortexOffset = WaveModel.HeightAt(waves, vortex, x, z, _time) - baseHeight;

                    float offsetX = x - center.x;
                    float offsetZ = z - center.y;
                    bool isOutside = offsetX * offsetX + offsetZ * offsetZ >= radiusSqr;

                    if (isOutside)
                    {
                        outsideCount++;
                        if (bumpOffset != 0f || vortexOffset != 0f)
                        {
                            outsideMismatches++;
                        }
                    }
                    else if (bumpOffset < -BoundEpsilon || vortexOffset > BoundEpsilon)
                    {
                        insideMismatches++;
                    }
                }
            }

            WaveModifierData[] tooMany = new WaveModifierData[WaveModifierModel.MaxModifiers + 1];
            bool isValidityOk = WaveModifierModel.AreValid(bump) && !WaveModifierModel.AreValid(tooMany);

            bool isPassed = isCenterOk && outsideMismatches == 0 && insideMismatches == 0 && isValidityOk;
            WaveCheckLog.Report(
                "Modifiers",
                isPassed,
                $"bumpDelta={bumpDelta:F4}, vortexDelta={vortexDelta:F4}, outside={outsideCount} (mismatch {outsideMismatches}), insideMismatch={insideMismatches}, validity={isValidityOk}");
            return isPassed;
        }

        private bool CheckNormals(ReadOnlySpan<WaveParams> waves)
        {
            Vector3 origin = transform.position;
            Vector2 center = new Vector2(origin.x, origin.z);
            WaveModifierData[] bump = { new WaveModifierData(WaveModifierKind.Bump, center, _modifierRadius, _modifierStrength, 0f) };

            float minDotWaves = MinNormalDot(waves, ReadOnlySpan<WaveModifierData>.Empty);
            float minDotBump = MinNormalDot(waves, bump);

            bool isPassed = minDotWaves >= NormalDotThreshold && minDotBump >= NormalDotThreshold;
            WaveCheckLog.Report("Normals", isPassed, $"minDot(waves)={minDotWaves:F5}, minDot(waves+bump)={minDotBump:F5}, threshold={NormalDotThreshold}");
            return isPassed;
        }

        private float MinNormalDot(ReadOnlySpan<WaveParams> waves, ReadOnlySpan<WaveModifierData> modifiers)
        {
            int cells = Mathf.Clamp(_gridCells, MinGridCells, MaxGridCells);
            Vector3 origin = transform.position;
            float doubleStep = FiniteDifferenceStep * 2f;
            float minDot = 1f;

            for (int iz = 0; iz <= cells; iz++)
            {
                for (int ix = 0; ix <= cells; ix++)
                {
                    GetRestPosition(ix, iz, cells, origin, out float x, out float z);

                    float slopeX = (WaveModel.HeightAt(waves, modifiers, x + FiniteDifferenceStep, z, _time)
                        - WaveModel.HeightAt(waves, modifiers, x - FiniteDifferenceStep, z, _time)) / doubleStep;
                    float slopeZ = (WaveModel.HeightAt(waves, modifiers, x, z + FiniteDifferenceStep, _time)
                        - WaveModel.HeightAt(waves, modifiers, x, z - FiniteDifferenceStep, _time)) / doubleStep;

                    Vector3 expected = new Vector3(-slopeX, 1f, -slopeZ).normalized;
                    Vector3 actual = WaveModel.NormalAt(waves, modifiers, x, z, _time);
                    minDot = Mathf.Min(minDot, Vector3.Dot(actual, expected));
                }
            }

            return minDot;
        }

        private void GetRestPosition(int ix, int iz, int cells, Vector3 origin, out float x, out float z)
        {
            x = origin.x + (ix - cells * Half) * _cellSize;
            z = origin.z + (iz - cells * Half) * _cellSize;
        }

        private void FillVertices()
        {
            ReadOnlySpan<WaveParams> waves = _waves;
            ReadOnlySpan<WaveModifierData> modifiers = GetModifiers();
            Vector3 origin = transform.position;
            int side = _builtCells + 1;

            for (int iz = 0; iz < side; iz++)
            {
                for (int ix = 0; ix < side; ix++)
                {
                    float localX = (ix - _builtCells * Half) * _cellSize;
                    float localZ = (iz - _builtCells * Half) * _cellSize;
                    Vector3 displacement = WaveModel.DisplacementAt(waves, origin.x + localX, origin.z + localZ, _time);
                    float worldX = origin.x + localX + displacement.x;
                    float worldZ = origin.z + localZ + displacement.z;
                    float height = displacement.y + WaveModifierModel.HeightAt(modifiers, worldX, worldZ);
                    _vertices[iz * side + ix] = new Vector3(localX + displacement.x, height, localZ + displacement.z);
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
    }
}
