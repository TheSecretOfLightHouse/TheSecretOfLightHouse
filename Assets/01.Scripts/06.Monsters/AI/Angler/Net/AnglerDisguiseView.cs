using System.Collections.Generic;
using Mirror;
using Lighthouse.Monsters.AI.Rules;
using Lighthouse.Monsters.AI.Server;
using Lighthouse.World.Query.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lighthouse.Monsters.AI.Net
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster))]
    public sealed class AnglerDisguiseView : NetworkBehaviour
    {
        [SerializeField] private GameObject _boatVisual;
        [SerializeField] private GameObject _fishVisual;
        [SerializeField] private GameObject[] _boatVariants = new GameObject[0];
        [SerializeField, Min(0.01f)] private float _revealDistance = 12f;
        [SerializeField, Min(0.01f)] private float _transitionSeconds = 0.7f;
        [SerializeField, Min(0.01f)] private float _revealedDepth = 0.1f;
        [SerializeField] private float _boatWaterlineOffset = -0.15f;

        private readonly List<FadeRenderer> _boatRenderers = new List<FadeRenderer>();
        private readonly List<FadeRenderer> _fishRenderers = new List<FadeRenderer>();
        private readonly List<GameObject> _fishEffects = new List<GameObject>();
        private readonly List<bool> _effectActiveStates = new List<bool>();
        private bool _hasPresentation;
        private bool _isRevealed;
        private float _blend;
        private Vector3 _boatLocalPosition;
        private SeaMonster _monster;
        private MonsterWaterBody _waterBody;
        [SyncVar(hook = nameof(HandleDisguiseChanged))] private int _disguiseIndex = -1;

        public bool IsRevealed => _hasPresentation && _isRevealed;
        public float RevealDistance => _revealDistance;
        public int DisguiseIndex => _disguiseIndex;

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            _waterBody = GetComponent<MonsterWaterBody>();
            if (_boatVisual != null)
            {
                _boatLocalPosition = _boatVisual.transform.localPosition;
            }
            CacheRenderers(_boatVisual, _boatRenderers);
            CacheRenderers(_fishVisual, _fishRenderers);
            CacheFishEffects();
            ApplyDisguiseVariant();
            HidePresentation();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _disguiseIndex = _boatVariants.Length > 0 ? Random.Range(0, _boatVariants.Length) : -1;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyDisguiseVariant();
        }

        [ServerCallback]
        private void FixedUpdate()
        {
            if (_monster == null || !_monster.IsInitialized || _monster.IsDead
                || _waterBody == null || !_waterBody.isActiveAndEnabled)
            {
                return;
            }

            UpdateTargetDepth(HasNearbyObserver());
        }

        private void LateUpdate()
        {
            if (!isClient || !NetworkClient.ready || NetworkClient.localPlayer == null)
            {
                HidePresentation();
                return;
            }

            bool reveal = ShouldReveal(NetworkClient.localPlayer.transform.position);
            if (!_hasPresentation)
            {
                _hasPresentation = true;
                _isRevealed = reveal;
                _blend = reveal ? 1f : 0f;
            }
            else
            {
                _isRevealed = reveal;
                _blend = Mathf.MoveTowards(_blend, reveal ? 1f : 0f,
                    Time.deltaTime / Mathf.Max(0.01f, _transitionSeconds));
            }

            UpdateBoatSurfacePosition();
            ApplyPresentation();
        }

        private void OnDisable()
        {
            if (isServer && _waterBody != null && _monster != null && !_monster.IsDead)
            {
                _waterBody.RestoreDefaultDepth();
            }
            HidePresentation();
            if (_boatVisual != null)
            {
                _boatVisual.transform.localPosition = _boatLocalPosition;
            }
        }

        private void OnDestroy()
        {
            ReleaseRenderers(_boatRenderers);
            ReleaseRenderers(_fishRenderers);
        }

        public bool ShouldReveal(Vector3 observerPosition)
        {
            return AnglerDisguiseRules.ShouldReveal(transform.position, observerPosition, _revealDistance);
        }

        private void HandleDisguiseChanged(int oldIndex, int newIndex)
        {
            ApplyDisguiseVariant();
        }

        private void ApplyDisguiseVariant()
        {
            int selected = _disguiseIndex >= 0 && _disguiseIndex < _boatVariants.Length
                ? _disguiseIndex : 0;
            for (int i = 0; i < _boatVariants.Length; i++)
            {
                if (_boatVariants[i] != null)
                {
                    _boatVariants[i].SetActive(i == selected);
                }
            }
        }

        private bool HasNearbyObserver()
        {
            // The physical body is shared, so remote observers must also trigger surfacing.
            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
            {
                if (connection != null && connection.isReady && connection.identity != null
                    && ShouldReveal(connection.identity.transform.position))
                {
                    return true;
                }
            }
            return false;
        }

        private void UpdateTargetDepth(bool revealed)
        {
            if (revealed)
            {
                // A positive target keeps depth control active; zero disables it.
                _waterBody.Buoyancy.SetTargetDepth(_revealedDepth);
            }
            else
            {
                _waterBody.RestoreDefaultDepth();
            }
        }

        private void UpdateBoatSurfacePosition()
        {
            if (_boatVisual == null || !WorldServices.TryGet<IWorldQuery>(out IWorldQuery world))
            {
                return;
            }

            Transform boat = _boatVisual.transform;
            Transform parent = boat.parent;
            if (parent == null)
            {
                return;
            }

            // The model pivot is at the hull bottom, so use a waterline offset instead of its setup height.
            Vector3 offset = parent.TransformVector(_boatLocalPosition);
            Vector3 position = parent.position + offset;
            float surfaceHeight = world.SampleWaterHeight(position);
            if (float.IsNaN(surfaceHeight) || float.IsInfinity(surfaceHeight))
            {
                return;
            }
            position.y = surfaceHeight + _boatWaterlineOffset;
            boat.position = position;
        }

        private void ApplyPresentation()
        {
            bool isTransitioning = _blend > 0f && _blend < 1f;
            SetEffectsVisible(!isTransitioning);
            if (_boatVisual != null)
            {
                _boatVisual.SetActive(_blend < 1f);
            }
            if (_fishVisual != null)
            {
                _fishVisual.SetActive(_blend > 0f);
            }

            ApplyFade(_boatRenderers, 1f - _blend, isTransitioning);
            ApplyFade(_fishRenderers, _blend, isTransitioning);
        }

        private void HidePresentation()
        {
            _hasPresentation = false;
            _isRevealed = false;
            SetEffectsVisible(true);
            if (_boatVisual != null)
            {
                _boatVisual.SetActive(false);
            }
            if (_fishVisual != null)
            {
                _fishVisual.SetActive(false);
            }
        }

        private void CacheFishEffects()
        {
            if (_fishVisual == null)
            {
                return;
            }

            foreach (ParticleSystem particle in _fishVisual.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particle.transform.parent != null && particle.transform.parent.GetComponent<ParticleSystem>() != null)
                {
                    continue;
                }
                _fishEffects.Add(particle.gameObject);
                _effectActiveStates.Add(particle.gameObject.activeSelf);
            }
        }

        private void SetEffectsVisible(bool visible)
        {
            for (int i = 0; i < _fishEffects.Count; i++)
            {
                GameObject effect = _fishEffects[i];
                bool active = visible && _effectActiveStates[i];
                if (effect != null && effect.activeSelf != active)
                {
                    effect.SetActive(active);
                }
            }
        }

        private static void CacheRenderers(GameObject visual, List<FadeRenderer> result)
        {
            if (visual == null)
            {
                return;
            }

            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
                {
                    continue;
                }
                result.Add(new FadeRenderer(renderer));
            }
        }

        private static void ApplyFade(List<FadeRenderer> renderers, float alpha, bool transitioning)
        {
            foreach (FadeRenderer renderer in renderers)
            {
                renderer.Apply(alpha, transitioning);
            }
        }

        private static void ReleaseRenderers(List<FadeRenderer> renderers)
        {
            foreach (FadeRenderer renderer in renderers)
            {
                renderer.Release();
            }
        }

        private sealed class FadeRenderer
        {
            private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
            private readonly Renderer _renderer;
            private readonly Material[] _originals;
            private readonly Material[] _transparent;
            private readonly Color[] _colors;
            private readonly ShadowCastingMode _originalShadows;
            private bool _usingTransparent;

            public FadeRenderer(Renderer renderer)
            {
                _renderer = renderer;
                _originals = renderer.sharedMaterials;
                _transparent = new Material[_originals.Length];
                _colors = new Color[_originals.Length];
                _originalShadows = renderer.shadowCastingMode;
                for (int i = 0; i < _originals.Length; i++)
                {
                    Material original = _originals[i];
                    if (original == null || !original.HasProperty(BaseColorId) || !original.HasProperty("_Surface"))
                    {
                        _transparent[i] = original;
                        continue;
                    }

                    Material copy = new Material(original);
                    copy.SetFloat("_Surface", 1f);
                    copy.SetFloat("_Blend", 0f);
                    copy.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    copy.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    copy.SetFloat("_ZWrite", 0f);
                    copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    copy.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    copy.renderQueue = (int)RenderQueue.Transparent;
                    _transparent[i] = copy;
                    _colors[i] = original.GetColor(BaseColorId);
                }
            }

            public void Apply(float alpha, bool transitioning)
            {
                if (_renderer == null)
                {
                    return;
                }
                if (_usingTransparent != transitioning)
                {
                    _renderer.sharedMaterials = transitioning ? _transparent : _originals;
                    _renderer.shadowCastingMode = transitioning ? ShadowCastingMode.Off : _originalShadows;
                    _usingTransparent = transitioning;
                }
                if (!transitioning)
                {
                    return;
                }
                for (int i = 0; i < _transparent.Length; i++)
                {
                    if (_transparent[i] == null || _transparent[i] == _originals[i])
                    {
                        continue;
                    }
                    Color color = _colors[i];
                    color.a *= alpha;
                    _transparent[i].SetColor(BaseColorId, color);
                }
            }

            public void Release()
            {
                if (_renderer != null)
                {
                    _renderer.sharedMaterials = _originals;
                    _renderer.shadowCastingMode = _originalShadows;
                }
                for (int i = 0; i < _transparent.Length; i++)
                {
                    if (_transparent[i] != null && _transparent[i] != _originals[i])
                    {
                        Object.Destroy(_transparent[i]);
                    }
                }
            }
        }
    }
}
