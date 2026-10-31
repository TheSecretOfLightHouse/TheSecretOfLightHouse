using Mirror;
using UnityEngine;
using Lighthouse.Systems.Audio;

[DisallowMultipleComponent]
public sealed class DashModule : BoatModule
{
    [SerializeField, Min(0.01f)]
    private float _duration = 0.8f;

    [SerializeField, Min(1f)]
    private float _speedMultiplier = 1.8f;

    [SerializeField, Min(0.01f)]
    private float _cooldown = 5f;

    [SerializeField, Range(0f, 1f)]
    private float _soundVolume = 0.65f;

    [SyncVar]
    private double _nextDashTime;

    [SyncVar]
    private double _dashEndTime;

    public bool IsDashing => Equipment.HasModule(ModuleType) && NetworkTime.time < _dashEndTime;

    public float SpeedMultiplier => _speedMultiplier;

    public override BoatModuleType ModuleType => BoatModuleType.Dash;

    protected override void ApplyEffect()
    {
        Debug.Log($"[{nameof(DashModule)}] Dash module equipped.", this);
    }

    protected override void RemoveEffect()
    {
        Debug.Log($"[{nameof(DashModule)}] Dash module effect removed.", this);
    }

    public void RequestDash()
    {
        if (Lighthouse.Common.Cinematics.CinematicPresentationLock.IsLocked)
        {
            return;
        }
        if (!isClient || !isOwned || !IsApplied)
        {
            return;
        }

        if (NetworkTime.time < _nextDashTime)
        {
            return;
        }

        CmdRequestDash();
    }
    [Command]
    private void CmdRequestDash(NetworkConnectionToClient sender = null)
    {
        if (Lighthouse.Common.Cinematics.ServerCinematicProtection.IsActive)
        {
            return;
        }
        BoatController boat = GetComponent<BoatController>();
        PlayerController player = boat.Owner;

        if (!player || player.connectionToClient != sender)
        {
            return;
        }

        if (player.IsDowned || !player.IsOnBoat || player.CurrentBoat != boat || !boat.IsOccupied || boat.IsWrecked)
        {
            return;
        }

        if (!Equipment.HasModule(ModuleType) || IsDashing || NetworkTime.time < _nextDashTime)
        {
            return;
        }

        double now = NetworkTime.time;
        _dashEndTime = now + _duration;
        _nextDashTime = now + _cooldown;
        RpcPlayDashSound();

        Debug.Log($"[{nameof(DashModule)}] Dash approved by server.", this);
    }

    [ClientRpc]
    private void RpcPlayDashSound()
    {
        if (SoundManager.Instance)
        {
            SoundManager.Instance.PlaySfxAt(SfxType.BoatDash, transform.position, _soundVolume);
        }
    }
}
