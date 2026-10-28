using UnityEngine;

namespace Lighthouse.Player.Boat.View
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoatCannon))]
    public sealed class BoatCannonFeedback : MonoBehaviour
    {
        [SerializeField] private ParticleSystem _flash;
        [SerializeField] private ParticleSystem _smoke;
        [SerializeField] private AudioSource _audio;
        [SerializeField] private AudioClip _fireClip;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.65f;
        [SerializeField] private Transform _barrelVisual;
        [SerializeField, Min(0f)] private float _barrelDistance = 0.08f;
        [SerializeField, Min(0.01f)] private float _returnDuration = 0.22f;
        [SerializeField, Min(0f)] private float _cameraPitch = 1.2f;
        [SerializeField, Min(0f)] private float _cameraDistance = 0.035f;

        private BoatCannon _cannon;
        private BoatController _boat;
        private Vector3 _barrelRestPosition;
        private Vector3 _barrelRecoilDirection;
        private float _elapsed;
        private bool _isRecoiling;
        private PlayerController _cameraOwner;
        private PlayerCamera _camera;

        private void Awake()
        {
            _cannon = GetComponent<BoatCannon>();
            _boat = GetComponent<BoatController>();
            if (_barrelVisual)
            {
                _barrelRestPosition = _barrelVisual.localPosition;
                _barrelRecoilDirection = _barrelVisual.localRotation * Vector3.back;
            }
        }

        private void OnEnable()
        {
            _cannon.Fired += HandleFired;
        }

        private void Update()
        {
            if (!_isRecoiling || !_barrelVisual)
            {
                return;
            }

            _elapsed = Mathf.Min(_elapsed + Time.deltaTime, _returnDuration);
            float weight = 1f - Mathf.SmoothStep(0f, 1f, _elapsed / _returnDuration);
            _barrelVisual.localPosition = _barrelRestPosition + _barrelRecoilDirection * (_barrelDistance * weight);
            _isRecoiling = _elapsed < _returnDuration;
        }

        private void OnDisable()
        {
            _cannon.Fired -= HandleFired;
            _isRecoiling = false;
            if (_barrelVisual)
            {
                _barrelVisual.localPosition = _barrelRestPosition;
            }
            if (_flash)
            {
                _flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (_smoke)
            {
                _smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (_audio)
            {
                _audio.Stop();
            }
        }

        private void HandleFired()
        {
            if (_flash)
            {
                _flash.Play(true);
            }
            if (_smoke)
            {
                _smoke.Play(true);
            }
            if (_audio && _fireClip)
            {
                _audio.PlayOneShot(_fireClip, _volume);
            }

            _elapsed = 0f;
            _isRecoiling = _barrelVisual;

            PlayerController owner = _boat.Owner;
            if (!owner || !owner.isOwned || owner.CurrentBoat != _boat)
            {
                return;
            }
            if (_cameraOwner != owner)
            {
                _cameraOwner = owner;
                _camera = owner.GetComponent<PlayerCamera>();
            }
            if (_camera)
            {
                _camera.PlayCannonRecoil(_cameraPitch, _cameraDistance, _returnDuration);
            }
        }
    }
}
