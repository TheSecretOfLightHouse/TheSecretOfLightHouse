using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Ocean.View;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;

namespace Lighthouse.World.Ocean.Sandbox
{
    [DefaultExecutionOrder(150)]
    public sealed class WaterF3Surf : MonoBehaviour
    {
        private const int ProfileCount = 16;
        private const float BendLimit = 1.3f;
        private static readonly ProfilerMarker UpdateMarker = new ProfilerMarker("WaterF3Surf.UpdateMesh");
        private static readonly Plane[] ViewPlanes = new Plane[6];
        private static int _viewFrame = -1;
        private static Camera _viewCamera;
        private static readonly Vector2[] Profile =
        {
            new Vector2(-1f, -0.5f), new Vector2(-0.8f, -0.15f),
            new Vector2(-0.6f, 0.2f), new Vector2(-0.4f, 0.55f),
            new Vector2(-0.2f, 0.82f), new Vector2(0f, 1f),
            new Vector2(0.2f, 1.05f), new Vector2(0.38f, 0.95f),
            new Vector2(0.52f, 0.75f), new Vector2(0.58f, 0.5f),
            new Vector2(0.57f, 0.25f), new Vector2(0.48f, 0f),
            new Vector2(0.35f, -0.15f), new Vector2(0.2f, -0.22f),
            new Vector2(0.08f, -0.16f), new Vector2(0.02f, -0.04f)
        };

        [SerializeField] private WaterSamplerHost _host;
        [SerializeField] private WaterF3Terrain _terrain;
        private bool _worldField;
        private int _worldRegion;
        [SerializeField] private MeshFilter _filter;
        [SerializeField] private ParticleSystem _spray;
        [SerializeField] private int _bands = 8;
        [SerializeField] private int _acrossCount = 321;
        [SerializeField] private float _acrossStep = 0.25f;
        [SerializeField] private Vector2 _center = new Vector2(0f, 16f);
        [SerializeField] private float _curlWidth = 0.8f;
        [SerializeField] private float _curlLift = 0.35f;
        [SerializeField] private float _breakDepth = 2f;
        [SerializeField] private float _sprayInterval = 0.05f;
        [SerializeField] private float _contactReach = 0.6f;
        [SerializeField] private LayerMask _solidLayers = ~0;
        [SerializeField] private float _updateDistance = 150f;
        [SerializeField] private int _updateInterval = 4;

        private static int s_nextSlot;

        private Mesh _mesh;
        private Bounds _bounds;
        private MeshRenderer _renderer;
        private int _slot;
        private Vector3[] _vertices;
        private Vector3[] _normals;
        private Color[] _colors;
        private Vector2[] _uv;
        private int[] _triangles;
        private int[] _profileStarts;
        [SerializeField] private bool _useParallelSampling = true;
        private float _nextSpray;
        private NativeArray<WaveParams> _jobWaves;
        private NativeArray<WaveModifierData> _jobModifiers;
        private NativeArray<Vector4> _jobPoints;
        private NativeArray<Vector4> _jobGround;

        public int ActiveCrests { get; private set; }
        public int SuppressedFreeCrests { get; private set; }
        public int ProfileResolution => ProfileCount;
        public int EmittedSpray { get; private set; }
        public double LastSolveMilliseconds { get; private set; }
        public double LastUpdateMilliseconds { get; private set; }
        public int LastUpdateFrame { get; private set; }
        public int UploadedVertices { get; private set; }

        public void InitializeWorldField(WaterSamplerHost host, Vector2 center, int region)
        {
            _host = host;
            _center = center;
            _worldField = true;
            _worldRegion = region;
        }

        private Vector4 SampleField(Vector2 point, Vector2 direction)
        {
            return _worldField
                ? F3WaveField.DirectionAt(point.x, point.y, direction)
                : _terrain.Sample(point);
        }

        private Vector4 GeometryAt(Vector2 point)
        {
            return _worldField
                ? F3WaveField.GeometryAt(F3WaveField.Geometry, F3WaveField.Layout, F3WaveField.GeometrySize,
                    point.x, point.y, F3WaveField.Regions, F3WaveField.RegionExtent)
                : _terrain.GeometryAt(point);
        }

