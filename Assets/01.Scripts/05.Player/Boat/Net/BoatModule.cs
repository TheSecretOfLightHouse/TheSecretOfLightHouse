using UnityEngine;

[RequireComponent(typeof(BoatModuleEquipment))]
public abstract class BoatModule : MonoBehaviour
{
    private BoatModuleEquipment _equipment;
    private bool _isApplied;

    public abstract BoatModuleType ModuleType { get; }
    public bool IsApplied => _isApplied;
    protected BoatModuleEquipment Equipment => _equipment;

    protected virtual void Awake()
    {
        _equipment = GetComponent<BoatModuleEquipment>();
    }

    protected virtual void OnEnable()
    {
        _equipment.ModuleInstalled += HandleModuleInstalled;
        _equipment.ModuleRemoved += HandleModuleRemoved;
        RefreshEffect();
    }

    protected virtual void OnDisable()
    {
        if (_equipment)
        {
            _equipment.ModuleInstalled -= HandleModuleInstalled;
            _equipment.ModuleRemoved -= HandleModuleRemoved;
        }

        SetApplied(false);
    }

    public void RefreshEffect()
    {
        bool shouldApply = isActiveAndEnabled && _equipment && _equipment.netIdentity
            && _equipment.isClient && _equipment.HasModule(ModuleType)
            && CanApplyEffect();

        SetApplied(shouldApply);
    }

    protected virtual bool CanApplyEffect()
    {
        return true;
    }

    protected abstract void ApplyEffect();
    protected abstract void RemoveEffect();

    private void HandleModuleInstalled(BoatModuleType moduleType)
    {
        if (moduleType == ModuleType)
        {
            RefreshEffect();
        }
    }

    private void HandleModuleRemoved(BoatModuleType moduleType)
    {
        if (moduleType == ModuleType)
        {
            SetApplied(false);
        }
    }

    private void SetApplied(bool shouldApply)
    {
        if (_isApplied == shouldApply)
        {
            return;
        }

        if (shouldApply)
        {
            ApplyEffect();
        }
        else
        {
            RemoveEffect();
        }

        _isApplied = shouldApply;
    }
}