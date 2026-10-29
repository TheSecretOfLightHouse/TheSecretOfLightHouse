using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerAbilityRunner : MonoBehaviour, IPlayerAbilitySource
{
    [SerializeField]
    private PlayerAbility _jobAbility;

    private PlayerController _player;
    private IPlayerAbility _activeAbility;
    private PlayerPassiveController _passives;

    public bool IsExecuting => IsAvailable(_activeAbility);
    public PlayerAbility JobAbility => _jobAbility;
    public IPlayerAbility CurrentAbility => IsExecuting ? _activeAbility : null;
    public MoveConfig MovementModifier => IsExecuting ? _activeAbility.MovementModifier : MoveConfig.Normal;

    public event Action<IPlayerAbility> AbilityStarted;

    public event Action<IPlayerAbility, bool> AbilityEnded;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _passives = GetComponent<PlayerPassiveController>();
    }

    private void OnEnable()
    {
        _player.AbilityRequested += HandleAbilityRequested;
    }

    private void OnDisable()
    {
        if (_player)
        {
            _player.AbilityRequested -= HandleAbilityRequested;
        }

        Cancel();
    }

    public void SetJobAbility(PlayerAbility ability)
    {
        Cancel();
        _jobAbility = ability;
    }

    public bool TryExecute(IPlayerAbility ability, IPlayerAbilityContext context)
    {
        if (!IsAvailable(ability) ||context == null ||!isActiveAndEnabled ||IsExecuting ||context.IsDowned
            || (_passives && !_passives.IsJobAbilityEnabled))
        {
            return false;
        }
        if (!ability.CanExecute(context))
        {
            return false;
        }

        _activeAbility = ability;

        ability.Begin(context);

        if (!ReferenceEquals(_activeAbility, ability))
            return true;

        if (!IsAvailable(ability))
        {
            Finish(true);
            return true;
        }
        AbilityStarted?.Invoke(ability);

        if (ReferenceEquals(_activeAbility, ability) && IsAvailable(ability) && ability.IsFinished)
        {
            Finish(false);
        }

        return true;
    }

    public void Tick(float deltaTime)
    {
        if (_passives && !_passives.IsJobAbilityEnabled)
        {
            Cancel();
            return;
        }

        if (!IsExecuting)
        {
            _activeAbility = null;
            return;
        }

        IPlayerAbility ability = _activeAbility;

        ability.TickAbility(deltaTime);

        if (ReferenceEquals(_activeAbility, ability) && IsAvailable(ability) && ability.IsFinished)
        {
            Finish(false);
        }
    }

    public void Cancel()
    {
        Finish(true);
    }

    private void HandleAbilityRequested(PlayerController player)
    {
        if (player != _player || !player.isActiveAndEnabled)
        {
            return;
        }

        TryExecute(_jobAbility, player);
    }

    private void Finish(bool cancelled)
    {
        IPlayerAbility ability = _activeAbility;
        _activeAbility = null;

        if (!IsAvailable(ability))
        {
            return;
        }

        if (cancelled)
        {
            ability.Cancel();
        }

        AbilityEnded?.Invoke(ability, cancelled);
    }

    private static bool IsAvailable(IPlayerAbility ability)
    {
        if (ability == null)
        {
            return false;
        }

        if (ability is UnityEngine.Object unityObject)
        {
            return unityObject != null;
        }

        return true;
    }
}
