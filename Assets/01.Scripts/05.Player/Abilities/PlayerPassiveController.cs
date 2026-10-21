using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerPassiveController : NetworkBehaviour
{
    [SerializeField]
    private MonoBehaviour _passiveComponent;

    private PlayerController _player;
    private IPlayerPassive _passive;
    private bool _isBound;
    private bool _isJobAbilityEnabled = true;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
    }

    private void OnEnable()
    {
        if (isServer)
        {
            Bind();
        }
    }

    private void OnDisable()
    {
        Unbind();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Bind();
    }

    public override void OnStopServer()
    {
        Unbind();
        base.OnStopServer();
    }

    [Server]
    public void SetJobAbilityEnabled(bool isEnabled)
    {
        _isJobAbilityEnabled = isEnabled;

        if (_passive != null)
        {
            _passive.Refresh();
        }
    }

    [Server]
    public float GetReloadDurationMultiplier(BoatController boat)
    {
        if (!isActiveAndEnabled || !_isBound || !_isJobAbilityEnabled || !_passiveComponent || !_passiveComponent.isActiveAndEnabled)
        {
            return 1f;
        }

        if (_passive is ICannonReloadModifier modifier)
        {
            return modifier.GetReloadDurationMultiplier(boat);
        }

        return 1f;
    }

    private void Bind()
    {
        if (_isBound || !_player || !_passiveComponent)
        {
            return;
        }

        if (!(_passiveComponent is IPlayerPassive passive))
        {
            return;
        }

        _passive = passive;

        _player.Boarded += HandleBoatChanged;
        _player.Disembarked += HandleBoatChanged;
        _player.DownedChanged += HandleDownedChanged;

        _isBound = true;
        _passive.Attach(_player);
    }

    private void Unbind()
    {
        if (!_isBound)
        {
            return;
        }

        if (_player)
        {
            _player.Boarded -= HandleBoatChanged;
            _player.Disembarked -= HandleBoatChanged;
            _player.DownedChanged -= HandleDownedChanged;
        }

        if (_passiveComponent && _passive != null)
        {
            _passive.Detach();
        }

        _passive = null;
        _isBound = false;
    }

    private void HandleBoatChanged(BoatController boat)
    {
        RefreshPassive();
    }

    private void HandleDownedChanged(bool isDowned)
    {
        RefreshPassive();
    }

    private void RefreshPassive()
    {
        if (_passiveComponent && _passive != null)
        {
            _passive.Refresh();
        }
    }
}