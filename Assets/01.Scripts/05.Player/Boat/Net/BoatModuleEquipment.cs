using Mirror;
using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoatController), typeof(BoatDurability))]
public sealed class BoatModuleEquipment : NetworkBehaviour
{
    private readonly SyncList<BoatModuleType> _installedModules = new SyncList<BoatModuleType>();

    private BoatController _boat;
    private BoatDurability _durability;

    public int InstalledCount => _installedModules.Count;

    public event Action<BoatModuleType> ModuleInstalled;
    public event Action<BoatModuleType> ModuleRemoved;

    private void Awake()
    {
        _boat = GetComponent<BoatController>();
        _durability = GetComponent<BoatDurability>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        _installedModules.Clear();
        _durability.Wrecked += HandleWrecked;
    }

    public override void OnStopServer()
    {
        if (_durability)
        {
            _durability.Wrecked -= HandleWrecked;
        }

        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        _installedModules.OnAdd += HandleModuleAdded;
        _installedModules.OnRemove += HandleModuleRemoved;
        _installedModules.OnClear += HandleModulesCleared;

        for (int i = 0; i < _installedModules.Count; i++)
        {
            ModuleInstalled?.Invoke(_installedModules[i]);
        }
    }

    public override void OnStopClient()
    {
        _installedModules.OnAdd -= HandleModuleAdded;
        _installedModules.OnRemove -= HandleModuleRemoved;
        _installedModules.OnClear -= HandleModulesCleared;

        for (int i = 0; i < _installedModules.Count; i++)
        {
            ModuleRemoved?.Invoke(_installedModules[i]);
        }

        base.OnStopClient();
    }

    public bool HasModule(BoatModuleType moduleType)
    {
        return _installedModules.Contains(moduleType);
    }

    [Server]
    public bool CanInstall(PlayerController player, BoatModuleType moduleType)
    {
        if (!player || player.IsDowned)
        {
            return false;
        }

        if (!player.IsOnBoat || player.CurrentBoat != _boat || _boat.Owner != player || !_boat.IsOccupied)
        {
            return false;
        }

        if (_durability.IsWrecked)
        {
            return false;
        }

        if (!IsSupported(moduleType) || HasModule(moduleType))
        {
            return false;
        }

        return true;
    }

    [Server]
    public bool TryInstall(PlayerController player, BoatModuleType moduleType)
    {
        if (!CanInstall(player, moduleType))
        {
            return false;
        }

        _installedModules.Add(moduleType);
        return true;
    }

    private void HandleModuleAdded(int index)
    {
        ModuleInstalled?.Invoke(_installedModules[index]);
    }

    private void HandleModuleRemoved(int index, BoatModuleType moduleType)
    {
        ModuleRemoved?.Invoke(moduleType);
    }

    private void HandleModulesCleared()
    {
        for (int i = 0; i < _installedModules.Count; i++)
        {
            ModuleRemoved?.Invoke(_installedModules[i]);
        }
    }

    [Server]
    private void HandleWrecked()
    {
        _installedModules.Clear();
    }

    private static bool IsSupported(BoatModuleType moduleType)
    {
        switch (moduleType)
        {
            case BoatModuleType.Searchlight:
            case BoatModuleType.DepthAlarm:
            case BoatModuleType.Stabilizer:
            case BoatModuleType.Dash:
                return true;

            default:
                return false;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Test/Install Searchlight (No Inventory)")]
    private void TestInstallSearchlight()
    {
        if (!isServer)
        {
            Debug.LogWarning($"[{nameof(BoatModuleEquipment)}] Use the running server boat.", this);
            return;
        }

        bool installed = TryInstall(_boat.Owner, BoatModuleType.Searchlight);
        Debug.Log($"[{nameof(BoatModuleEquipment)}] Searchlight installed: {installed}", this);
    }

    [ContextMenu("Test/Install Dash (No Inventory)")]
    private void TestInstallDash()
    {
        if (!isServer)
        {
            Debug.LogWarning($"[{nameof(BoatModuleEquipment)}] Use the running server boat.", this);
            return;
        }

        bool installed = TryInstall(_boat.Owner, BoatModuleType.Dash);
        Debug.Log($"[{nameof(BoatModuleEquipment)}] Dash installed: {installed}", this);
    }

    [ContextMenu("Test/Install Stabilizer (No Inventory)")]
    private void TestInstallStabilizer()
    {
        if (!isServer)
        {
            Debug.LogWarning(
                $"[{nameof(BoatModuleEquipment)}] Use the running server boat.",
                this);
            return;
        }

        bool installed = TryInstall(
            _boat.Owner,
            BoatModuleType.Stabilizer);

        Debug.Log(
            $"[{nameof(BoatModuleEquipment)}] Stabilizer installed: {installed}",
            this);
    }

    [ContextMenu("Test/Wreck Boat")]
    private void TestWreckBoat()
    {
        if (isServer)
        {
            _durability.ApplyDamage(_durability.MaxDurability);
        }
    }
#endif
}
