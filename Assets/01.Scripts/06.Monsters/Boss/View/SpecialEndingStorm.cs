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
        [SerializeField] private ParticleSystem _rain;
        [SerializeField] private AudioSource _rainAudio;
        [SerializeField] private float _rainPeakRate = 1150f;
        [SerializeField] private AnimationCurve _rainStrength = AnimationCurve.EaseInOut(2.2f, 0f, 11f, 1f);
        [SerializeField] private float _flashIntensity = 10f;
        [SerializeField] private float _flashDuration = 0.6f;
        [SerializeField] private float _flashHold = 0.05f;
        [SerializeField] private float _flashAfterglowDecay = 7f;
        [SerializeField] private float[] _shakeTimes = { 12.4f, 26.25f, 27.325f };
        [SerializeField] private float[] _shakeDurations = { 0.35f, 0.8f, 0.55f };
        [SerializeField] private Vector3[] _shakeAngles =
        {
            new Vector3(0.12f, 0.3f, 0.45f),
            new Vector3(0.18f, 0.5f, 0.7f),
            new Vector3(0.1f, 0.16f, 0.26f)
        };
        [SerializeField] private float _shakeDamping = 1.8f;
        [SerializeField] private Vector3 _shakeFrequency = new Vector3(23f, 37f, 29f);
        private Material _activeSky;
        private Quaternion _cameraBaseRotation;
        private Quaternion _cameraShakenRotation;
        private bool _hasCameraOffset;

        private void Update()
        {
            RestoreCamera();
        }

        private void LateUpdate()
        {
            RestoreCamera();
            float rain = _camera != null && _camera.enabled && _director != null
                ? Mathf.Clamp01(_rainStrength.Evaluate((float)_director.time)) : 0f;
            ApplyRain(rain);
            if (_camera == null || !_camera.enabled || _director == null)
            {
                ClearFlash();
                return;
            }
            _activeSky = RenderSettings.skybox;
            float pulse = EvaluateFlash((float)_director.time, out int strike);
            if (_flashLight != null)
            {
                _flashLight.enabled = pulse > 0.005f;
                _flashLight.intensity = pulse * _flashIntensity;
            }
            if (_activeSky != null && _activeSky.HasProperty(FlashId))
            {
                float offset = strike < _strikeAngles.Length ? _strikeAngles[strike] : 0f;
                float angle = transform.eulerAngles.y * Mathf.Deg2Rad + Mathf.PI + offset;
                _activeSky.SetFloat(FlashId, pulse * 0.8f);
                _activeSky.SetVector(BoltId, new Vector4(angle, 3.7f + strike * 9.3f, pulse * 1.4f, 0.75f));
            }
            ApplyCameraShake((float)_director.time);
        }

        private void OnDisable()
        {
            RestoreCamera();
            ApplyRain(0f);
            ClearFlash();
        }

        public float GetFlashStrength(float time)
        {
            return EvaluateFlash(time, out _);
        }

        private float EvaluateFlash(float time, out int strike)
        {
            strike = 0;
            for (int i = 0; i < _strikeTimes.Length; i++)
            {
                float age = time - _strikeTimes[i];
                if (age < 0f || age >= _flashDuration)
                {
                    continue;
                }
                strike = i;
                // Keep the sharp double strike, then let its illumination linger on the silhouette.
                float pulse = age >= 0.14f
                    ? 0.65f * Mathf.Exp(-Mathf.Max(0f, age - 0.14f - _flashHold) * _flashAfterglowDecay)
                    : age < 0.12f ? Mathf.Exp(-Mathf.Max(0f, age - _flashHold) * 24f) : 0f;
                float tail = Mathf.InverseLerp(_flashDuration * 0.7f, _flashDuration, age);
                return pulse * (1f - Mathf.SmoothStep(0f, 1f, tail));
            }
            return 0f;
        }

        private void ApplyCameraShake(float time)
        {
            Vector3 angles = Vector3.zero;
            for (int i = 0; i < _shakeTimes.Length; i++)
            {
                float age = time - _shakeTimes[i];
                float duration = _shakeDurations[i];
                if (age < 0f || age >= duration)
                {
                    continue;
                }
                float decay = Mathf.Exp(-age * _shakeDamping / duration)
                    * (1f - Mathf.SmoothStep(0f, 1f, age / duration));
                angles += Vector3.Scale(_shakeAngles[i], new Vector3(
                    Mathf.Sin(age * _shakeFrequency.x), Mathf.Sin(age * _shakeFrequency.y),
                    Mathf.Sin(age * _shakeFrequency.z))) * decay;
            }
            if (angles.sqrMagnitude < 0.00000001f)
            {
                return;
            }
            // Only rotate the cinematic camera; its fixed height and animation path remain intact.
            _cameraBaseRotation = _camera.transform.localRotation;
            _cameraShakenRotation = _cameraBaseRotation * Quaternion.Euler(angles);
            _camera.transform.localRotation = _cameraShakenRotation;
            _hasCameraOffset = true;
        }

        private void RestoreCamera()
        {
            if (_hasCameraOffset && _camera != null)
            {
                // An explicit Timeline seek may already have replaced the shaken pose.
                Quaternion current = _camera.transform.localRotation;
                float difference = Mathf.Abs(current.x - _cameraShakenRotation.x)
                    + Mathf.Abs(current.y - _cameraShakenRotation.y)
                    + Mathf.Abs(current.z - _cameraShakenRotation.z)
                    + Mathf.Abs(current.w - _cameraShakenRotation.w);
                if (difference < 0.00001f)
                {
                    _camera.transform.localRotation = _cameraBaseRotation;
                }
            }
            _hasCameraOffset = false;
        }

        private void ApplyRain(float strength)
        {
            if (_rain != null)
            {
                ParticleSystem.EmissionModule emission = _rain.emission;
                emission.rateOverTimeMultiplier = _rainPeakRate * strength;
            }
            if (_rainAudio != null)
            {
                _rainAudio.volume = strength;
            }
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
