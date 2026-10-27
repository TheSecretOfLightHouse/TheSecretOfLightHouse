using System;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.View
{
    public sealed class SurfaceInteractionGrid : IDisposable
    {
        public const int Resolution = 128;
        public const float CellSize = 0.25f;
        private const float Step = 1f / 60f;
        private const int ContactCapacity = 128;
        private static readonly int StateId = Shader.PropertyToID("_OceanInteractionState");
        private static readonly int AreaId = Shader.PropertyToID("_OceanInteractionArea");
        private static readonly int EnabledId = Shader.PropertyToID("_OceanInteractionEnabled");
        private readonly ComputeShader _solver;
        private readonly RenderTexture[] _states = new RenderTexture[2];
        private readonly RenderTexture _velocity;
        private readonly RenderTexture _background;
        private readonly RenderTexture _domain;
        private readonly Texture2D _obstacles;
        private readonly Color[] _pixels = new Color[Resolution * Resolution];
        private readonly Collider[] _colliders = new Collider[1024];
        private readonly Contact[] _contacts = new Contact[ContactCapacity];
        private readonly Vector4[] _waveA = new Vector4[WaveModel.MaxWaves];
        private readonly Vector4[] _waveB = new Vector4[WaveModel.MaxWaves];
        private readonly Vector4[] _modifierA = new Vector4[WaveModifierModel.MaxModifiers];
        private readonly Vector4[] _modifierB = new Vector4[WaveModifierModel.MaxModifiers];
        private readonly Vector4[] _modifierC = new Vector4[WaveModifierModel.MaxModifiers];
        private readonly Vector4[] _coastProfiles = new Vector4[CoastWaveRegistry.Capacity];
        private readonly Vector4[] _coasts = new Vector4[CoastWaveRegistry.Capacity];
        private readonly Vector4[] _edges = new Vector4[CoastWaveRegistry.Capacity * CoastWaveRegistry.Segments];
        private readonly int _prepare;
        private readonly int _flow;
        private readonly int _integrate;
        private readonly int _shift;
        private Vector2 _origin;
        private float _accumulator;
        private float _nextScan;
        private bool _initialized;
        private int _current;
        private int _coastVersion = -1;

        private struct Contact
        {
            public int Id;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Seen;
        }

        public float StaticContactFoam { get; set; }
        public int ObstacleCount { get; private set; }
        public int UnsupportedMeshCount { get; private set; }
        public int SaturatedScanCount { get; private set; }
        public int StepCount { get; private set; }
        public RenderTexture State => _states[_current];
        public Vector2 Origin => _origin;
        public ComputeShader Simulation => _solver;
        public RenderTexture Background => _background;
        public RenderTexture Domain => _domain;
        public Texture2D Obstacles => _obstacles;

        public SurfaceInteractionGrid(ComputeShader solver)
        {
            _solver = UnityEngine.Object.Instantiate(solver);
            _solver.hideFlags = HideFlags.DontSave;
            _prepare = _solver.FindKernel("Prepare");
            _flow = _solver.FindKernel("Flow");
            _integrate = _solver.FindKernel("Integrate");
            _shift = _solver.FindKernel("Shift");
            _states[0] = CreateTexture("WaterInteraction_StateA");
            _states[1] = CreateTexture("WaterInteraction_StateB");
            _velocity = CreateTexture("WaterInteraction_Flow");
            _background = CreateTexture("WaterInteraction_Background");
            _domain = CreateTexture("WaterInteraction_Domain");
            _obstacles = new Texture2D(Resolution, Resolution, TextureFormat.RGBAFloat, false, true)
            {
                name = "WaterInteraction_Obstacles", hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            _solver.SetInt("_Resolution", Resolution);
            _solver.SetFloat("_CellSize", CellSize);
            _solver.SetFloat("_Step", Step);
            Clear(_states[0]);
            Clear(_states[1]);
        }

        public void Update(Vector3 focus, float seaLevel, float deltaTime, float now,
            WaterSamplerHost host, float waveTime)
        {
            var waves = host.Inputs.Waves;
            float waveEnergy = 0f;
            for (int i = 0; i < waves.Length; i++)
            {
                WaveParams wave = waves[i];
                waveEnergy += wave.Amplitude;
                _waveA[i] = new Vector4(wave.Direction.x, wave.Direction.y, wave.Wavelength, wave.Speed);
                _waveB[i] = new Vector4(wave.Amplitude, wave.Steepness, 0f, 0f);
            }
            _solver.SetVectorArray("_OceanWaveA", _waveA);
            _solver.SetVectorArray("_OceanWaveB", _waveB);
            _solver.SetFloat("_OceanWaveCount", waves.Length);
            _solver.SetFloat("_WaveEnergy", waveEnergy);
            _solver.SetFloat("_StaticContactFoam", StaticContactFoam);
            _solver.SetFloat("_OceanTime", waveTime);
            var modifiers = host.Inputs.Modifiers;
            for (int i = 0; i < modifiers.Length; i++)
            {
                WaveModifierData modifier = modifiers[i];
                _modifierA[i] = new Vector4(modifier.Center.x, modifier.Center.y, modifier.Radius, modifier.Strength);
                _modifierB[i] = new Vector4((int)modifier.Kind, modifier.StartTime, modifier.BandWidth, modifier.Wavelength);
                _modifierC[i] = new Vector4(modifier.Speed, 0f, 0f, 0f);
            }
            _solver.SetVectorArray("_OceanModifierA", _modifierA);
            _solver.SetVectorArray("_OceanModifierB", _modifierB);
            _solver.SetVectorArray("_OceanModifierC", _modifierC);
            _solver.SetFloat("_OceanModifierCount", modifiers.Length);
            if (_coastVersion != CoastWaveRegistry.Version)
            {
                CoastWaveRegistry.Bounds.CopyTo(_coasts);
                CoastWaveRegistry.Profiles.CopyTo(_coastProfiles);
                CoastWaveRegistry.Edges.CopyTo(_edges);
                _solver.SetVectorArray("_OceanCoastBounds", _coasts);
                _solver.SetVectorArray("_OceanCoastProfiles", _coastProfiles);
                _solver.SetVectorArray("_OceanCoastEdges", _edges);
                _coastVersion = CoastWaveRegistry.Version;
            }
            _solver.SetFloat("_OceanCoastEnabled", 1f);
            Vector2 target = new Vector2(
                Mathf.Floor(focus.x / (CellSize * 8)) * CellSize * 8 - Resolution * CellSize * 0.5f,
                Mathf.Floor(focus.z / (CellSize * 8)) * CellSize * 8 - Resolution * CellSize * 0.5f);
            bool moved = !_initialized || target != _origin;
            if (moved)
            {
                int dx = Mathf.RoundToInt((target.x - _origin.x) / CellSize);
                int dz = Mathf.RoundToInt((target.y - _origin.y) / CellSize);
                if (!_initialized || Mathf.Abs(dx) >= Resolution || Mathf.Abs(dz) >= Resolution)
                {
                    Clear(_states[0]);
                    Clear(_states[1]);
                }
                else
                {
                    _solver.SetInts("_Shift", dx, dz);
                    _solver.SetTexture(_shift, "_Read", State);
                    _solver.SetTexture(_shift, "_Write", _states[1 - _current]);
                    _solver.Dispatch(_shift, Resolution / 8, Resolution / 8, 1);
                    _current = 1 - _current;
                }
                _origin = target;
                _initialized = true;
            }
            if (moved || now >= _nextScan)
            {
                Rasterize(seaLevel, now);
                _nextScan = now + 0.05f;
            }
            _solver.SetVector("_Origin", new Vector4(_origin.x, _origin.y, 0f, 0f));
            _solver.SetTexture(_prepare, "_Obstacles", _obstacles);
            _solver.SetTexture(_prepare, "_BackgroundWrite", _background);
            _solver.SetTexture(_prepare, "_DomainWrite", _domain);
            _solver.Dispatch(_prepare, Resolution / 8, Resolution / 8, 1);
            _accumulator = Mathf.Min(_accumulator + deltaTime, Step * 4);
            while (_accumulator >= Step)
            {
                _solver.SetTexture(_flow, "_Read", State);
                _solver.SetTexture(_flow, "_Write", _velocity);
                _solver.SetTexture(_flow, "_Background", _background);
                _solver.SetTexture(_flow, "_Domain", _domain);
                _solver.Dispatch(_flow, Resolution / 8, Resolution / 8, 1);
                _solver.SetTexture(_integrate, "_Read", _velocity);
                _solver.SetTexture(_integrate, "_Write", _states[1 - _current]);
                _solver.SetTexture(_integrate, "_Background", _background);
                _solver.SetTexture(_integrate, "_Domain", _domain);
                _solver.Dispatch(_integrate, Resolution / 8, Resolution / 8, 1);
                _current = 1 - _current;
                _accumulator -= Step;
                StepCount++;
            }
            Shader.SetGlobalTexture(StateId, State);
            Shader.SetGlobalVector(AreaId, new Vector4(_origin.x, _origin.y, Resolution * CellSize, CellSize));
            Shader.SetGlobalFloat(EnabledId, 1f);
        }

        public void Dispose()
        {
            Shader.SetGlobalFloat(EnabledId, 0f);
            for (int i = 0; i < _states.Length; i++)
            {
                Release(_states[i]);
            }
            Release(_velocity);
            Release(_background);
            Release(_domain);
            UnityEngine.Object.Destroy(_obstacles);
            UnityEngine.Object.Destroy(_solver);
        }

        private void Rasterize(float seaLevel, float now)
        {
            Array.Clear(_pixels, 0, _pixels.Length);
            float half = Resolution * CellSize * 0.5f;
            Vector3 center = new Vector3(_origin.x + half, seaLevel, _origin.y + half);
            int count = Physics.OverlapBoxNonAlloc(center, new Vector3(half, 1f, half),
                _colliders, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            if (count == _colliders.Length)
            {
                SaturatedScanCount++;
            }
            ObstacleCount = 0;
            UnsupportedMeshCount = 0;
            for (int i = 0; i < count; i++)
            {
                Collider collider = _colliders[i];
                Rigidbody body = collider.attachedRigidbody;
                if (collider.isTrigger)
                {
                    continue;
                }
                if (collider is MeshCollider mesh && !mesh.convex)
                {
                    // Registered coast contours provide land boundaries without treating seabed as solid water.
                    UnsupportedMeshCount++;
                    continue;
                }
                if (!(collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider
                    || collider is MeshCollider))
                {
                    continue;
                }
                Bounds bounds = collider.bounds;
                if (bounds.min.y > seaLevel + 0.03f || bounds.max.y < seaLevel - 0.03f)
                {
                    continue;
                }
                int slot = FindContact(collider.GetInstanceID(), now);
                Vector3 velocity = Vector3.zero;
                Vector3 angular = Vector3.zero;
                Quaternion rotation = collider.transform.rotation;
                if (slot >= 0)
                {
                    Contact previous = _contacts[slot];
                    if (previous.Id == collider.GetInstanceID() && now > previous.Seen)
                    {
                        velocity = (bounds.center - previous.Position) / (now - previous.Seen);
                        Quaternion delta = rotation * Quaternion.Inverse(previous.Rotation);
                        delta.ToAngleAxis(out float angle, out Vector3 axis);
                        if (angle > 180f)
                        {
                            angle -= 360f;
                        }
                        if (axis.sqrMagnitude > 0.0001f && Mathf.Abs(angle) > 0.001f)
                        {
                            angular = axis * (angle * Mathf.Deg2Rad / (now - previous.Seen));
                        }
                    }
                    _contacts[slot] = new Contact
                    {
                        Id = collider.GetInstanceID(), Position = bounds.center, Rotation = rotation, Seen = now
                    };
                }
                if (body != null && !body.isKinematic && body.linearVelocity.sqrMagnitude > 0.001f)
                {
                    velocity = body.GetPointVelocity(bounds.center);
                    angular = body.angularVelocity;
                }
                velocity = Vector3.ClampMagnitude(velocity, 4f);
                int minX = Mathf.Clamp(Mathf.FloorToInt((bounds.min.x - _origin.x) / CellSize), 0, Resolution - 1);
                int maxX = Mathf.Clamp(Mathf.CeilToInt((bounds.max.x - _origin.x) / CellSize), 0, Resolution - 1);
                int minZ = Mathf.Clamp(Mathf.FloorToInt((bounds.min.z - _origin.y) / CellSize), 0, Resolution - 1);
                int maxZ = Mathf.Clamp(Mathf.CeilToInt((bounds.max.z - _origin.y) / CellSize), 0, Resolution - 1);
                for (int z = minZ; z <= maxZ; z++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        Vector3 p = new Vector3(_origin.x + (x + 0.5f) * CellSize, seaLevel,
                            _origin.y + (z + 0.5f) * CellSize);
                        Vector3 closest = collider.ClosestPoint(p);
                        float coverage = 1f - Mathf.Clamp01((p - closest).magnitude / (CellSize * 0.45f));
                        int index = z * Resolution + x;
                        if (coverage > _pixels[index].r)
                        {
                            Vector3 localVelocity = Vector3.ClampMagnitude(velocity
                                + Vector3.Cross(angular, p - bounds.center), 4f);
                            _pixels[index] = new Color(coverage, localVelocity.x, localVelocity.z, 0f);
                        }
                    }
                }
                ObstacleCount++;
            }
            _obstacles.SetPixels(_pixels);
            _obstacles.Apply(false, false);
        }

        private int FindContact(int id, float now)
        {
            int free = -1;
            for (int i = 0; i < _contacts.Length; i++)
            {
                if (_contacts[i].Id == id)
                {
                    return i;
                }
                if (free < 0 && (_contacts[i].Id == 0 || now - _contacts[i].Seen > 0.5f))
                {
                    free = i;
                }
            }
            return free;
        }

        private static RenderTexture CreateTexture(string name)
        {
            var texture = new RenderTexture(Resolution, Resolution, 0, RenderTextureFormat.ARGBFloat,
                RenderTextureReadWrite.Linear)
            {
                name = name, enableRandomWrite = true, filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave
            };
            texture.Create();
            return texture;
        }

        private static void Clear(RenderTexture texture)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = texture;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = previous;
        }

        private static void Release(RenderTexture texture)
        {
            texture.Release();
            UnityEngine.Object.Destroy(texture);
        }
    }
}
