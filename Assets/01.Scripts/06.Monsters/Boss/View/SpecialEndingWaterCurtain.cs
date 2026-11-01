using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace Lighthouse.Monsters.Boss
{
    [DefaultExecutionOrder(20004)]
    public sealed class SpecialEndingWaterCurtain : MonoBehaviour
    {
        private static readonly int FlowTimeId = Shader.PropertyToID("_FlowTime");
        private static readonly int StrengthId = Shader.PropertyToID("_Strength");
        private static readonly int IlluminationId = Shader.PropertyToID("_Illumination");
        private const int Rows = 28;
        private const int AttachedRows = 24;
        private const int Columns = 4;
        private const int StripVertices = (Rows + 1) * (Columns + 1);
        [SerializeField] private SkinnedMeshRenderer _skin;
        [SerializeField] private float _skinOffset = 0.035f;
        [SerializeField] private float _releaseLength = 1.1f;
        [SerializeField] private Transform[] _bones;
        [SerializeField] private Vector3[] _points;
        [SerializeField] private float[] _widths;
        [SerializeField] private Camera _camera;
        [SerializeField] private PlayableDirector _director;
        [SerializeField] private SpecialEndingWaterSurfaceAnchor _water;
        [SerializeField] private MeshFilter _filter;
        [SerializeField] private MeshRenderer _renderer;
        [SerializeField] private ParticleSystem _droplets;
        [SerializeField] private AnimationCurve _strength;
        [SerializeField] private float _length = 6.5f;
        [SerializeField] private float _timeOffset;
        [SerializeField] private SpecialEndingStorm _storm;
        [SerializeField, HideInInspector] private int[] _surfaceTriangles;
        [SerializeField, HideInInspector] private Vector3[] _surfaceWeights;
        private Mesh _mesh;
        private Vector3[] _vertices;
        private MaterialPropertyBlock _properties;
        private ParticleSystem.Particle[] _particles;
        private Mesh _bakedSkin;
        private List<Vector3> _skinVertices;
        private int[] _skinTriangles;
        private bool _hasSurfaceBindings;

        private void Awake()
        {
            _vertices = new Vector3[_bones.Length * StripVertices];
            Vector2[] uv = new Vector2[_vertices.Length];
            int[] triangles = new int[_bones.Length * Rows * Columns * 6];
            for (int strip = 0; strip < _bones.Length; strip++)
            {
                for (int row = 0; row <= Rows; row++)
                {
                    for (int column = 0; column <= Columns; column++)
                    {
                        int v = strip * StripVertices + row * (Columns + 1) + column;
                        uv[v] = new Vector2(column / (float)Columns, row / (float)Rows);
                        if (row == Rows || column == Columns)
                        {
                            continue;
                        }
                        int t = ((strip * Rows + row) * Columns + column) * 6;
                        triangles[t] = v;
                        triangles[t + 1] = v + Columns + 1;
                        triangles[t + 2] = v + 1;
                        triangles[t + 3] = v + 1;
                        triangles[t + 4] = v + Columns + 1;
                        triangles[t + 5] = v + Columns + 2;
                    }
                }
            }
            _mesh = new Mesh { name = "Cinematic water curtains" };
            _mesh.MarkDynamic();
            _mesh.vertices = _vertices;
            _mesh.uv = uv;
            _mesh.triangles = triangles;
            _filter.sharedMesh = _mesh;
            _properties = new MaterialPropertyBlock();
            _particles = new ParticleSystem.Particle[_bones.Length * 8];
            if (_skin != null)
            {
                _bakedSkin = new Mesh { name = "Cinematic runoff surface" };
                _skinVertices = new List<Vector3>(_skin.sharedMesh.vertexCount);
                _hasSurfaceBindings = _surfaceTriangles != null && _surfaceWeights != null
                    && _surfaceTriangles.Length == _vertices.Length && _surfaceWeights.Length == _vertices.Length;
                if (!_hasSurfaceBindings)
                {
                    Debug.LogError($"[{nameof(SpecialEndingWaterCurtain)}] Bake surface bindings into the cinematic prefab before playback.", this);
                }
            }
        }

        private void LateUpdate()
        {
            if (_director == null || _camera == null || _water == null || (_skin != null && !_hasSurfaceBindings))
            {
                _renderer.enabled = false;
                _droplets.Clear();
                return;
            }
            float time = (float)_director.time - _timeOffset;
            float strength = _camera.enabled ? _strength.Evaluate(time) : 0f;
            float illumination = _storm != null
                ? Mathf.Lerp(0.12f, 1f, _storm.GetFlashStrength((float)_director.time)) : 1f;
            _renderer.enabled = strength > 0.005f;
            if (!_renderer.enabled)
            {
                _droplets.Clear();
                return;
            }
            float surface = _water.HasSurface ? _water.SurfaceHeight : 0f;
            if (_skin != null)
            {
                // Include the imported renderer scale so local vertices transform back to the visible skin.
                _skin.BakeMesh(_bakedSkin, true);
                _bakedSkin.GetVertices(_skinVertices);
                for (int i = 0; i < _skinVertices.Count; i++)
                {
                    _skinVertices[i] = _skin.transform.TransformPoint(_skinVertices[i]);
                }
                if (_skinTriangles == null)
                {
                    _skinTriangles = _bakedSkin.triangles;
                }
            }
            int particleCount = 0;
            for (int strip = 0; strip < _bones.Length; strip++)
            {
                Vector3 top = _bones[strip].TransformPoint(_points[strip]);
                Vector3 view = Vector3.ProjectOnPlane(_camera.transform.position - top, Vector3.up).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, view);
                Vector3 release = top;
                for (int row = 0; row <= Rows; row++)
                {
                    int index = strip * StripVertices + row * (Columns + 1);
                    for (int edge = 0; edge <= Columns; edge++)
                    {
                        Vector3 position;
                        if (row <= AttachedRows)
                        {
                            Vector3 target = top + Vector3.down * (_length * 0.72f * row / AttachedRows)
                                + side * ((edge / (float)Columns - 0.5f) * _widths[strip] * 0.72f);
                            position = GetSurfacePoint(index + edge, target, view);
                        }
                        else
                        {
                            Vector3 attached = transform.TransformPoint(_vertices[strip * StripVertices + AttachedRows * (Columns + 1) + edge]);
                            float fall = (row - AttachedRows) / (float)(Rows - AttachedRows);
                            position = attached + Vector3.down * (_releaseLength * fall)
                                + view * (0.06f * fall * fall);
                        }
                        position.y = Mathf.Max(position.y, surface - 0.05f);
                        _vertices[index + edge] = transform.InverseTransformPoint(position);
                    }
                    if (row == AttachedRows)
                    {
                        release = transform.TransformPoint(_vertices[index + Columns / 2]);
                    }
                }
                for (int drop = 0; drop < 8; drop++)
                {
                    float age = Mathf.Repeat(time + strip * 0.173f + drop * 0.153f, 1.25f);
                    float lateral = Mathf.Sin(strip * 71f + drop * 19f);
                    Vector3 position = release + side * (lateral * _widths[strip] * 0.25f)
                        + view * (age * 0.08f)
                        + Vector3.down * (age * 2f + age * age * 4.9f);
                    if (position.y < surface || top.y < surface)
                    {
                        continue;
                    }
                    _particles[particleCount].position = position;
                    _particles[particleCount].startLifetime = 2f;
                    _particles[particleCount].remainingLifetime = 1f;
                    _particles[particleCount].startSize = (0.06f + Mathf.Abs(lateral) * 0.1f) * strength;
                    _particles[particleCount].startColor = new Color(0.65f * illumination, 0.8f * illumination,
                        0.88f * illumination, strength * 0.6f);
                    particleCount++;
                }
            }
            _mesh.vertices = _vertices;
            _mesh.RecalculateBounds();
            _properties.SetFloat(FlowTimeId, time);
            _properties.SetFloat(StrengthId, strength);
            _properties.SetFloat(IlluminationId, illumination);
            _renderer.SetPropertyBlock(_properties);
            _droplets.SetParticles(_particles, particleCount);
        }

        private void BindSurface()
        {
            // Authoring-only bake: persist these bindings instead of searching every triangle during playback.
            float reach = _skin.bounds.size.magnitude;
            for (int strip = 0; strip < _bones.Length; strip++)
            {
                Vector3 top = _bones[strip].TransformPoint(_points[strip]);
                Vector3 view = Vector3.ProjectOnPlane(_camera.transform.position - top, Vector3.up).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, view);
                for (int row = 0; row <= AttachedRows; row++)
                {
                    for (int edge = 0; edge <= Columns; edge++)
                    {
                        int index = strip * StripVertices + row * (Columns + 1) + edge;
                        _surfaceTriangles[index] = -1;
                        Vector3 target = top + Vector3.down * (_length * 0.72f * row / AttachedRows)
                            + side * ((edge / (float)Columns - 0.5f) * _widths[strip] * 0.72f);
                        Vector3 origin = target + view * reach;
                        Vector3 direction = -view;
                        float nearest = reach * 2f;
                        for (int t = 0; t < _skinTriangles.Length; t += 3)
                        {
                            Vector3 a = _skinVertices[_skinTriangles[t]];
                            Vector3 b = _skinVertices[_skinTriangles[t + 1]];
                            Vector3 c = _skinVertices[_skinTriangles[t + 2]];
                            if (target.y < Mathf.Min(a.y, Mathf.Min(b.y, c.y))
                                || target.y > Mathf.Max(a.y, Mathf.Max(b.y, c.y)))
                            {
                                continue;
                            }
                            Vector3 ab = b - a;
                            Vector3 ac = c - a;
                            Vector3 p = Vector3.Cross(direction, ac);
                            float determinant = Vector3.Dot(ab, p);
                            if (Mathf.Abs(determinant) < 0.000001f)
                            {
                                continue;
                            }
                            float inverse = 1f / determinant;
                            Vector3 offset = origin - a;
                            float u = Vector3.Dot(offset, p) * inverse;
                            Vector3 q = Vector3.Cross(offset, ab);
                            float v = Vector3.Dot(direction, q) * inverse;
                            float distance = Vector3.Dot(ac, q) * inverse;
                            if (u < 0f || v < 0f || u + v > 1f || distance < 0f || distance >= nearest)
                            {
                                continue;
                            }
                            nearest = distance;
                            _surfaceTriangles[index] = t;
                            _surfaceWeights[index] = new Vector3(1f - u - v, u, v);
                        }
                    }
                }
                // Collapse uncovered samples onto the closest covered row instead of leaving floating panels.
                for (int row = 0; row <= AttachedRows; row++)
                {
                    for (int column = 0; column <= Columns; column++)
                    {
                        int index = strip * StripVertices + row * (Columns + 1) + column;
                        if (_surfaceTriangles[index] >= 0)
                        {
                            continue;
                        }
                        for (int distance = 1; distance <= AttachedRows; distance++)
                        {
                            int previous = index - distance * (Columns + 1);
                            int next = index + distance * (Columns + 1);
                            int source = -1;
                            if (row >= distance && _surfaceTriangles[previous] >= 0)
                            {
                                source = previous;
                            }
                            else if (row + distance <= AttachedRows && _surfaceTriangles[next] >= 0)
                            {
                                source = next;
                            }
                            if (source >= 0)
                            {
                                _surfaceTriangles[index] = _surfaceTriangles[source];
                                _surfaceWeights[index] = _surfaceWeights[source];
                                break;
                            }
                        }
                    }
                }
            }
        }

        private Vector3 GetSurfacePoint(int index, Vector3 fallback, Vector3 view)
        {
            if (_skin == null || _surfaceTriangles[index] < 0)
            {
                return fallback;
            }
            int triangle = _surfaceTriangles[index];
            Vector3 a = _skinVertices[_skinTriangles[triangle]];
            Vector3 b = _skinVertices[_skinTriangles[triangle + 1]];
            Vector3 c = _skinVertices[_skinTriangles[triangle + 2]];
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(normal, view) < 0f)
            {
                normal = -normal;
            }
            Vector3 weights = _surfaceWeights[index];
            return a * weights.x + b * weights.y + c * weights.z + normal * _skinOffset;
        }

        private void OnDisable()
        {
            _renderer.enabled = false;
            if (_droplets != null)
            {
                _droplets.Clear();
            }
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
            if (_bakedSkin != null)
            {
                Destroy(_bakedSkin);
            }
        }
    }
}
