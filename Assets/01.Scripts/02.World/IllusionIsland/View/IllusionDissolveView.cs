using Lighthouse.World.IllusionIsland.Net;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lighthouse.World.IllusionIsland.View
{
    [RequireComponent(typeof(IllusionIslandProxy))]
    [DisallowMultipleComponent]
    public class IllusionDissolveView : MonoBehaviour
    {
        private const int FogTextureSize = 32;
        private const string FogShaderName = "Universal Render Pipeline/Particles/Unlit";
        private const string FogFallbackShaderName = "Sprites/Default";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static Texture2D s_fogTexture;

        [SerializeField, Min(0.1f)] private float _fallbackSeconds = 1.5f;
        [SerializeField, Range(0.1f, 0.8f)] private float _dissolveStartFraction = 0.2f;
        [SerializeField, Range(0.2f, 1f)] private float _hideFraction = 0.4f;
        [SerializeField, Range(0.05f, 1f)] private float _pieceSpan = 0.4f;
        [SerializeField, Min(0.1f)] private float _lingerSeconds = 28f;
        [SerializeField, Min(0f)] private float _holdSeconds = 6f;
        [SerializeField] private Color _fogColor = new Color(0.88f, 0.92f, 0.96f, 1f);
        [SerializeField, Min(1)] private int _fogPuffCount = 150;
        [SerializeField] private Vector3 _fogExtent = new Vector3(9f, 4f, 8.5f);
        [SerializeField, Min(0f)] private float _fogNoiseStrength = 1.5f;
        [SerializeField, Min(0.1f)] private float _fogPuffSize = 10f;
        [SerializeField, Min(0f)] private float _fogHeight = 3f;

        private IllusionIslandProxy _proxy;
        private Renderer[] _renderers;
        private Terrain[] _terrains;
        private Vector3[] _baseScales;
        private Vector3[] _basePositions;
        private Vector3[] _centers;
        private ParticleSystem _fog;
        private Material _fogMaterial;
        private Vector3 _startPosition;
        private float _startTime;
        private float _duration;
        private bool _isPlaying;
        private bool _isHidden;
        private int _doneCount;

        private void Awake()
        {
            if (!TryGetComponent(out _proxy))
            {
                Debug.LogError($"[{nameof(IllusionDissolveView)}] IllusionIslandProxy가 없다.", this);
                enabled = false;
                return;
            }

            CollectRenderers();
        }

        private void OnEnable()
        {
            if (_proxy != null)
            {
                _proxy.VanishingChanged += HandleVanishingChanged;
            }
        }

        private void Start()
        {
            if (_proxy != null && _proxy.IsVanishing)
            {
                Begin();
            }
        }

        private void OnDisable()
        {
            if (_proxy != null)
            {
                _proxy.VanishingChanged -= HandleVanishingChanged;
            }
        }

        private void OnDestroy()
        {
            if (_fog != null)
            {
                _fog.transform.SetParent(null, true);
                Destroy(_fog.gameObject, _duration + _lingerSeconds + 1f);
            }

            if (_fogMaterial != null)
            {
                Destroy(_fogMaterial, _duration + _lingerSeconds + 1f);
            }
        }

        private void Update()
        {
            if (!_isPlaying)
            {
                return;
            }

            float t = Mathf.Clamp01((Time.time - _startTime) / _duration);
            float d = Mathf.InverseLerp(_dissolveStartFraction, _hideFraction, t);
            DissolveRenderers(d);
            if (d >= 1f && !_isHidden)
            {
                HideTerrains();
            }

            if (t >= 1f)
            {
                _isPlaying = false;
            }
        }

        private void HandleVanishingChanged(IllusionIslandProxy proxy, bool isVanishing)
        {
            if (isVanishing)
            {
                Begin();
            }
        }

        private void Begin()
        {
            if (_isPlaying)
            {
                return;
            }

            float seconds = _proxy.VanishSeconds;
            _duration = seconds > 0f ? seconds : _fallbackSeconds;
            _startTime = Time.time;
            _startPosition = transform.position;
            _isPlaying = true;
            _isHidden = false;
            _doneCount = 0;
            PlayFog();
        }

        private void CollectRenderers()
        {
            Renderer[] found = GetComponentsInChildren<Renderer>(true);
            int count = 0;
            for (int i = 0; i < found.Length; i++)
            {
                if (IsHideable(found[i]))
                {
                    count++;
                }
            }

            _renderers = new Renderer[count];
            int index = 0;
            for (int i = 0; i < found.Length; i++)
            {
                if (IsHideable(found[i]))
                {
                    _renderers[index++] = found[i];
                }
            }

            float[] heights = new float[count];
            for (int i = 0; i < count; i++)
            {
                heights[i] = -_renderers[i].bounds.center.y;
            }

            System.Array.Sort(heights, _renderers);
            _baseScales = new Vector3[count];
            _basePositions = new Vector3[count];
            _centers = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                Transform piece = _renderers[i].transform;
                _baseScales[i] = piece.localScale;
                _basePositions[i] = piece.position;
                _centers[i] = _renderers[i].bounds.center;
            }

            _terrains = GetComponentsInChildren<Terrain>(true);
        }

        private static bool IsHideable(Renderer target)
        {
            return target != null && !(target is ParticleSystemRenderer);
        }

        private void DissolveRenderers(float progress)
        {
            int count = _renderers.Length;
            float span = Mathf.Clamp(_pieceSpan, 0.05f, 1f);
            int started = progress >= 1f ? count : Mathf.Min(count, Mathf.FloorToInt(progress * count / (1f - span)) + 1);
            for (int i = _doneCount; i < started; i++)
            {
                Renderer piece = _renderers[i];
                if (piece == null)
                {
                    continue;
                }

                float startAt = (float)i / count * (1f - span);
                float q = Mathf.Clamp01((progress - startAt) / span);
                if (q >= 1f)
                {
                    piece.enabled = false;
                    if (i == _doneCount)
                    {
                        _doneCount++;
                    }

                    continue;
                }

                float factor = 1f - q * q * (3f - 2f * q);
                Transform pieceTransform = piece.transform;
                pieceTransform.localScale = _baseScales[i] * factor;
                pieceTransform.position = _centers[i] + (_basePositions[i] - _centers[i]) * factor;
            }
        }

        private void HideTerrains()
        {
            _isHidden = true;
            DissolveRenderers(1f);
            for (int i = 0; i < _terrains.Length; i++)
            {
                if (_terrains[i] != null)
                {
                    _terrains[i].drawHeightmap = false;
                    _terrains[i].drawTreesAndFoliage = false;
                }
            }
        }

        private void PlayFog()
        {
            if (_fog == null && !TryCreateFog())
            {
                return;
            }

            _fog.transform.position = _startPosition + Vector3.up * _fogHeight;
            _fog.Clear();
            _fog.Play();
        }

        private bool TryCreateFog()
        {
            Shader shader = Shader.Find(FogShaderName);
            if (shader == null)
            {
                shader = Shader.Find(FogFallbackShaderName);
            }

            if (shader == null)
            {
                return false;
            }

            _fogMaterial = new Material(shader) { name = "IllusionFogRuntime" };
            _fogMaterial.mainTexture = GetFogTexture();
            if (_fogMaterial.HasProperty(BaseColorId))
            {
                _fogMaterial.SetColor(BaseColorId, Color.white);
            }

            if (_fogMaterial.HasProperty(SurfaceId))
            {
                _fogMaterial.SetFloat(SurfaceId, 1f);
                _fogMaterial.SetOverrideTag("RenderType", "Transparent");
                _fogMaterial.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
                _fogMaterial.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
                _fogMaterial.SetFloat(ZWriteId, 0f);
                _fogMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _fogMaterial.renderQueue = (int)RenderQueue.Transparent;
            }

            GameObject fogObject = new GameObject("IllusionFog");
            fogObject.SetActive(false);
            fogObject.transform.SetParent(transform, false);
            _fog = fogObject.AddComponent<ParticleSystem>();
            ConfigureFog(_fog);
            fogObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = _fogMaterial;
            fogObject.SetActive(true);
            _fog.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return true;
        }

        private void ConfigureFog(ParticleSystem system)
        {
            float life = _duration + _lingerSeconds;

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = life;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.9f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(_fogPuffSize * 0.6f, _fogPuffSize);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = _fogColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = _fogPuffCount * 2;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)_fogPuffCount) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1f;
            shape.radiusThickness = 1f;
            shape.scale = _fogExtent;

            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = _fogNoiseStrength;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.4f;
            noise.damping = true;

            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.25f));

            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            float peak = Mathf.Clamp(_dissolveStartFraction * _duration / life, 0.05f, 0.45f);
            float hold = Mathf.Clamp((_duration + _holdSeconds) / life, peak + 0.05f, 0.9f);
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, peak),
                    new GradientAlphaKey(1f, hold),
                    new GradientAlphaKey(0.62f, hold + (1f - hold) * 0.4f),
                    new GradientAlphaKey(0.3f, hold + (1f - hold) * 0.65f),
                    new GradientAlphaKey(0.1f, hold + (1f - hold) * 0.85f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;

            ParticleSystemRenderer particleRenderer = system.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
        }

        private static Texture2D GetFogTexture()
        {
            if (s_fogTexture != null)
            {
                return s_fogTexture;
            }

            s_fogTexture = new Texture2D(FogTextureSize, FogTextureSize, TextureFormat.RGBA32, false)
            {
                name = "IllusionFogSoft",
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            float half = (FogTextureSize - 1) * 0.5f;
            for (int y = 0; y < FogTextureSize; y++)
            {
                for (int x = 0; x < FogTextureSize; x++)
                {
                    float distance = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float alpha = Mathf.Clamp01(1f - distance);
                    alpha *= alpha;
                    s_fogTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            s_fogTexture.Apply(false, true);
            return s_fogTexture;
        }
    }
}