        private bool HasGroundAt(Vector2 point)
        {
            return _worldField ? F3MapWater.HasGroundAt(point) : _terrain.HasGroundAt(point);
        }

        private void OnEnable()
        {
            if (_filter == null || _host == null || (_terrain == null && !_worldField))
            {
                return;
            }
            int count = _bands * _acrossCount * ProfileCount;
            _jobWaves = new NativeArray<WaveParams>(WaveModel.MaxWaves, Allocator.Persistent);
            _jobModifiers = new NativeArray<WaveModifierData>(WaveModifierModel.MaxModifiers, Allocator.Persistent);
            _jobPoints = new NativeArray<Vector4>(_bands * _acrossCount, Allocator.Persistent);
            _jobGround = new NativeArray<Vector4>(_bands * _acrossCount, Allocator.Persistent);
            _vertices = new Vector3[count];
            _normals = new Vector3[count];
            _colors = new Color[count];
            _uv = new Vector2[count];
            _triangles = new int[_bands * (_acrossCount - 1) * (ProfileCount - 1) * 6];
            _profileStarts = new int[_bands * _acrossCount];
            for (int i = 0; i < count; i++)
            {
                _uv[i] = Vector2.one * 0.5f;
            }
            _mesh = new Mesh { name = "F3_TravellingBreakerCrests", hideFlags = HideFlags.DontSave };
            _mesh.indexFormat = IndexFormat.UInt16;
            _mesh.MarkDynamic();
            _mesh.SetVertices(_vertices, 0, 0);
            _mesh.SetNormals(_normals, 0, 0);
            _mesh.SetColors(_colors, 0, 0);
            _mesh.SetUVs(0, _uv, 0, 0);
            _mesh.SetTriangles(System.Array.Empty<int>(), 0, false);
            _bounds = new Bounds(new Vector3(_center.x, 1f, _center.y), new Vector3(110f, 8f, 110f));
            _mesh.bounds = _bounds;
            _filter.sharedMesh = _mesh;
            _filter.TryGetComponent(out _renderer);
            _slot = s_nextSlot++;
            if (_spray != null && Application.isPlaying)
            {
                _spray.Play();
            }
            _nextSpray = Time.time;
        }

