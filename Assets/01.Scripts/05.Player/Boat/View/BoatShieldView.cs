using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lighthouse.Player.Boat.View
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(BoatShield), typeof(BoatController))]
    public sealed class BoatShieldView : MonoBehaviour
    {
        [SerializeField] private GameObject _shellPrefab;
        [SerializeField] private GameObject _pulsePrefab;
        [SerializeField] private Vector3 _localCenter = new Vector3(0.02f, 0.3f, 0.01f);
        [SerializeField] private Vector3 _localSize = new Vector3(0.66f, 0.66f, 0.66f);
        [SerializeField, Min(0.01f)] private float _growthDuration = 0.25f;
        [SerializeField, Min(0.01f)] private float _grantPulseScale = 0.8f;
        [SerializeField, Min(0.01f)] private float _breakPulseScale = 1.25f;
        [SerializeField, Min(0.1f)] private float _pulseLifetime = 3f;

        private BoatShield _shield;
        private BoatController _boat;
        private GameObject _shell;
        private float _elapsed;

        private void Awake()
        {
            _shield = GetComponent<BoatShield>();
            _boat = GetComponent<BoatController>();
        }

        private void OnEnable()
        {
            _shield.ShieldChanged += HandleShieldChanged;
            if (_shield.isClient)
            {
                SetShieldVisible(_shield.HasShield);
            }
        }

        private void Update()
        {
            if (!_shell)
            {
                return;
            }

            if (_boat.IsWrecked)
            {
                SetShieldVisible(false);
                return;
            }

            _elapsed = Mathf.Min(_elapsed + Time.deltaTime, _growthDuration);
            float scale = Mathf.Lerp(0.15f, 1f, Mathf.SmoothStep(0f, 1f, _elapsed / _growthDuration));
            _shell.transform.localScale = Vector3.Scale(_localSize, transform.lossyScale) * scale;
        }

        private void LateUpdate()
        {
            if (_shell)
            {
                _shell.transform.SetPositionAndRotation(
                    transform.TransformPoint(_localCenter), Quaternion.identity);
            }
        }

        private void OnDisable()
        {
            if (_shield)
            {
                _shield.ShieldChanged -= HandleShieldChanged;
            }
            SetShieldVisible(false);
        }

        public void SetShieldVisible(bool isVisible)
        {
            if (!isVisible)
            {
                if (_shell)
                {
                    _shell.SetActive(false);
                    Destroy(_shell);
                    _shell = null;
                }
                return;
            }

            if (_shell || !_shellPrefab || !isActiveAndEnabled || _boat.IsWrecked)
            {
                return;
            }

            _shell = Instantiate(_shellPrefab, transform.TransformPoint(_localCenter), Quaternion.identity);
            SceneManager.MoveGameObjectToScene(_shell, gameObject.scene);
            _shell.transform.localScale = Vector3.Scale(_localSize, transform.lossyScale) * 0.15f;
            _elapsed = 0f;
        }

        public void PlayPulse(bool isBreaking)
        {
            if (!_pulsePrefab || !isActiveAndEnabled)
            {
                return;
            }

            GameObject pulse = Instantiate(_pulsePrefab, transform.TransformPoint(_localCenter), Quaternion.identity);
            SceneManager.MoveGameObjectToScene(pulse, gameObject.scene);
            pulse.transform.localScale = Vector3.one * (isBreaking ? _breakPulseScale : _grantPulseScale);
            Destroy(pulse, _pulseLifetime);
        }

        private void HandleShieldChanged(bool hasShield)
        {
            SetShieldVisible(hasShield);
        }
    }
}
