using UnityEngine;

namespace Lighthouse.Player.Boat.View
{
    [DisallowMultipleComponent]
    public sealed class BoatCollisionSplashEffect : MonoBehaviour
    {
        private const int RibbonCount = 5;
        private const int RibbonSegments = 9;

        [SerializeField] private ParticleSystem _droplets;
        [SerializeField] private ParticleSystem _foam;
        [SerializeField] private ParticleSystem _streaks;
        [SerializeField] private MeshFilter _waterFan;
        [SerializeField, Min(0.1f)] private float _lifetime = 1.5f;
        [SerializeField, Min(0.1f)] private float _fanDuration = 0.45f;
        [SerializeField, Min(0f)] private float _gravity = 8f;
        [Header("Water colours")]
        [SerializeField] private Color _waterColor = new Color(0.4f, 0.58f, 0.67f, 0.82f);
        [SerializeField] private Color _waterHighlightColor = new Color(0.62f, 0.76f, 0.82f, 0.9f);
        [SerializeField] private Color _foamColor = new Color(0.72f, 0.8f, 0.83f, 0.85f);
        [SerializeField] private Color _streakColor = new Color(0.68f, 0.79f, 0.84f, 0.8f);
        [SerializeField, Range(5f, 80f)] private float _sprayHalfAngle = 28f;
        [SerializeField] private Vector2 _horizontalSpeed = new Vector2(2.4f, 4f);
        [SerializeField] private Vector2 _verticalSpeed = new Vector2(3.2f, 4.2f);
        [Header("Particle sizes")]
        [SerializeField] private Vector2 _dropletSize = new Vector2(0.035f, 0.08f);
        [SerializeField] private Vector2 _foamSize = new Vector2(0.049f, 0.112f);

        private Mesh _mesh;
        private Vector3[] _vertices;
        private Color[] _colors;
        private Vector3[] _velocities;
        private float _elapsed;
        private float _strength;
        private bool _isPlaying;

        private void Update()
        {
            if (!_isPlaying)
            {
                return;
            }

            _elapsed += Time.deltaTime;
            UpdateFan(_elapsed);
            if (_elapsed >= _lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_mesh)
            {
                Destroy(_mesh);
            }
        }

        public void Play(float strength)
        {
            _strength = Mathf.Lerp(0.55f, 1.05f, Mathf.Clamp01(strength));
            _elapsed = 0f;
            _isPlaying = true;
            CreateFan();
            EmitWater(_droplets, 78, 1f);
            EmitWater(_foam, 32, 0.32f);
            EmitWater(_streaks, 3, 0.8f);
            UpdateFan(0f);
        }

        private void EmitWater(ParticleSystem system, int count, float speedMultiplier)
        {
            if (!system)
            {
                return;
            }

            var main = system.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            system.Play(false);
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(-_sprayHalfAngle, _sprayHalfAngle) * Mathf.Deg2Rad;
                float outwardSpeed = Random.Range(_horizontalSpeed.x, _horizontalSpeed.y)
                    * _strength * speedMultiplier;
                var particle = new ParticleSystem.EmitParams
                {
                    position = new Vector3(Random.Range(-0.12f, 0.12f),
                        0.02f, Random.Range(0f, 0.08f)),
                    velocity = new Vector3(Mathf.Sin(angle) * outwardSpeed,
                        Random.Range(_verticalSpeed.x, _verticalSpeed.y) * _strength * speedMultiplier,
                        Mathf.Cos(angle) * outwardSpeed),
                    startSize = (system == _foam ? Random.Range(_foamSize.x, _foamSize.y)
                        : Random.Range(_dropletSize.x, _dropletSize.y)) * _strength,
                    startLifetime = Random.Range(0.3f, 0.65f),
                    startColor = system == _foam ? _foamColor
                        : Color.Lerp(_waterColor, _waterHighlightColor, Random.value)
                };
                if (system == _streaks)
                {
                    particle.startLifetime = 0.16f;
                    particle.startSize = 0.018f * _strength;
                    particle.startColor = _streakColor;
                }
                system.Emit(particle, 1);
            }
        }

        private void CreateFan()
        {
            if (!_waterFan || _mesh)
            {
                return;
            }

            int vertexCount = RibbonCount * RibbonSegments * 2;
            _vertices = new Vector3[vertexCount];
            _colors = new Color[vertexCount];
            _velocities = new Vector3[RibbonCount];
            var uv = new Vector2[vertexCount];
            var triangles = new int[RibbonCount * (RibbonSegments - 1) * 6];
            for (int r = 0; r < RibbonCount; r++)
            {
                float angle = Mathf.Lerp(-_sprayHalfAngle, _sprayHalfAngle,
                    r / (float)(RibbonCount - 1)) * Mathf.Deg2Rad;
                float speed = Random.Range(_horizontalSpeed.x, _horizontalSpeed.y) * _strength;
                _velocities[r] = new Vector3(Mathf.Sin(angle) * speed,
                    Random.Range(_verticalSpeed.x, _verticalSpeed.y) * _strength,
                    Mathf.Cos(angle) * speed);
                for (int s = 0; s < RibbonSegments; s++)
                {
                    int v = (r * RibbonSegments + s) * 2;
                    float along = s / (float)(RibbonSegments - 1);
                    uv[v] = new Vector2(0f, along);
                    uv[v + 1] = new Vector2(1f, along);
                    if (s == RibbonSegments - 1)
                    {
                        continue;
                    }
                    int t = (r * (RibbonSegments - 1) + s) * 6;
                    triangles[t] = v;
                    triangles[t + 1] = v + 2;
                    triangles[t + 2] = v + 1;
                    triangles[t + 3] = v + 1;
                    triangles[t + 4] = v + 2;
                    triangles[t + 5] = v + 3;
                }
            }

            _mesh = new Mesh { name = "Collision water fan" };
            _mesh.MarkDynamic();
            _mesh.vertices = _vertices;
            _mesh.uv = uv;
            _mesh.triangles = triangles;
            _mesh.bounds = new Bounds(Vector3.up, Vector3.one * 12f);
            _waterFan.sharedMesh = _mesh;
        }

        private void UpdateFan(float elapsed)
        {
            if (!_mesh)
            {
                return;
            }

            float fade = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0.12f, _fanDuration, elapsed));
            for (int r = 0; r < RibbonCount; r++)
            {
                Vector3 velocity = _velocities[r];
                Vector3 side = Vector3.Cross(Vector3.up, velocity).normalized;
                for (int s = 0; s < RibbonSegments; s++)
                {
                    float along = s / (float)(RibbonSegments - 1);
                    float age = Mathf.Max(0f, elapsed - (1f - along) * 0.09f);
                    Vector3 center = velocity * age
                        + Vector3.down * (0.5f * _gravity * age * age);
                    float width = (0.015f + Mathf.Sin(along * Mathf.PI) * 0.055f)
                        * _strength * Mathf.Clamp01(elapsed * 16f) * fade;
                    int v = (r * RibbonSegments + s) * 2;
                    _vertices[v] = center - side * width;
                    _vertices[v + 1] = center + side * width;
                    Color color = Color.Lerp(_waterColor, _waterHighlightColor, along);
                    color.a *= fade;
                    _colors[v] = color;
                    _colors[v + 1] = color;
                }
            }
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
        }
    }
}