        private void LateUpdate()
        {
            if (_mesh == null || IsOutOfView())
            {
                return;
            }
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            using (UpdateMarker.Auto())
            {
                UpdateSurf();
            }
            LastUpdateFrame = Time.frameCount;
            LastUpdateMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started)
                * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        private void UpdateSurf()
        {
            if (!F3WaveField.IsActive || _host.Inputs.Waves.Length == 0)
            {
                _mesh.SetTriangles(System.Array.Empty<int>(), 0, false);
                UploadedVertices = 0;
                ActiveCrests = 0;
                if (_spray != null)
                {
                    _spray.Clear();
                }
                return;
            }
            int primary = 0;
            for (int i = 1; i < _host.Inputs.Waves.Length; i++)
            {
                if (_host.Inputs.Waves[i].Amplitude > _host.Inputs.Waves[primary].Amplitude) primary = i;
            }
            WaveParams wave = _host.Inputs.Waves[primary];
            if (wave.Wavelength <= 0f)
            {
                _mesh.SetTriangles(System.Array.Empty<int>(), 0, false);
                UploadedVertices = 0;
                ActiveCrests = 0;
                if (_spray != null)
                {
                    _spray.Clear();
                }
                return;
            }
            Vector2 direction = wave.Direction;
            Vector2 acrossDirection = new Vector2(-direction.y, direction.x);
            float time = (float)_host.CurrentTime;
            float centerAlong = Vector2.Dot(_center, direction);
            float centerAcross = Vector2.Dot(_center, acrossDirection);
            float spectralPhase = F3WaveField.PhaseOffset(wave.Wavelength) / (2f * Mathf.PI);
            int firstBand = Mathf.FloorToInt((centerAlong - wave.Speed * time) / wave.Wavelength + spectralPhase) - _bands / 2;
            bool useJob = _useParallelSampling && _worldField && F3WaveField.DirectionData.IsCreated
                && F3WaveField.DirectionInfo.IsCreated && F3WaveField.DirectionBounds.IsCreated;
            if (useJob)
            {
                PrepareSamples(primary, firstBand, centerAcross, time);
            }
            bool shouldSpray = Time.time >= _nextSpray;
            ActiveCrests = 0;
            SuppressedFreeCrests = 0;
            System.Array.Fill(_profileStarts, -1);
            int vertexCount = 0;
            for (int band = 0; band < _bands; band++)
            {
                // The crest is a moving phase from the open ocean, never a coast-distance oscillator.
                float incoming = wave.Speed * time + (firstBand + band + 0.25f - spectralPhase) * wave.Wavelength;
                for (int across = 0; across < _acrossCount; across++)
                {
                    float side = centerAcross + (across - (_acrossCount - 1) * 0.5f) * _acrossStep;
                    float along = incoming;
                    Vector2 point = direction * along + acrossDirection * side;
                    float height = 0f;
                    Vector4 geometry;
                    if (useJob)
                    {
                        int sampleIndex = band * _acrossCount + across;
                        Vector4 sample = _jobPoints[sampleIndex];
                        point = new Vector2(sample.x, sample.y);
                        geometry = _jobGround[sampleIndex];
                        height = sample.z;
                        if (sample.w <= 0f)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        // The secondary-wave bend moves the crest by at most BendLimit, so points that stay outside the
                        // breaking zone even with that slack skip the per-wave sampling.
                        Vector2 cheapPoint = point;
                        float cheapAlong = along;
                        for (int iteration = 0; iteration < 3; iteration++)
                        {
                            Vector4 cheapPath = SampleField(cheapPoint, direction);
                            float cheapOffset = direction.x * cheapPath.y + direction.y * cheapPath.z + wave.Speed * cheapPath.w;
                            cheapAlong = Mathf.Lerp(cheapAlong, incoming - cheapOffset, 0.75f);
                            cheapPoint = direction * cheapAlong + acrossDirection * side;
                        }
                        Vector2 slack = direction * BendLimit;
                        bool isFarFromBreak = IsOutsideBreak(cheapPoint) && IsOutsideBreak(cheapPoint + slack)
                            && IsOutsideBreak(cheapPoint - slack);
                        for (int iteration = 0; !isFarFromBreak && iteration < 3; iteration++)
                        {
                            Vector4 path = SampleField(point, direction);
                            float phaseOffset = direction.x * path.y + direction.y * path.z + wave.Speed * path.w;
                            float secondarySlope = 0f;
                            for (int otherIndex = 0; otherIndex < _host.Inputs.Waves.Length; otherIndex++)
                            {
                                if (otherIndex == primary) continue;
                                WaveParams other = _host.Inputs.Waves[otherIndex];
                                if (other.Wavelength <= 0f) continue;
                                Vector4 otherPath = SampleField(point, other.Direction);
                                float k = 2f * Mathf.PI / other.Wavelength;
                                float phase = k * (Vector2.Dot(other.Direction, point + new Vector2(otherPath.y, otherPath.z))
                                    - other.Speed * (time - otherPath.w)) + F3WaveField.PhaseOffset(other.Wavelength);
                                secondarySlope += other.Amplitude * F3WaveField.WavelengthTransmission(otherPath.x, other.Wavelength)
                                    * k * Vector2.Dot(other.Direction, direction) * Mathf.Cos(phase);
                            }
                            float primaryK = 2f * Mathf.PI / wave.Wavelength;
                            float bend = 1.3f * (2f / Mathf.PI) * Mathf.Atan(secondarySlope / Mathf.Max(0.01f, wave.Amplitude * primaryK * primaryK) / 1.3f);
                            along = Mathf.Lerp(along, incoming - phaseOffset + bend, 0.75f);
                            point = direction * along + acrossDirection * side;
                        }
                        if (isFarFromBreak)
                        {
                            point = cheapPoint;
                        }
                        geometry = GeometryAt(point);
                    }
                    if ((_worldField && !F3MapWater.OwnsPoint(_worldRegion, point)) || geometry.x <= 0.02f || geometry.x >= _breakDepth || geometry.y <= 0f)
                    {
                        continue;
                    }
                    float weight = Mathf.SmoothStep(0f, 1f, (_breakDepth - geometry.x) / 1.4f)
                        * Mathf.SmoothStep(0f, 1f, geometry.x / 0.2f);
                    float incomingEnergy = wave.Amplitude * F3WaveField.WavelengthTransmission(SampleField(point, direction).x, wave.Wavelength);
                    weight *= Mathf.Clamp01(incomingEnergy / 0.22f);
                    if (!useJob)
                    {
                        height = F3WaveField.HeightAt(_host.Inputs.Waves, _host.Inputs.Modifiers,
                            point.x, point.y, time);
                    }
                    float contact = ContactWeight(point, direction, height);
                    if (contact <= 0f)
                    {
                        SuppressedFreeCrests++;
                    }
                    weight *= contact;
                    if (weight <= 0.001f)
                    {
                        continue;
                    }
                    ActiveCrests++;
                    int compactStart = vertexCount;
                    _profileStarts[band * _acrossCount + across] = compactStart;
                    vertexCount += ProfileCount;
                    for (int j = 0; j < ProfileCount; j++)
                    {
                        Vector2 profile = Profile[j];
                        Vector2 position = point + direction * (profile.x * _curlWidth);
                        int index = compactStart + j;
                        Vector4 localGround = GeometryAt(position);
                        bool isWithinReach = localGround.y > 0f && localGround.w > 0f
                            && (!HasGroundAt(position) || localGround.x + height > 0.002f);
                        _vertices[index] = new Vector3(position.x, height + profile.y * _curlLift * weight, position.y);
                        Vector2 tangent = Profile[Mathf.Min(ProfileCount - 1, j + 1)]
                            - Profile[Mathf.Max(0, j - 1)];
                        Vector3 alongTangent = new Vector3(direction.x * tangent.x * _curlWidth,
                            tangent.y * _curlLift * weight, direction.y * tangent.x * _curlWidth);
                        _normals[index] = Vector3.Cross(new Vector3(acrossDirection.x, 0f, acrossDirection.y),
                            alongTangent).normalized;
                        float texture = Mathf.PerlinNoise(side * 0.9f, (firstBand + band) * 1.37f + time * 0.15f);
                        float foam = Mathf.SmoothStep(0f, 1f, ((float)j / (ProfileCount - 1) - 0.32f) / 0.28f) * Mathf.Clamp01((texture - 0.2f) * 2.5f);
                        Color color = Color.Lerp(new Color(0.015f, 0.62f, 0.6f), new Color(0.92f, 0.98f, 0.96f), foam);
                        color.a = !isWithinReach || j == 0 || j == ProfileCount - 1 ? 0f : weight * Mathf.Lerp(0.55f, 0.9f, foam);
                        _colors[index] = color;
                    }
                    if (shouldSpray && _spray != null && weight > 0.4f && across % 2 == 0)
                    {
                        float noise = Mathf.Sin(across * 12.9898f + band * 78.233f + Time.frameCount * 0.37f);
                        float foamSize = 0.055f + Mathf.Abs(noise) * 0.08f;
                        var emission = new ParticleSystem.EmitParams
                        {
                            position = new Vector3(point.x + direction.x * 0.15f,
                                height + _curlLift * weight, point.y + direction.y * 0.15f),
                            velocity = new Vector3(direction.x * 0.6f + acrossDirection.x * noise * 0.25f,
                                0.8f + Mathf.Abs(noise) * 0.7f,
                                direction.y * 0.6f + acrossDirection.y * noise * 0.25f),
                            startLifetime = 0.35f + Mathf.Abs(noise) * 0.3f,
                            startSize3D = new Vector3(foamSize, foamSize * 0.6f, foamSize * 1.2f),
                            startColor = new Color(0.95f, 1f, 0.98f, 0.65f),
                            randomSeed = unchecked((uint)(across + band * 179 + Time.frameCount * 397 + 1))
                        };
                        for (int particle = 0; particle < 3; particle++)
                        {
                            var clump = emission;
                            float offset = particle - 1f;
                            clump.position += new Vector3(acrossDirection.x, 0f, acrossDirection.y) * (offset * 0.07f);
                            clump.position += new Vector3(direction.x, 0f, direction.y) * (offset * noise * 0.06f);
                            clump.velocity += Vector3.up * (offset * 0.15f);
                            _spray.Emit(clump, 1);
                        }
                        EmittedSpray += 3;
                    }
                }
            }
            if (shouldSpray)
            {
                _nextSpray = Time.time + _sprayInterval;
            }
            if (vertexCount == 0)
            {
                if (UploadedVertices > 0)
                {
                    _mesh.SetTriangles(System.Array.Empty<int>(), 0, false);
                }
                UploadedVertices = 0;
                return;
            }
            for (int band = 0; band < _bands; band++)
            {
                for (int across = 1; across < _acrossCount - 1; across++)
                {
                    int current = _profileStarts[band * _acrossCount + across];
                    int previous = _profileStarts[band * _acrossCount + across - 1];
                    int next = _profileStarts[band * _acrossCount + across + 1];
                    if (current < 0 || previous < 0 || next < 0)
                    {
                        continue;
                    }
                    for (int j = 1; j < ProfileCount - 1; j++)
                    {
                        int index = current + j;
                        if (_colors[index].a <= 0f)
                        {
                            continue;
                        }
                        Vector3 side = _vertices[next + j] - _vertices[previous + j];
                        Vector3 front = _vertices[index + 1] - _vertices[index - 1];
                        Vector3 normal = Vector3.Cross(side, front);
                        if (normal.sqrMagnitude > 0.000001f)
                        {
                            _normals[index] = normal.normalized;
                        }
                    }
                }
            }
            // Remove the old index range before shrinking the vertex count.
            _mesh.SetTriangles(System.Array.Empty<int>(), 0, false);
            _mesh.SetVertices(_vertices, 0, vertexCount);
            _mesh.SetNormals(_normals, 0, vertexCount);
            _mesh.SetColors(_colors, 0, vertexCount);
            _mesh.SetUVs(0, _uv, 0, vertexCount);
            UploadedVertices = vertexCount;
            int triangleIndex = 0;
            for (int band = 0; band < _bands; band++)
            {
                for (int across = 0; across < _acrossCount - 1; across++)
                {
                    int current = _profileStarts[band * _acrossCount + across];
                    int next = _profileStarts[band * _acrossCount + across + 1];
                    if (current < 0 || next < 0)
                    {
                        continue;
                    }
                    for (int j = 0; j < ProfileCount - 1; j++)
                    {
                        int a = current + j;
                        int b = next + j;
                        bool canJoin = _colors[a].a > 0f && _colors[b].a > 0f
                            && _colors[a + 1].a > 0f && _colors[b + 1].a > 0f
                            && (_vertices[a] - _vertices[b]).sqrMagnitude < _acrossStep * _acrossStep * 9f;
                        if (!canJoin)
                        {
                            continue;
                        }
                        _triangles[triangleIndex++] = a;
                        _triangles[triangleIndex++] = b;
                        _triangles[triangleIndex++] = a + 1;
                        _triangles[triangleIndex++] = a + 1;
                        _triangles[triangleIndex++] = b;
                        _triangles[triangleIndex++] = b + 1;
                    }
                }
            }
            _mesh.SetTriangles(_triangles, 0, triangleIndex, 0, false);
            // Empty compact meshes must keep their spatial bounds so a later crest can become visible again.
            _mesh.bounds = _bounds;
        }

