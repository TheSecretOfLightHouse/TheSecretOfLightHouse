using UnityEngine;
using UnityEngine.Playables;

namespace Lighthouse.Monsters.Boss
{
    [DefaultExecutionOrder(30002)]
    [DisallowMultipleComponent]
    public sealed class SpecialEndingStorm : MonoBehaviour
    {
        private static readonly int FlashId = Shader.PropertyToID("_LightningFlash");
        private static readonly int BoltId = Shader.PropertyToID("_LightningBolt");

        [SerializeField] private Camera _camera;
        [SerializeField] private PlayableDirector _director;
        [SerializeField] private Light _flashLight;
        [SerializeField] private float[] _strikeTimes = { 3.1f, 7.6f, 12.5f, 18.05f };
        [SerializeField] private float[] _strikeAngles = { -0.6f, 0.22f, -0.45f, 0.12f };
        private Material _activeSky;

        private void LateUpdate()
        {
            if (_camera == null || !_camera.enabled || _director == null)
            {
                ClearFlash();
                return;
            }
            _activeSky = RenderSettings.skybox;
            float pulse = 0f;
            int strike = 0;
            for (int i = 0; i < _strikeTimes.Length; i++)
            {
                float age = (float)_director.time - _strikeTimes[i];
                if (age < 0f || age > 0.3f)
                {
                    continue;
                }
                // Two brief strokes share the same bolt, evaluated from the synchronized Timeline.
                pulse = age < 0.12f ? Mathf.Exp(-age * 35f) : 0f;
                if (age >= 0.14f)
                {
                    pulse = 0.65f * Mathf.Exp(-(age - 0.14f) * 35f);
                }
                strike = i;
                break;
            }
            if (_flashLight != null)
            {
                _flashLight.enabled = pulse > 0.005f;
                _flashLight.intensity = pulse * 6f;
            }
            if (_activeSky != null && _activeSky.HasProperty(FlashId))
            {
                float offset = strike < _strikeAngles.Length ? _strikeAngles[strike] : 0f;
                float angle = transform.eulerAngles.y * Mathf.Deg2Rad + Mathf.PI + offset;
                _activeSky.SetFloat(FlashId, pulse * 0.8f);
                _activeSky.SetVector(BoltId, new Vector4(angle, 3.7f + strike * 9.3f, pulse * 1.4f, 0.75f));
            }
        }

        private void OnDisable()
        {
            ClearFlash();
        }

        private void ClearFlash()
        {
            if (_flashLight != null)
            {
                _flashLight.enabled = false;
                _flashLight.intensity = 0f;
            }
            if (_activeSky != null && _activeSky.HasProperty(FlashId))
            {
                _activeSky.SetFloat(FlashId, 0f);
                _activeSky.SetVector(BoltId, Vector4.zero);
            }
            _activeSky = null;
        }
    }
}
