using UnityEngine;

[DefaultExecutionOrder(100)]
public sealed class PlayerCamera : MonoBehaviour
{
    [SerializeField]
    private Transform _cameraAnchor;

    private Transform _attachedCamera;
    private Transform _originalCameraParent;
    private Vector3 _originalCameraPosition;
    private Quaternion _originalCameraRotation;

    [SerializeField]
    private float _minPitch = -10f;
    [SerializeField]
    private float _maxPitch = 70f;

    [SerializeField]
    private Transform _cameraPivot;

    [SerializeField, Min(0f)]
    private float _boatLookReturnDelay = 0.5f;
    [SerializeField, Min(0f)]
    private float _boatLookReturnSpeed = 120f;

    private float _pitch;
    private float _orbitYaw;
    private bool _isBoatOrbit;
    private float _lastLookTime;
    private float _recoilPitch;
    private float _recoilDistance;
    private float _recoilDuration;
    private float _recoilElapsed;
    private bool _hasRecoil;

    public void Attach()
    {
        if (_attachedCamera || !_cameraAnchor)
        {
            return;
        }

        Camera mainCamera = Camera.main;

        if (!mainCamera)
        {
            return;
        }

        _attachedCamera = mainCamera.transform;
        _originalCameraParent = _attachedCamera.parent;
        _originalCameraPosition = _attachedCamera.position;
        _originalCameraRotation = _attachedCamera.rotation;

        _attachedCamera.SetParent(_cameraAnchor, false);
        _attachedCamera.localPosition = Vector3.zero;
        _attachedCamera.localRotation = Quaternion.identity;
        _pitch = 0f;

        if (_cameraPivot)
        {
            _cameraPivot.localRotation = Quaternion.identity;
        }
    }

    public void Release()
    {
        _hasRecoil = false;
        if (!_attachedCamera)
        {
            return;
        }

        _attachedCamera.SetParent(_originalCameraParent, true);
        _attachedCamera.SetPositionAndRotation(_originalCameraPosition, _originalCameraRotation);

        _attachedCamera = null;
        _originalCameraParent = null;
    }

    private void OnDisable()
    {
        Release();
    }

    private void OnDestroy()
    {
        Release();
    }

    private void LateUpdate()
    {
        if (_isBoatOrbit && _attachedCamera && _cameraPivot)
        {
            UpdateBoatLookReturn(transform.eulerAngles.y, Time.deltaTime);
            _cameraPivot.rotation = Quaternion.Euler(_pitch, _orbitYaw, 0f);
        }

        if (_attachedCamera && _hasRecoil)
        {
            _recoilElapsed = Mathf.Min(_recoilElapsed + Time.deltaTime, _recoilDuration);
            float progress = _recoilElapsed / _recoilDuration;
            float weight = 1f - Mathf.SmoothStep(0f, 1f, progress);
            _attachedCamera.localPosition = Vector3.back * (_recoilDistance * weight);
            _attachedCamera.localRotation = Quaternion.Euler(-_recoilPitch * weight, 0f, 0f);
            _hasRecoil = progress < 1f;
        }
    }

    public void PlayCannonRecoil(float pitch, float distance, float duration)
    {
        if (!_attachedCamera || !isActiveAndEnabled)
        {
            return;
        }

        _recoilPitch = Mathf.Max(0f, pitch);
        _recoilDistance = Mathf.Max(0f, distance);
        _recoilDuration = Mathf.Max(0.01f, duration);
        _recoilElapsed = 0f;
        _hasRecoil = true;
    }

    public void SetBoatOrbit(bool isBoatOrbit, float heading)
    {
        _isBoatOrbit = isBoatOrbit;
        _orbitYaw = heading;
        _lastLookTime = Time.time - _boatLookReturnDelay;
        if (!_isBoatOrbit && _cameraPivot)
        {
            _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }

    public void ApplyHorizontalLook(float delta)
    {
        if (_isBoatOrbit && _attachedCamera)
        {
            _orbitYaw = Mathf.Repeat(_orbitYaw + delta, 360f);
            if (delta != 0f)
            {
                _lastLookTime = Time.time;
            }
        }
    }

    public void ApplyVerticalLook(float delta)
    {
        if(!_attachedCamera||!_cameraPivot)
        {
            return;
        }
        _pitch = Mathf.Clamp(_pitch - delta, _minPitch, _maxPitch);
        if (_isBoatOrbit && delta != 0f)
        {
            _lastLookTime = Time.time;
        }
        if (!_isBoatOrbit)
        {
            _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }

    private void UpdateBoatLookReturn(float heading, float deltaTime)
    {
        if (Time.time - _lastLookTime < _boatLookReturnDelay)
        {
            return;
        }

        float step = _boatLookReturnSpeed * deltaTime;
        _orbitYaw = Mathf.MoveTowardsAngle(_orbitYaw, heading, step);
        _pitch = Mathf.MoveTowards(_pitch, Mathf.Clamp(0f, _minPitch, _maxPitch), step);
    }
}
