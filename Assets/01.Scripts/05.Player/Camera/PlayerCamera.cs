using UnityEngine;
using UnityEngine.SceneManagement;

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

    [SerializeField, Min(0f)]
    private float _lookSmoothTime = 0.08f;

    [SerializeField]
    private LayerMask _obstacleMask = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0.01f)]
    private float _collisionRadius = 0.2f;
    [SerializeField, Min(0f)]
    private float _collisionPadding = 0.05f;
    [SerializeField, Min(0f)]
    private float _hideCharacterDistance = 0.8f;

    private readonly RaycastHit[] _obstacleHits = new RaycastHit[64];
    private readonly Collider[] _overlaps = new Collider[64];
    private PlayerController _player;
    private Camera _cameraComponent;
    private SphereCollider _collisionProbe;
    private Renderer[] _characterRenderers;
    private bool[] _originalRenderingOff;
    private bool _isCharacterHidden;

    private float _pitch;
    private float _viewPitch;
    private float _viewYaw;
    private float _pitchVelocity;
    private float _yawVelocity;
    private float _onFootYaw;
    private float _orbitYaw;
    private bool _isBoatOrbit;
    private float _lastLookTime;
    private float _recoilPitch;
    private float _recoilDistance;
    private float _recoilDuration;
    private float _recoilElapsed;
    private bool _hasRecoil;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _characterRenderers = GetComponentsInChildren<Renderer>(true);
        _originalRenderingOff = new bool[_characterRenderers.Length];
        var probe = new GameObject("Camera Collision Probe");
        probe.hideFlags = HideFlags.HideAndDontSave;
        probe.layer = 2;
        probe.transform.position = Vector3.down * 100000f;
        _collisionProbe = probe.AddComponent<SphereCollider>();
        _collisionProbe.isTrigger = true;
    }

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
        _cameraComponent = mainCamera;
        _originalCameraParent = _attachedCamera.parent;
        _originalCameraPosition = _attachedCamera.position;
        _originalCameraRotation = _attachedCamera.rotation;

        _attachedCamera.SetParent(_cameraAnchor, false);
        _attachedCamera.localPosition = Vector3.zero;
        _attachedCamera.localRotation = Quaternion.identity;
        _pitch = 0f;
        _onFootYaw = transform.eulerAngles.y;
        ResetLookSmoothing(_isBoatOrbit ? _orbitYaw : _onFootYaw);

        if (_cameraPivot)
        {
            _cameraPivot.localRotation = Quaternion.identity;
        }
    }

    public void Release()
    {
        SetCharacterHidden(false);
        _hasRecoil = false;
        if (!_attachedCamera)
        {
            return;
        }

        _attachedCamera.SetParent(_originalCameraParent, true);
        _attachedCamera.SetPositionAndRotation(_originalCameraPosition, _originalCameraRotation);

        _attachedCamera = null;
        _cameraComponent = null;
        _originalCameraParent = null;
    }

    private void OnDisable()
    {
        Release();
    }

    private void OnDestroy()
    {
        Release();
        if (_collisionProbe)
        {
            Destroy(_collisionProbe.gameObject);
        }
    }

    private void LateUpdate()
    {
        if (_attachedCamera && _cameraPivot)
        {
            if (_isBoatOrbit)
            {
                UpdateBoatLookReturn(transform.eulerAngles.y, Time.deltaTime);
            }

            // Cancel inherited body yaw before the camera anchor is rendered.
            float yaw = _isBoatOrbit ? _orbitYaw : _onFootYaw;
            if (_lookSmoothTime <= 0f)
            {
                ResetLookSmoothing(yaw);
            }
            else if (Time.deltaTime > 0f)
            {
                _viewYaw = Mathf.SmoothDampAngle(
                    _viewYaw, yaw, ref _yawVelocity, _lookSmoothTime, Mathf.Infinity, Time.deltaTime);
                _viewPitch = Mathf.SmoothDamp(
                    _viewPitch, _pitch, ref _pitchVelocity, _lookSmoothTime, Mathf.Infinity, Time.deltaTime);
            }
            _cameraPivot.rotation = Quaternion.Euler(_viewPitch, _viewYaw, 0f);
        }

        if (!_attachedCamera)
        {
            return;
        }

        Vector3 recoilOffset = Vector3.zero;
        Quaternion recoilRotation = Quaternion.identity;
        if (_hasRecoil)
        {
            _recoilElapsed = Mathf.Min(_recoilElapsed + Time.deltaTime, _recoilDuration);
            float progress = _recoilElapsed / _recoilDuration;
            float weight = 1f - Mathf.SmoothStep(0f, 1f, progress);
            recoilOffset = Vector3.back * (_recoilDistance * weight);
            recoilRotation = Quaternion.Euler(-_recoilPitch * weight, 0f, 0f);
            _hasRecoil = progress < 1f;
        }

        _attachedCamera.localRotation = recoilRotation;
        _attachedCamera.position = GetUnobstructedPosition(_cameraAnchor.TransformPoint(recoilOffset));
        SetCharacterHidden(_cameraPivot &&
            Vector3.Distance(_attachedCamera.position, _cameraPivot.position) < _hideCharacterDistance);
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
        ResetLookSmoothing(heading);
        if (!_isBoatOrbit && _cameraPivot)
        {
            _onFootYaw = heading;
            _cameraPivot.rotation = Quaternion.Euler(_pitch, _onFootYaw, 0f);
        }
    }

    public void SetOnFootHeading(float heading)
    {
        _onFootYaw = heading;
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

    private void ResetLookSmoothing(float yaw)
    {
        _viewYaw = yaw;
        _viewPitch = _pitch;
        _yawVelocity = 0f;
        _pitchVelocity = 0f;
    }

    private Vector3 GetUnobstructedPosition(Vector3 desiredPosition)
    {
        if (!_cameraPivot)
        {
            return desiredPosition;
        }

        Vector3 origin = _cameraPivot.position;
        Vector3 offset = desiredPosition - origin;
        float distance = offset.magnitude;
        if (distance <= Mathf.Epsilon)
        {
            return origin;
        }

        float radius = Mathf.Max(0.01f, _collisionRadius);
        if (_cameraComponent)
        {
            // Enclose the near plane so its corners cannot peek through a wall.
            float near = _cameraComponent.nearClipPlane;
            float halfHeight = _cameraComponent.orthographic
                ? _cameraComponent.orthographicSize
                : near * Mathf.Tan(_cameraComponent.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * _cameraComponent.aspect;
            radius = Mathf.Max(radius, Mathf.Sqrt(near * near + halfHeight * halfHeight + halfWidth * halfWidth));
        }

        PhysicsScene physicsScene = gameObject.scene.GetPhysicsScene();
        int overlapCount = physicsScene.OverlapSphere(
            origin, radius, _overlaps, _obstacleMask, QueryTriggerInteraction.Ignore);
        // A full buffer cannot prove that all environmental obstacles were inspected.
        if (overlapCount == _overlaps.Length)
        {
            return origin;
        }
        if (_collisionProbe)
        {
            _collisionProbe.radius = radius + _collisionPadding;
            // Recover a valid cast origin instead of placing the camera inside the character.
            for (int iteration = 0; iteration < 4; iteration++)
            {
                bool moved = false;
                for (int i = 0; i < overlapCount; i++)
                {
                    Collider obstacle = _overlaps[i];
                    if (!IsIgnoredObstacle(obstacle) && Physics.ComputePenetration(
                        _collisionProbe, origin, Quaternion.identity,
                        obstacle, obstacle.transform.position, obstacle.transform.rotation,
                        out Vector3 separation, out float depth))
                    {
                        origin += separation * (depth + _collisionPadding);
                        moved = true;
                    }
                }
                if (!moved)
                {
                    break;
                }
                overlapCount = physicsScene.OverlapSphere(origin, radius, _overlaps,
                    _obstacleMask, QueryTriggerInteraction.Ignore);
                if (overlapCount == _overlaps.Length)
                {
                    return origin;
                }
            }
        }

        offset = desiredPosition - origin;
        distance = offset.magnitude;
        if (distance <= Mathf.Epsilon)
        {
            return origin;
        }
        Vector3 direction = offset / distance;
        int hitCount = physicsScene.SphereCast(
            origin, radius, direction, _obstacleHits, distance, _obstacleMask, QueryTriggerInteraction.Ignore);
        if (hitCount == _obstacleHits.Length)
        {
            return origin;
        }
        float allowedDistance = distance;
        for (int i = 0; i < hitCount; i++)
        {
            if (!IsIgnoredObstacle(_obstacleHits[i].collider))
            {
                allowedDistance = Mathf.Min(allowedDistance,
                    Mathf.Max(0f, _obstacleHits[i].distance - _collisionPadding));
            }
        }
        return origin + direction * allowedDistance;
    }

    private bool IsIgnoredObstacle(Collider obstacle)
    {
        if (!obstacle || obstacle == _collisionProbe || obstacle.transform.IsChildOf(transform))
        {
            return true;
        }
        BoatController boat = _player ? _player.CurrentBoat : null;
        return boat && obstacle.transform.IsChildOf(boat.transform);
    }

    private void SetCharacterHidden(bool hidden)
    {
        if (_isCharacterHidden == hidden || _characterRenderers == null)
        {
            return;
        }
        for (int i = 0; i < _characterRenderers.Length; i++)
        {
            Renderer characterRenderer = _characterRenderers[i];
            if (!characterRenderer)
            {
                continue;
            }
            if (hidden)
            {
                _originalRenderingOff[i] = characterRenderer.forceRenderingOff;
                characterRenderer.forceRenderingOff = true;
            }
            else
            {
                characterRenderer.forceRenderingOff = _originalRenderingOff[i];
            }
        }
        _isCharacterHidden = hidden;
    }
}
