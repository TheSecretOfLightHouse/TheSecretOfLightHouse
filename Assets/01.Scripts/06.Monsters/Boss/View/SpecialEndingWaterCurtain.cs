using UnityEngine;
using UnityEngine.Playables;

namespace Lighthouse.Monsters.Boss
{
    [DefaultExecutionOrder(20004)]
    public sealed class SpecialEndingWaterCurtain : MonoBehaviour
    {
        private static readonly int FlowTimeId = Shader.PropertyToID("_FlowTime");
        private static readonly int StrengthId = Shader.PropertyToID("_Strength");
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
        private Mesh _mesh;
        private Vector3[] _vertices;
        private MaterialPropertyBlock _properties;
        private ParticleSystem.Particle[] _particles;

        private void Awake()
        {
            const int rows = 12;
            _vertices = new Vector3[_bones.Length * (rows + 1) * 2];
            Vector2[] uv = new Vector2[_vertices.Length];
            int[] triangles = new int[_bones.Length * rows * 6];
            for (int strip = 0; strip < _bones.Length; strip++)
            {
                for (int row = 0; row <= rows; row++)
                {
                    int v = strip * (rows + 1) * 2 + row * 2;
                    uv[v] = new Vector2(0f, row / (float)rows);
                    uv[v + 1] = new Vector2(1f, row / (float)rows);
                    if (row == rows)
                    {
                        continue;
                    }
                    int t = (strip * rows + row) * 6;
                    triangles[t] = v;
                    triangles[t + 1] = v + 2;
                    triangles[t + 2] = v + 1;
                    triangles[t + 3] = v + 1;
                    triangles[t + 4] = v + 2;
                    triangles[t + 5] = v + 3;
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
        }

        private void LateUpdate()
        {
            if (_director == null || _camera == null || _water == null)
            {
                _renderer.enabled = false;
                return;
            }
            float time = (float)_director.time;
            float strength = _camera.enabled ? _strength.Evaluate(time) : 0f;
            _renderer.enabled = strength > 0.005f;
            if (!_renderer.enabled)
            {
                _droplets.Clear();
                return;
            }
            float surface = _water.HasSurface ? _water.SurfaceHeight : 0f;
            int particleCount = 0;
            for (int strip = 0; strip < _bones.Length; strip++)
            {
                Vector3 top = _bones[strip].TransformPoint(_points[strip]);
                Vector3 outward = Vector3.ProjectOnPlane(top - _water.transform.position, Vector3.up).normalized;
                Vector3 view = Vector3.ProjectOnPlane(_camera.transform.position - top, Vector3.up).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, view);
                float length = Mathf.Clamp(top.y - surface, 0f, _length * Mathf.Lerp(0.65f, 1f, strength));
                for (int drop = 0; drop < 8; drop++)
                {
                    float age = Mathf.Repeat(time + strip * 0.173f + drop * 0.153f, 1.25f);
                    float lateral = Mathf.Sin(strip * 71f + drop * 19f);
                    Vector3 position = top + side * (lateral * _widths[strip] * 0.45f)
                        + outward * (0.3f + age * 0.4f)
                        + Vector3.down * (age * 2f + age * age * 4.9f);
                    if (position.y < surface || top.y < surface)
                    {
                        continue;
                    }
                    _particles[particleCount].position = position;
                    _particles[particleCount].startLifetime = 2f;
                    _particles[particleCount].remainingLifetime = 1f;
                    _particles[particleCount].startSize = (0.06f + Mathf.Abs(lateral) * 0.1f) * strength;
                    _particles[particleCount].startColor = new Color(0.65f, 0.8f, 0.88f, strength * 0.6f);
                    particleCount++;
                }
                for (int row = 0; row <= 12; row++)
                {
                    float v = row / 12f;
                    Vector3 center = top + outward * (0.12f + v * v * 0.8f)
                        + Vector3.down * (length * v);
                    float width = _widths[strip] * (1f - v * 0.35f)
                        * (1f + Mathf.Sin(v * 15f - time * 5f + strip) * 0.12f);
                    int index = strip * 26 + row * 2;
                    _vertices[index] = transform.InverseTransformPoint(center - side * width * 0.5f);
                    _vertices[index + 1] = transform.InverseTransformPoint(center + side * width * 0.5f);
                }
            }
            _mesh.vertices = _vertices;
            _mesh.RecalculateBounds();
            _properties.SetFloat(FlowTimeId, time);
            _properties.SetFloat(StrengthId, strength);
            _renderer.SetPropertyBlock(_properties);
            _droplets.SetParticles(_particles, particleCount);
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
        }
    }
}
