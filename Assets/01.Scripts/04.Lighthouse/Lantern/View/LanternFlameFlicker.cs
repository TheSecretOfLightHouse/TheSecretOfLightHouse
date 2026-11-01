using UnityEngine;

namespace Lighthouse.Lantern.View
{
    // Flame flicker for a lantern. A steady base with slow breathing plus fast small jitter reads as a real wick;
    // pure random noise reads as a broken bulb. The light also drifts a few millimetres so the frame shadows waver.
    // The flame and the frosted glass glow follow the same value through per-material property blocks, so the glass
    // brightens with the flame instead of glowing on its own (no material copies).
    [DisallowMultipleComponent]
    public sealed class LanternFlameFlicker : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private Light _light;
        [SerializeField] private Renderer _renderer;
        [Tooltip("Material slot of the flame on the renderer. -1 skips it.")]
        [SerializeField] private int _flameMaterialIndex = -1;
        [ColorUsage(false, true)]
        [SerializeField] private Color _flameEmission = new Color(0.9f, 2.6f, 1.3f);
        [Tooltip("Material slot of the glass on the renderer. -1 skips it.")]
        [SerializeField] private int _glassMaterialIndex = -1;
        [ColorUsage(false, true)]
        [SerializeField] private Color _glassEmission = new Color(0.25f, 0.9f, 0.5f);
        [Range(0f, 0.5f)]
        [SerializeField] private float _slowAmplitude = 0.08f;
        [SerializeField] private float _slowSpeed = 0.7f;
        [Range(0f, 0.5f)]
        [SerializeField] private float _fastAmplitude = 0.06f;
        [SerializeField] private float _fastSpeed = 9f;
        [SerializeField] private float _positionJitter = 0.004f;

        private MaterialPropertyBlock _block;
        private Vector3 _baseLocalPosition;
        private float _baseIntensity;
        private float _seed;

        // Owner-driven dimming (0 = dark, 1 = full), e.g. a ghost ship fading its lanterns under the sea.
        public float Brightness { get; set; } = 1f;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            // Per-instance seed keeps a row of lanterns from pulsing in sync.
            _seed = Random.Range(0f, 100f);
            if (_light != null)
            {
                _baseIntensity = _light.intensity;
                _baseLocalPosition = _light.transform.localPosition;
            }
        }

        private void OnDisable()
        {
            if (_light != null)
            {
                _light.enabled = true;
                _light.intensity = _baseIntensity;
                _light.transform.localPosition = _baseLocalPosition;
            }

            ClearSlot(_flameMaterialIndex);
            ClearSlot(_glassMaterialIndex);
        }

        private void Update()
        {
            float time = Time.time;
            float slow = Mathf.PerlinNoise(_seed, time * _slowSpeed) * 2f - 1f;
            float fast = Mathf.PerlinNoise(_seed + 17f, time * _fastSpeed) * 2f - 1f;
            float brightness = Mathf.Clamp01(Brightness);
            float factor = (1f + slow * _slowAmplitude + fast * _fastAmplitude) * brightness;

            if (_light != null)
            {
                _light.enabled = brightness > 0f;
                _light.intensity = _baseIntensity * factor;
                float swayX = Mathf.PerlinNoise(_seed + 31f, time * _slowSpeed * 2f) * 2f - 1f;
                float swayZ = Mathf.PerlinNoise(_seed + 47f, time * _slowSpeed * 2f) * 2f - 1f;
                _light.transform.localPosition = _baseLocalPosition +
                    new Vector3(swayX * _positionJitter, fast * _positionJitter * 0.5f, swayZ * _positionJitter);
            }

            SetSlotEmission(_flameMaterialIndex, _flameEmission * factor);
            SetSlotEmission(_glassMaterialIndex, _glassEmission * factor);
        }

        private void SetSlotEmission(int materialIndex, Color emission)
        {
            if (_renderer == null || materialIndex < 0)
            {
                return;
            }

            _renderer.GetPropertyBlock(_block, materialIndex);
            _block.SetColor(EmissionColorId, emission);
            _renderer.SetPropertyBlock(_block, materialIndex);
        }

        private void ClearSlot(int materialIndex)
        {
            if (_renderer != null && materialIndex >= 0)
            {
                _renderer.SetPropertyBlock(null, materialIndex);
            }
        }
    }
}