        // Crest detail beyond the update distance is not readable, so far or off-screen tiles keep their last mesh.
        private void PrepareSamples(int primary, int firstBand, float centerAcross, float time)
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int i = 0; i < _host.Inputs.Waves.Length; i++)
            {
                _jobWaves[i] = _host.Inputs.Waves[i];
            }
            for (int i = 0; i < _host.Inputs.Modifiers.Length; i++)
            {
                _jobModifiers[i] = _host.Inputs.Modifiers[i];
            }
            var job = new F3SurfSampleJob
            {
                Waves = _jobWaves, Modifiers = _jobModifiers, Cells = F3WaveField.Cells,
                Geometry = F3WaveField.Geometry, Regions = F3WaveField.Regions,
                Directions = F3WaveField.DirectionData, DirectionInfo = F3WaveField.DirectionInfo,
                PathBounds = F3WaveField.DirectionBounds,
                Points = _jobPoints, Ground = _jobGround, Layout = F3WaveField.Layout,
                Size = F3WaveField.Size, Extent = F3WaveField.RegionExtent,
                WaveCount = _host.Inputs.Waves.Length, ModifierCount = _host.Inputs.Modifiers.Length,
                Primary = primary, AcrossCount = _acrossCount, FirstBand = firstBand, Region = _worldRegion,
                AcrossStep = _acrossStep, CenterAcross = centerAcross, Time = time, BreakDepth = _breakDepth
            };
            job.Schedule(_jobPoints.Length, 32).Complete();
            LastSolveMilliseconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started)
                * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        private bool IsOutOfView()
        {
            if (_renderer == null)
            {
                return false;
            }
            if (_updateInterval > 1 && (Time.frameCount + _slot) % _updateInterval != 0)
            {
                return true;
            }
            Camera viewCamera = Camera.main;
            if (viewCamera == null)
            {
                return false;
            }
            if (_viewFrame != Time.frameCount || _viewCamera != viewCamera)
            {
                GeometryUtility.CalculateFrustumPlanes(viewCamera, ViewPlanes);
                _viewFrame = Time.frameCount;
                _viewCamera = viewCamera;
            }
            Bounds bounds = new Bounds(_filter.transform.TransformPoint(_bounds.center),
                Vector3.Scale(_bounds.size, _filter.transform.lossyScale));
            return !GeometryUtility.TestPlanesAABB(ViewPlanes, bounds)
                || bounds.SqrDistance(viewCamera.transform.position) > _updateDistance * _updateDistance;
        }

        private bool IsOutsideBreak(Vector2 point)
        {
            Vector4 geometry = GeometryAt(point);
            return (_worldField && !F3MapWater.OwnsPoint(_worldRegion, point)) || geometry.x <= 0.02f
                || geometry.x >= _breakDepth || geometry.y <= 0f;
        }

        private float ContactWeight(Vector2 point, Vector2 direction, float height)
        {
            float level = Mathf.Clamp(height * 0.25f, 0.02f, 0.12f);
            float nearest = float.PositiveInfinity;
            Vector3 forward = new Vector3(direction.x, 0f, direction.y);
            if (Physics.Raycast(new Vector3(point.x, level, point.y), forward, out RaycastHit hit,
                _contactReach, _solidLayers, QueryTriggerInteraction.Ignore))
            {
                nearest = hit.distance;
            }
            // Use the render-ground coverage too; colliders are deliberately coarser than the visual coast.
            for (int step = 1; step <= 8; step++)
            {
                float distance = _contactReach * step / 8f;
                Vector2 ahead = point + direction * distance;
                if (HasGroundAt(ahead) && -GeometryAt(ahead).x >= level)
                {
                    nearest = Mathf.Min(nearest, distance);
                    break;
                }
            }
            return float.IsPositiveInfinity(nearest) ? 0f
                : 1f - Mathf.Clamp01(Mathf.Max(0f, nearest - 0.2f) / Mathf.Max(0.01f, _contactReach - 0.2f));
        }

        private void OnDisable()
        {
            if (_jobWaves.IsCreated)
            {
                _jobWaves.Dispose();
            }
            if (_jobModifiers.IsCreated)
            {
                _jobModifiers.Dispose();
            }
            if (_jobPoints.IsCreated)
            {
                _jobPoints.Dispose();
            }
            if (_jobGround.IsCreated)
            {
                _jobGround.Dispose();
            }
            if (_spray != null)
            {
                _spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (_filter != null)
            {
                _filter.sharedMesh = null;
            }
            if (_mesh != null)
            {
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
            _renderer = null;
            _vertices = null;
            _normals = null;
            _colors = null;
            _uv = null;
            _triangles = null;
            _profileStarts = null;
        }
    }
}
