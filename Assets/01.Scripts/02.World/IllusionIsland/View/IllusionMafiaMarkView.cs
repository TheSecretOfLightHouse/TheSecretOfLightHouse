using Lighthouse.World.IllusionIsland.Net;
using UnityEngine;

namespace Lighthouse.World.IllusionIsland.View
{
    [RequireComponent(typeof(IllusionIslandProxy))]
    [DisallowMultipleComponent]
    public class IllusionMafiaMarkView : MonoBehaviour
    {
        private const string UnlitShaderName = "Universal Render Pipeline/Unlit";
        private const string FallbackShaderName = "Sprites/Default";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Color _markColor = new Color(0.62f, 0.3f, 0.95f, 1f);
        [SerializeField, Min(0.1f)] private float _markHeight = 18f;
        [SerializeField, Min(0.1f)] private float _markSize = 3f;
        [SerializeField, Min(0f)] private float _bobAmplitude = 0.8f;
        [SerializeField, Min(0f)] private float _bobSpeed = 1.6f;
        [SerializeField, Min(0f)] private float _spinSpeed = 60f;

        private IllusionIslandProxy _proxy;
        private Transform _mark;
        private Material _material;
        private float _baseY;

        private void Awake()
        {
            if (!TryGetComponent(out _proxy))
            {
                Debug.LogError($"[{nameof(IllusionMafiaMarkView)}] IllusionIslandProxy가 없다.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            IllusionIslandProxy.MafiaMarkReceived += HandleMafiaMarkReceived;
            if (_proxy != null)
            {
                _proxy.VanishingChanged += HandleVanishingChanged;
                if (_proxy.IsMafiaMarked && !_proxy.IsVanishing)
                {
                    CreateMark();
                }
            }
        }

        private void OnDisable()
        {
            IllusionIslandProxy.MafiaMarkReceived -= HandleMafiaMarkReceived;
            if (_proxy != null)
            {
                _proxy.VanishingChanged -= HandleVanishingChanged;
            }
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
        }

        private void Update()
        {
            if (_mark == null)
            {
                return;
            }

            Vector3 local = _mark.localPosition;
            local.y = _baseY + Mathf.Sin(Time.time * _bobSpeed) * _bobAmplitude;
            _mark.localPosition = local;
            _mark.Rotate(0f, _spinSpeed * Time.deltaTime, 0f, Space.Self);
        }

        private void HandleMafiaMarkReceived(IllusionIslandProxy proxy)
        {
            if (proxy == _proxy && !proxy.IsVanishing)
            {
                CreateMark();
            }
        }

        private void HandleVanishingChanged(IllusionIslandProxy proxy, bool isVanishing)
        {
            if (isVanishing)
            {
                DestroyMark();
            }
        }

        private void CreateMark()
        {
            if (_mark != null)
            {
                return;
            }

            Shader shader = Shader.Find(UnlitShaderName);
            if (shader == null)
            {
                shader = Shader.Find(FallbackShaderName);
            }

            if (shader == null)
            {
                return;
            }

            _material = new Material(shader) { name = "IllusionMarkRuntime" };
            if (_material.HasProperty(BaseColorId))
            {
                _material.SetColor(BaseColorId, _markColor);
            }
            else
            {
                _material.color = _markColor;
            }

            GameObject root = new GameObject("IllusionMafiaMark");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.up * _markHeight;
            _baseY = _markHeight;
            _mark = root.transform;

            AddPart(PrimitiveType.Sphere, new Vector3(_markSize, _markSize, _markSize), Vector3.zero);
            AddPart(
                PrimitiveType.Cylinder,
                new Vector3(_markSize * 2.2f, _markSize * 0.05f, _markSize * 2.2f),
                Vector3.zero);
        }

        private void AddPart(PrimitiveType type, Vector3 scale, Vector3 offset)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = type.ToString();
            if (part.TryGetComponent(out Collider collider))
            {
                Destroy(collider);
            }

            part.transform.SetParent(_mark, false);
            part.transform.localPosition = offset;
            part.transform.localScale = scale;
            if (part.TryGetComponent(out MeshRenderer meshRenderer))
            {
                meshRenderer.sharedMaterial = _material;
                meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
            }
        }

        private void DestroyMark()
        {
            if (_mark != null)
            {
                Destroy(_mark.gameObject);
                _mark = null;
            }
        }
    }
}
