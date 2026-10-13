using UnityEngine;

public sealed class PlayerCamera : MonoBehaviour
{
    [SerializeField]
    private Transform _cameraAnchor;

    private Transform _attachedCamera;
    private Transform _originalCameraParent;
    private Vector3 _originalCameraPosition;
    private Quaternion _originalCameraRotation;

    [SerializeField]
    private float _minPitch = -60f;
    [SerializeField]
    private float _maxPitch = 70f;

    [SerializeField]
    private Transform _cameraPivot;

    private float _pitch;

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

    public void ApplyVerticalLook(float delta)
    {
        if(!_attachedCamera||!_cameraPivot)
        {
            return;
        }
        _pitch = Mathf.Clamp(_pitch - delta, _minPitch, _maxPitch);
        _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }
}
