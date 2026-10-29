using System;
using Mirror;
using UnityEngine;
using Lighthouse.Systems.Audio;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
public sealed class BoatDurability : NetworkBehaviour
{
    [SerializeField, Min(1f)]
    private float _maxDurability = 100f;

    [SerializeField, Range(0f, 1f)]
    private float _reefCollisionVolume = 0.65f;

    [SyncVar(hook = nameof(OnDurabilityChanged))]
    private float _currentDurability;

    [SyncVar]
    private bool _isWrecked;

    [SyncVar]
    private double _wreckTime;

    public float MaxDurability => _maxDurability;
    public float CurrentDurability => _currentDurability;
    public bool IsWrecked => _isWrecked;
    public double WreckTime => _wreckTime;

    public event Action<float, float> DurabilityChanged;
    public event Action Wrecked;

    private BoatShield _shield;

    private void Awake()
    {
        _shield = GetComponent<BoatShield>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        _currentDurability = _maxDurability;
        _isWrecked = false;
        _wreckTime = 0d;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        DurabilityChanged?.Invoke(_currentDurability, _maxDurability);
    }

    [Server]
    public void ApplyDamage(float damage, BoatDamageType damageType = BoatDamageType.None)
    {
        if (_isWrecked || damage <= 0f || float.IsNaN(damage) || float.IsInfinity(damage))
        {
            return;
        }

        if (damageType == BoatDamageType.Reef)
        {
            RpcPlayReefCollisionSound();
        }

        if (_shield && _shield.TryAbsorbDamage())
        {
            return;
        }

        float previousDurability = _currentDurability;
        _currentDurability = Mathf.Max(0f, _currentDurability - damage);

        Debug.Log(
            $"[{nameof(BoatDurability)}] {name} | " +
            $"Damage: {previousDurability - _currentDurability} | " +
            $"Durability: {previousDurability} -> {_currentDurability}/{_maxDurability}",
            this);

        if (_currentDurability <= 0f)
        {
            _wreckTime = NetworkTime.time;
            _isWrecked = true;
            Debug.Log($"[{nameof(BoatDurability)}] {name} | Wrecked", this);
            Wrecked?.Invoke();
        }

    }

    [ClientRpc]
    private void RpcPlayReefCollisionSound()
    {
        if (SoundManager.Instance)
        {
            SoundManager.Instance.PlaySfxAt(SfxType.ReefCollision, transform.position, _reefCollisionVolume);
        }
    }

    private void OnDurabilityChanged(float oldValue, float newValue)
    {
        DurabilityChanged?.Invoke(newValue, _maxDurability);
    }

    [Server]
    public void RestoreDurability(float amount)
    {
        if (_isWrecked || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
        {
            return;
        }

        _currentDurability = Mathf.Min(_maxDurability, _currentDurability + amount);


        Debug.Log($"[{nameof(BoatDurability)}] {name} | " +$"Durability: {_currentDurability} -> {_maxDurability}",this);
    }
}
