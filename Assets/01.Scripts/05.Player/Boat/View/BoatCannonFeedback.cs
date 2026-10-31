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
        [SerializeField] private Transform _hullVisual;
        [SerializeField, Min(0f)] private float _hullPitch = 6f;
        [SerializeField, Min(0.01f)] private float _hullRiseDuration = 0.07f;
        [SerializeField, Min(0.01f)] private float _hullReturnDuration = 0.38f;
        [SerializeField, Min(0f)] private float _collisionTilt = 7f;
        [SerializeField, Min(0.01f)] private float _collisionDuration = 1.8f;
        [SerializeField, Min(1f)] private float _collisionOscillations = 2f;
        [SerializeField, Min(0.01f)] private float _collisionDamping = 3f;

        private BoatCannon _cannon;
        private BoatController _boat;
        private StabilizerModule _stabilizer;
        private Vector3 _barrelRestPosition;
        private Vector3 _barrelRecoilDirection;
        private float _elapsed;
        private bool _isRecoiling;
        private PlayerController _cameraOwner;
        private PlayerCamera _camera;
        private Quaternion _hullRestRotation;
        private float _hullElapsed;
        private bool _isHullRecoiling;
        private Vector3 _collisionAngle;
        private Vector3 _collisionVelocity;
        private float _collisionElapsed;
        private bool _isCollisionRocking;

        private void Awake()
        {
            _cannon = GetComponent<BoatCannon>();
            _boat = GetComponent<BoatController>();
            _stabilizer = GetComponent<StabilizerModule>();
            if (_hullVisual)
            {
                _hullRestRotation = _hullVisual.localRotation;
            }
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
            _isHullRecoiling = false;
            _isCollisionRocking = false;
            _collisionAngle = Vector3.zero;
            _collisionVelocity = Vector3.zero;
            if (_hullVisual)
            {
                _hullVisual.localRotation = _hullRestRotation;
            }
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
            _hullElapsed = 0f;
            _isHullRecoiling = _hullVisual;

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

        private void LateUpdate()
        {
            if ((!_isHullRecoiling && !_isCollisionRocking) || !_hullVisual)
            {
                return;
            }

            float weight = 0f;
            if (_isHullRecoiling)
            {
                _hullElapsed += Time.deltaTime;
                weight = _hullElapsed < _hullRiseDuration
                    ? Mathf.SmoothStep(0f, 1f, _hullElapsed / _hullRiseDuration)
                    : 1f - Mathf.SmoothStep(0f, 1f, (_hullElapsed - _hullRiseDuration) / _hullReturnDuration);
                _isHullRecoiling = _hullElapsed < _hullRiseDuration + _hullReturnDuration;
            }
            Quaternion collisionRotation = Quaternion.identity;
            if (_isCollisionRocking)
            {
                collisionRotation = AdvanceCollisionRock(Time.deltaTime);
            }
            // Pitch in boat space so differently oriented art still lifts the bow.
            _hullVisual.localRotation = collisionRotation
                * Quaternion.AngleAxis(-_hullPitch * weight, Vector3.right) * _hullRestRotation;
        }

        private Quaternion AdvanceCollisionRock(float deltaTime)
        {
            _collisionElapsed += deltaTime;
            float frequency = Mathf.PI * 2f * _collisionOscillations / Mathf.Max(0.01f, _collisionDuration);
            float damping = Mathf.Max(0.01f, _collisionDamping);
            if (_stabilizer)
            {
                damping *= _stabilizer.CollisionDampingMultiplier;
            }
            float decay = Mathf.Exp(-damping * deltaTime);
            float cosine = Mathf.Cos(frequency * deltaTime);
            float sine = Mathf.Sin(frequency * deltaTime);
            // Integrate the damped spring analytically so frame spikes cannot amplify the rocking.
            Vector3 spring = (_collisionVelocity + damping * _collisionAngle) / frequency;
            Vector3 nextAngle = decay * (_collisionAngle * cosine + spring * sine);
            _collisionVelocity = decay * (-_collisionAngle * frequency * sine + spring * frequency * cosine)
                - damping * nextAngle;
            _collisionAngle = nextAngle;
            _isCollisionRocking = _collisionElapsed < _collisionDuration;
            if (!_isCollisionRocking)
            {
                _collisionAngle = Vector3.zero;
                _collisionVelocity = Vector3.zero;
            }
            return Quaternion.Euler(_collisionAngle);
        }

        public void PlayCollisionRock(Vector3 worldDirection, float strength)
        {
            if (!isActiveAndEnabled || !_hullVisual || !_boat || _boat.IsWrecked
                || !float.IsFinite(strength) || !float.IsFinite(worldDirection.x)
                || !float.IsFinite(worldDirection.y) || !float.IsFinite(worldDirection.z))
            {
                return;
            }

            Vector3 localDirection = transform.InverseTransformDirection(
                Vector3.ProjectOnPlane(worldDirection, Vector3.up));
            if (localDirection.sqrMagnitude < 0.0001f)
            {
                return;
            }
            float frequency = Mathf.PI * 2f * _collisionOscillations / Mathf.Max(0.01f, _collisionDuration);
            float tilt = _collisionTilt * (_stabilizer ? _stabilizer.CollisionTiltMultiplier : 1f);
            float impulse = tilt * frequency * Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(strength));
            // Preserve the current lean on repeated hits; the new impact adds angular momentum.
            _collisionVelocity = Vector3.ClampMagnitude(_collisionVelocity
                + Vector3.Cross(Vector3.up, localDirection.normalized) * impulse, tilt * frequency);
            _collisionElapsed = 0f;
            _isCollisionRocking = true;
        }
    }
}
