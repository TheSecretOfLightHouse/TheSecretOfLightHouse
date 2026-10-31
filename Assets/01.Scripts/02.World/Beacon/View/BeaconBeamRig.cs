using UnityEngine;

namespace Lighthouse.World.Beacon.View
{
    // Turns one 0..1 level into the spot light, the glow light and the additive cone. The cone keeps the spot's
    // angle at every length, so the beam looks like it is growing out of the lantern instead of fattening.
    [DisallowMultipleComponent]
    public sealed class BeaconBeamRig : MonoBehaviour
    {
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        [SerializeField] private Light _spotLight;
        [Tooltip("Optional round glow at the lantern. Leave empty to skip it.")]
        [SerializeField] private Light _glowLight;
        [SerializeField] private MeshFilter _beamFilter;
        [SerializeField] private MeshRenderer _beamRenderer;
        [SerializeField, Min(1f)] private float _beamLength = 60f;
        [SerializeField, Min(0f)] private float _spotIntensity = 40f;
        [SerializeField, Min(0f)] private float _glowIntensity = 2f;
        [SerializeField, Min(0f)] private float _coneBrightness = 1f;

        private MaterialPropertyBlock _block;
        private Mesh _mesh;
        private float _appliedLevel = -1f;
        private float _tanHalfAngle;

        private void Awake()
        {
            if (_spotLight == null || _beamFilter == null || _beamRenderer == null)
            {
                Debug.LogError($"[{nameof(BeaconBeamRig)}] Spot light, beam filter and beam renderer are required.");
                enabled = false;
                return;
            }

            _tanHalfAngle = Mathf.Tan(_spotLight.spotAngle * 0.5f * Mathf.Deg2Rad);
            _spotLight.type = LightType.Spot;
            _spotLight.shadows = LightShadows.None;
            _spotLight.range = _beamLength;
            _mesh = BeaconConeMesh.Create();
            _beamFilter.sharedMesh = _mesh;
            _block = new MaterialPropertyBlock();
            Apply(0f);
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
            }
        }

        public void Apply(float level)
        {
            level = Mathf.Clamp01(level);

            if (!enabled || Mathf.Approximately(level, _appliedLevel))
            {
                return;
            }

            _appliedLevel = level;
            bool isVisible = level > 0f;
            float length = _beamLength * level;

            _spotLight.enabled = isVisible;
            _spotLight.intensity = _spotIntensity * level;
            _beamRenderer.enabled = isVisible;
            _beamFilter.transform.localScale = new Vector3(length * _tanHalfAngle, length * _tanHalfAngle, length);
            _beamRenderer.GetPropertyBlock(_block);
            _block.SetFloat(IntensityId, _coneBrightness * level);
            _beamRenderer.SetPropertyBlock(_block);

            if (_glowLight != null)
            {
                _glowLight.enabled = isVisible;
                _glowLight.intensity = _glowIntensity * level;
            }
        }
    }
}
