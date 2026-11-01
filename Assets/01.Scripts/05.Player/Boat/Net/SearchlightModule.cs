using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class SearchlightModule : BoatModule
{
    [SerializeField]
    private Light _searchlight;

    [SerializeField]
    private Light _fillLight;

    [SerializeField]
    private Renderer _beamRenderer;

    [SyncVar(hook = nameof(HandleLightOnChanged))]
    private bool _isLightOn = true;

    private BoatController _boat;

    public override BoatModuleType ModuleType => BoatModuleType.Searchlight;

    protected override void Awake()
    {
        base.Awake();
        _boat = GetComponent<BoatController>();

        if (!_beamRenderer && _searchlight)
        {
            _beamRenderer = _searchlight.GetComponentInChildren<MeshRenderer>(true);
        }

        if (_searchlight)
        {
            _searchlight.enabled = false;
        }

        if (_fillLight)
        {
            _fillLight.enabled = false;
        }

        if (_beamRenderer)
        {
            _beamRenderer.enabled = false;
        }
    }

    protected override bool CanApplyEffect()
    {
        return _searchlight && _isLightOn;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        RefreshEffect();
    }

    private void Update()
    {
        if (!isClient || !isOwned || !_boat || Mouse.current == null)
        {
            return;
        }

        PlayerController owner = _boat.Owner;
        if (!owner || !owner.isLocalPlayer || !owner.isActiveAndEnabled || owner.IsDowned
            || !owner.IsOnBoat || owner.CurrentBoat != _boat || !_boat.IsOccupied || _boat.IsWrecked
            || !Equipment.HasModule(ModuleType) || !owner.CanProcessBoatInput
            || Cursor.lockState == CursorLockMode.None
            || Lighthouse.Network.Session.View.SessionScreen.IsGameplayOverlayOpen
            || Lighthouse.Common.Cinematics.CinematicPresentationLock.IsLocked)
        {
            return;
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            CmdToggleLight();
        }
    }

    [Command]
    private void CmdToggleLight(NetworkConnectionToClient sender = null)
    {
        PlayerController owner = _boat ? _boat.Owner : null;
        if (!owner || owner.connectionToClient != sender || owner.IsDowned
            || !owner.IsOnBoat || owner.CurrentBoat != _boat || !_boat.IsOccupied || _boat.IsWrecked
            || !Equipment.HasModule(ModuleType)
            || Lighthouse.Common.Cinematics.ServerCinematicProtection.IsActive)
        {
            return;
        }

        _isLightOn = !_isLightOn;
    }

    private void HandleLightOnChanged(bool previous, bool current)
    {
        RefreshEffect();
    }

    protected override void ApplyEffect()
    {
        if (_searchlight)
        {
            _searchlight.enabled = true;
        }

        if (_fillLight)
        {
            _fillLight.enabled = true;
        }

        if (_beamRenderer)
        {
            _beamRenderer.enabled = true;
        }
    }

    protected override void RemoveEffect()
    {
        if (_searchlight)
        {
            _searchlight.enabled = false;
        }

        if (_fillLight)
        {
            _fillLight.enabled = false;
        }

        if (_beamRenderer)
        {
            _beamRenderer.enabled = false;
        }
    }
}
