using Mirror;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerPassiveController : NetworkBehaviour
{
    [SerializeField]
    private MonoBehaviour _passiveComponent;

    [Header("Lighthouse Rewards")]
    [SerializeField, Min(0f)]
    private float _towerBoatSpeedBonus = 1.5f;
    [SerializeField, Min(2)]
    private int _towerHarvestBonusInterval = 5;
    [SerializeField, Min(1)]
    private int _towerHarvestBonusAmount = 1;
    [SerializeField, Min(1f)]
    private float _towerCannonDamageMultiplier = 1.2f;

    private PlayerController _player;
    private IPlayerPassive _passive;
    private bool _isBound;

    [SyncVar(hook = nameof(OnJobAbilityEnabledChanged))]
    private bool _isJobAbilityEnabled = true;

    [SyncVar]
    private int _towerRewardTier;

    private bool _areTowerRewardsFrozen;
    private int _successfulRewardHarvests;

    private PlayerAbilityRunner _abilityRunner;
    private TowerNet _tower;
    private TowerMatchFlow _matchFlow;
    private TowerMatchNet _match;

    public bool IsJobAbilityEnabled => _isJobAbilityEnabled && (!isServer || !IsSummonedMafia());
    public int TowerRewardTier => _towerRewardTier;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _abilityRunner = GetComponent<PlayerAbilityRunner>();
    }

    [ServerCallback]
    private void Update()
    {
        RefreshTowerRewards();

        if (_isJobAbilityEnabled && IsSummonedMafia())
        {
            SetJobAbilityEnabled(false);
        }
    }

    private bool IsSummonedMafia()
    {
        if (!_player || _player.ActorID == 0 || !TryResolveTower())
        {
            return false;
        }

        return _match && _match.Phase == TowerPhase.FinalBattle
            && _matchFlow
            && _matchFlow.RevealedMafiaId == _player.ActorID;
    }

    private bool TryResolveTower()
    {
        if (!WorldServices.TryGet<TowerNet>(out TowerNet tower) || !tower)
        {
            tower = _tower;

            if (!tower)
            {
                // Fixed test scenes do not register the dynamically built island's facility service.
                foreach (NetworkIdentity identity in NetworkServer.spawned.Values)
                {
                    if (identity && identity.TryGetComponent(out tower))
                    {
                        break;
                    }
                }
            }
        }

        if (!tower)
        {
            return false;
        }

        if (_tower != tower)
        {
            ReleaseTower();
            _tower = tower;
            _matchFlow = tower.GetComponent<TowerMatchFlow>();
            _match = tower.GetComponent<TowerMatchNet>();

            _tower.Changed += HandleTowerChanged;
            if (_match)
            {
                _match.FinalBattleStarted += HandleTowerFinalBattleStarted;
            }
        }

        return true;
    }

    private void ReleaseTower()
    {
        if (_tower)
        {
            _tower.Changed -= HandleTowerChanged;
        }

        if (_match)
        {
            _match.FinalBattleStarted -= HandleTowerFinalBattleStarted;
        }

        _tower = null;
        _matchFlow = null;
        _match = null;
    }

    [Server]
    private void RefreshTowerRewards()
    {
        if (!TryResolveTower() || !_match || _areTowerRewardsFrozen)
        {
            return;
        }

        if (_match.Phase == TowerPhase.FinalBattle)
        {
            HandleTowerFinalBattleStarted();
            return;
        }

        if (_match.Phase == TowerPhase.Ended)
        {
            _areTowerRewardsFrozen = true;
            return;
        }

        _towerRewardTier = Mathf.Max(_towerRewardTier, _tower.RewardTier);
    }

    [Server]
    private void HandleTowerChanged()
    {
        RefreshTowerRewards();
    }

    [Server]
    private void HandleTowerFinalBattleStarted()
    {
        if (_areTowerRewardsFrozen)
        {
            return;
        }

        // Capture before battle repairs can unlock another tier; corrosion never revokes an earned tier.
        _towerRewardTier = Mathf.Max(_towerRewardTier, _tower.RewardTier);
        _areTowerRewardsFrozen = true;
    }

    private int GetAvailableTowerRewardTier()
    {
        if (isServer)
        {
            RefreshTowerRewards();
        }

        return isActiveAndEnabled && IsJobAbilityEnabled ? _towerRewardTier : 0;
    }

    public float GetTowerBoatSpeedBonus()
    {
        return GetAvailableTowerRewardTier() >= 1
            && !float.IsNaN(_towerBoatSpeedBonus) && !float.IsInfinity(_towerBoatSpeedBonus)
            ? Mathf.Max(0f, _towerBoatSpeedBonus) : 0f;
    }

    public float GetTowerCannonDamageMultiplier()
    {
        return GetAvailableTowerRewardTier() >= 3
            && !float.IsNaN(_towerCannonDamageMultiplier) && !float.IsInfinity(_towerCannonDamageMultiplier)
            ? Mathf.Max(1f, _towerCannonDamageMultiplier) : 1f;
    }

    [Server]
    public int GetTowerHarvestBonusForNextSuccess()
    {
        return GetAvailableTowerRewardTier() >= 2
            && _successfulRewardHarvests >= Mathf.Max(2, _towerHarvestBonusInterval) - 1
            ? Mathf.Max(1, _towerHarvestBonusAmount) : 0;
    }

    [Server]
    public void RegisterSuccessfulTowerHarvest()
    {
        if (GetAvailableTowerRewardTier() < 2)
        {
            return;
        }

        _successfulRewardHarvests++;
        if (_successfulRewardHarvests >= Mathf.Max(2, _towerHarvestBonusInterval))
        {
            _successfulRewardHarvests = 0;
        }
    }

    private void OnJobAbilityEnabledChanged(bool oldValue, bool newValue)
    {
        ApplyJobAbilityState();
    }

    private void ApplyJobAbilityState()
    {
        if (!_isJobAbilityEnabled && _abilityRunner)
        {
            _abilityRunner.Cancel();
        }

        RefreshPassive();
    }

    private void OnEnable()
    {
        if (isServer || isOwned)
        {
            Bind();
        }
    }

    private void OnDisable()
    {
        Unbind();
        ReleaseTower();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _towerRewardTier = 0;
        _successfulRewardHarvests = 0;
        _areTowerRewardsFrozen = false;
        Bind();
        RefreshTowerRewards();
    }

    public override void OnStartAuthority()
    {
        base.OnStartAuthority();
        Bind();
        ApplyJobAbilityState();
    }

    public override void OnStopAuthority()
    {
        if (!isServer)
        {
            Unbind();
        }

        base.OnStopAuthority();
    }

    public override void OnStopServer()
    {
        ReleaseTower();
        Unbind();
        base.OnStopServer();
    }

    [Server]
    public void SetJobAbilityEnabled(bool isEnabled)
    {
        _isJobAbilityEnabled = isEnabled;
        ApplyJobAbilityState();
    }

    public float GetReloadDurationMultiplier(BoatController boat)
    {
        if (!isActiveAndEnabled || !_isBound || !IsJobAbilityEnabled || !_passiveComponent || !_passiveComponent.isActiveAndEnabled)
        {
            return 1f;
        }

        if (_passive is ICannonReloadModifier modifier)
        {
            return modifier.GetReloadDurationMultiplier(boat);
        }

        return 1f;
    }

    public float GetRepairAmountMultiplier(BoatController boat)
    {
        if (!isActiveAndEnabled || !_isBound
            || !IsJobAbilityEnabled || !_passiveComponent
            || !_passiveComponent.isActiveAndEnabled)
        {
            return 1f;
        }

        if (_passive is IBoatRepairModifier modifier)
        {
            return modifier.GetRepairAmountMultiplier(boat);
        }

        return 1f;
    }

    public float GetBoatSpeedMultiplier(BoatController boat)
    {
        if (!isActiveAndEnabled || !_isBound
            || !IsJobAbilityEnabled || !_passiveComponent
            || !_passiveComponent.isActiveAndEnabled)
        {
            return 1f;
        }

        if (_passive is IBoatSpeedModifier modifier)
        {
            return modifier.GetBoatSpeedMultiplier(boat);
        }

        return 1f;
    }

    public int GetStackCapacity()
    {
        if (!isActiveAndEnabled || !_isBound
            || !IsJobAbilityEnabled || !_passiveComponent
            || !_passiveComponent.isActiveAndEnabled)
        {
            return InventoryData.StackCapacity;
        }

        if (_passive is IInventoryStackModifier modifier)
        {
            return modifier.GetStackCapacity();
        }

        return InventoryData.StackCapacity;
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
