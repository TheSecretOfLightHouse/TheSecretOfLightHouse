using System;
using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkIdentity))]
public sealed class BoatDurability : NetworkBehaviour
{
    [SerializeField, Min(1f)]
    private float _maxDurability = 100f;

    [SyncVar(hook = nameof(OnDurabilityChanged))]
    private float _currentDurability;

    [SyncVar]
    private bool _isWrecked;

    public float MaxDurability => _maxDurability;
    public float CurrentDurability => _currentDurability;
    public bool IsWrecked => _isWrecked;

    public event Action<float, float> DurabilityChanged;
    public event Action Wrecked;

    public override void OnStartServer()
    {
        base.OnStartServer();

        _currentDurability = _maxDurability;
        _isWrecked = false;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        DurabilityChanged?.Invoke(_currentDurability, _maxDurability);
    }

    [Server]
    public void ApplyDamage(float damage)
    {
        if (_isWrecked || damage <= 0f || float.IsNaN(damage) || float.IsInfinity(damage))
        {
            return;
        }

        _currentDurability = Mathf.Max(0f, _currentDurability - damage);

        if (_currentDurability <= 0f)
        {
            _isWrecked = true;
            Wrecked?.Invoke();
        }
    }

    private void OnDurabilityChanged(float oldValue, float newValue)
    {
        DurabilityChanged?.Invoke(newValue, _maxDurability);
    }
}
