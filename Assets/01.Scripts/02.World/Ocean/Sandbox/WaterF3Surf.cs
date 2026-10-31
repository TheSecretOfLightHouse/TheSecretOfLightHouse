using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Ocean.View;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lighthouse.World.Ocean.Sandbox
{
    [DefaultExecutionOrder(150)]
    public sealed class WaterF3Surf : MonoBehaviour
    {
        private const int ProfileCount = 16;
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

        private Mesh _mesh;
        private Vector3[] _vertices;
        private Vector3[] _normals;
        private Color[] _colors;
        private Vector2[] _uv;
        private int[] _triangles;
        private float _nextSpray;

        public int ActiveCrests { get; private set; }
        public int SuppressedFreeCrests { get; private set; }
        public int ProfileResolution => ProfileCount;
        public int EmittedSpray { get; private set; }

        public void InitializeWorldField(WaterSamplerHost host, Vector2 center, int region)
        {
            _host = host;
            _center = center;
            _worldField = true;
            _worldRegion = region;
        }

        private Vector4 SampleField(Vector2 point)
        {
            return _worldField
                ? F3WaveField.Sample(F3WaveField.Cells, F3WaveField.Layout, F3WaveField.Size,
                    point.x, point.y, F3WaveField.Geometry, F3WaveField.GeometrySize, F3WaveField.Regions, F3WaveField.RegionExtent)
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
            _vertices = new Vector3[count];
            _normals = new Vector3[count];
            _colors = new Color[count];
            _uv = new Vector2[count];
            int[] triangles = new int[_bands * (_acrossCount - 1) * (ProfileCount - 1) * 6];
            _triangles = triangles;
            int cursor = 0;
            for (int band = 0; band < _bands; band++)
            {
                for (int across = 0; across < _acrossCount - 1; across++)
                {
                    for (int profile = 0; profile < ProfileCount - 1; profile++)
                    {
                        int a = (band * _acrossCount + across) * ProfileCount + profile;
                        int b = a + ProfileCount;
                        triangles[cursor++] = a;
                        triangles[cursor++] = b;
                        triangles[cursor++] = a + 1;
                        triangles[cursor++] = a + 1;
                        triangles[cursor++] = b;
                        triangles[cursor++] = b + 1;
                    }
                }
            }
            for (int i = 0; i < count; i++)
            {
                _uv[i] = Vector2.one * 0.5f;
            }
            _mesh = new Mesh { name = "F3_TravellingBreakerCrests", hideFlags = HideFlags.DontSave };
            _mesh.indexFormat = IndexFormat.UInt16;
            _mesh.MarkDynamic();
            _mesh.vertices = _vertices;
            _mesh.normals = _normals;
            _mesh.colors = _colors;
            _mesh.uv = _uv;
            _mesh.triangles = triangles;
            _mesh.bounds = new Bounds(new Vector3(_center.x, 1f, _center.y), new Vector3(110f, 8f, 110f));
            _filter.sharedMesh = _mesh;
            if (_spray != null && Application.isPlaying)
            {
                _spray.Play();
            }
            _nextSpray = Time.time;
        }

        private void LateUpdate()
        {
            if (_mesh == null)
            {
                return;
            }
            if (!F3WaveField.IsActive || _host.Inputs.Waves.Length == 0)
            {
                System.Array.Clear(_colors, 0, _colors.Length);
                _mesh.SetColors(_colors);
                ActiveCrests = 0;
                if (_spray != null)
                {
                    _spray.Clear();
                }
                return;
            }
            WaveParams wave = _host.Inputs.Waves[0];
            if (wave.Wavelength <= 0f)
            {
                System.Array.Clear(_colors, 0, _colors.Length);
                _mesh.SetColors(_colors);
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
            int firstBand = Mathf.FloorToInt((centerAlong - wave.Speed * time) / wave.Wavelength) - _bands / 2;
            bool shouldSpray = Time.time >= _nextSpray;
            ActiveCrests = 0;
            SuppressedFreeCrests = 0;
            for (int band = 0; band < _bands; band++)
            {
                // The crest is a moving phase from the open ocean, never a coast-distance oscillator.
                float incoming = wave.Speed * time + (firstBand + band + 0.25f) * wave.Wavelength;
                for (int across = 0; across < _acrossCount; across++)
                {
                    float side = centerAcross + (across - (_acrossCount - 1) * 0.5f) * _acrossStep;
                    float along = incoming;
                    Vector2 point = direction * along + acrossDirection * side;
                    for (int iteration = 0; iteration < 3; iteration++)
                    {
                        Vector4 path = SampleField(point);
                        float phaseOffset = direction.x * path.y + direction.y * path.z + wave.Speed * path.w;
                        float secondarySlope = 0f;
                        for (int otherIndex = 1; otherIndex < _host.Inputs.Waves.Length; otherIndex++)
                        {
                            WaveParams other = _host.Inputs.Waves[otherIndex];
                            float k = 2f * Mathf.PI / other.Wavelength;
                            float phase = k * (Vector2.Dot(other.Direction, point + new Vector2(path.y, path.z))
                                - other.Speed * (time - path.w));
                            secondarySlope += other.Amplitude * k * Vector2.Dot(other.Direction, direction) * Mathf.Cos(phase);
                        }
                        float primaryK = 2f * Mathf.PI / wave.Wavelength;
                        float bend = 1.3f * (2f / Mathf.PI) * Mathf.Atan(secondarySlope / Mathf.Max(0.01f, wave.Amplitude * primaryK * primaryK) / 1.3f);
                        along = Mathf.Lerp(along, incoming - phaseOffset + bend, 0.75f);
                        point = direction * along + acrossDirection * side;
                    }
                    Vector4 geometry = GeometryAt(point);
                    int start = (band * _acrossCount + across) * ProfileCount;
                    if ((_worldField && !F3MapWater.OwnsPoint(_worldRegion, point)) || geometry.x <= 0.02f || geometry.x >= _breakDepth || geometry.y <= 0f)
                    {
                        for (int j = 0; j < ProfileCount; j++)
                        {
                            _colors[start + j] = Color.clear;
                            _vertices[start + j] = new Vector3(point.x, 0f, point.y);
                            _normals[start + j] = Vector3.up;
                        }
                        continue;
                    }
                    float weight = Mathf.SmoothStep(0f, 1f, (_breakDepth - geometry.x) / 1.4f)
                        * Mathf.SmoothStep(0f, 1f, geometry.x / 0.2f);
                    float incomingEnergy = wave.Amplitude * SampleField(point).x;
                    weight *= Mathf.Clamp01(incomingEnergy / 0.22f);
                    float height = F3WaveField.HeightAt(_host.Inputs.Waves, _host.Inputs.Modifiers,
                        point.x, point.y, time);
                    float contact = ContactWeight(point, direction, height);
                    if (contact <= 0f)
                    {
                        SuppressedFreeCrests++;
                    }
                    weight *= contact;
                    if (weight <= 0.001f)
                    {
                        for (int j = 0; j < ProfileCount; j++)
                        {
                            _colors[start + j] = Color.clear;
                            _vertices[start + j] = new Vector3(point.x, 0f, point.y);
                            _normals[start + j] = Vector3.up;
                        }
                        continue;
                    }
                    ActiveCrests++;
                    for (int j = 0; j < ProfileCount; j++)
                    {
                        Vector2 profile = Profile[j];
                        Vector2 position = point + direction * (profile.x * _curlWidth);
                        int index = start + j;
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
            for (int band = 0; band < _bands; band++)
            {
                for (int across = 1; across < _acrossCount - 1; across++)
                {
                    for (int j = 1; j < ProfileCount - 1; j++)
                    {
                        int index = (band * _acrossCount + across) * ProfileCount + j;
                        if (_colors[index].a <= 0f)
                        {
                            continue;
                        }
                        Vector3 side = _vertices[index + ProfileCount] - _vertices[index - ProfileCount];
                        Vector3 front = _vertices[index + 1] - _vertices[index - 1];
                        Vector3 normal = Vector3.Cross(side, front);
                        if (normal.sqrMagnitude > 0.000001f)
                        {
                            _normals[index] = normal.normalized;
                        }
                    }
                }
            }
            _mesh.SetVertices(_vertices);
            _mesh.SetNormals(_normals);
            _mesh.SetColors(_colors);
            int triangleIndex = 0;
            for (int band = 0; band < _bands; band++)
            {
                for (int across = 0; across < _acrossCount - 1; across++)
                {
                    for (int j = 0; j < ProfileCount - 1; j++)
                    {
                        int a = (band * _acrossCount + across) * ProfileCount + j;
                        int b = a + ProfileCount;
                        bool canJoin = _colors[a].a > 0f && _colors[b].a > 0f
                            && _colors[a + 1].a > 0f && _colors[b + 1].a > 0f
                            && (_vertices[a] - _vertices[b]).sqrMagnitude < _acrossStep * _acrossStep * 9f;
                        _triangles[triangleIndex++] = canJoin ? a : 0;
                        _triangles[triangleIndex++] = canJoin ? b : 0;
                        _triangles[triangleIndex++] = canJoin ? a + 1 : 0;
                        _triangles[triangleIndex++] = canJoin ? a + 1 : 0;
                        _triangles[triangleIndex++] = canJoin ? b : 0;
                        _triangles[triangleIndex++] = canJoin ? b + 1 : 0;
                    }
                }
            }
            _mesh.SetTriangles(_triangles, 0, false);
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
            _vertices = null;
            _normals = null;
            _colors = null;
            _uv = null;
            _triangles = null;
        }
    }
}
