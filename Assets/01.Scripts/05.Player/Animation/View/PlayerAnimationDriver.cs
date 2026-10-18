using UnityEngine;

[RequireComponent(typeof(Animator))]
public sealed class PlayerAnimationDriver : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
    private static readonly int IsDownedHash = Animator.StringToHash("IsDowned");
    private static readonly int IsOnBoatHash = Animator.StringToHash("IsOnBoat");
    private static readonly int IsDiggingHash = Animator.StringToHash("IsDigging");
    private static readonly int PickupHash = Animator.StringToHash("Pickup");
    private static readonly int FireHash = Animator.StringToHash("Fire");
    private static readonly int StumbleHash = Animator.StringToHash("Stumble");
    private static readonly int StumbleSequenceHash = Animator.StringToHash("StumbleSequence");

    [SerializeField]
    private PlayerActionEvents _actionEvents;

    private Animator _animator;
    private int _lastStumbleSequence;
    private IPlayerAnimationSource _source;
    private bool _stateSubscribed;
    private bool _actionSubscribed;

    private void Awake()
    {
        _animator = GetComponent<Animator>();

        if (_actionEvents == null)
        {
            _actionEvents = GetComponentInParent<PlayerActionEvents>();
        }
    }

    public void Initialize(IPlayerAnimationSource source)
    {
        UnsubscribeState();

        _animator = GetComponent<Animator>();
        _source = source;

        if (isActiveAndEnabled)
        {
            SubscribeState();
        }
    }

    private void OnEnable()
    {
        SubscribeState();
        SubscribeActions();
    }

    private void OnDisable()
    {
        UnsubscribeState();
        UnsubscribeActions();
    }

    private void Update()
    {
        if (!_animator || !_animator.runtimeAnimatorController)
        {
            return;
        }

        // NetworkAnimator replicates integer parameters, including repeated collision events.
        int sequence = _animator.GetInteger(StumbleSequenceHash);
        if (sequence == _lastStumbleSequence)
        {
            return;
        }

        _lastStumbleSequence = sequence;
        if (_animator.GetBool(IsOnBoatHash) && !_animator.GetBool(IsDownedHash))
        {
            _animator.ResetTrigger(FireHash);
            _animator.ResetTrigger(StumbleHash);
            _animator.SetTrigger(StumbleHash);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeState();
        UnsubscribeActions();
    }

    private void SubscribeState()
    {
        if (_source == null || _stateSubscribed)
        {
            return;
        }

        _source.Changed += HandleChanged;
        _stateSubscribed = true;

        HandleChanged(_source.Current);
    }

    private void UnsubscribeState()
    {
        if (!_stateSubscribed)
        {
            return;
        }

        _source.Changed -= HandleChanged;
        _stateSubscribed = false;
    }

    private void SubscribeActions()
    {
        if (_actionEvents == null || _actionSubscribed)
        {
            return;
        }

        _actionEvents.Performed += HandleAction;
        _actionSubscribed = true;
    }

    private void UnsubscribeActions()
    {
        if (!_actionSubscribed)
        {
            return;
        }

        if (_actionEvents != null)
        {
            _actionEvents.Performed -= HandleAction;
        }

        _actionSubscribed = false;
    }

    private void HandleChanged(PlayerAnimationState state)
    {
        if (!_animator || !_animator.runtimeAnimatorController)
        {
            return;
        }

        float speed = state switch
        {
            PlayerAnimationState.Walk => 0.5f,
            PlayerAnimationState.Run => 1f,
            _ => 0f
        };

        _animator.SetFloat(SpeedHash, speed);
        _animator.SetBool(IsJumpingHash, state == PlayerAnimationState.Jump);
        _animator.SetBool(IsDownedHash, state == PlayerAnimationState.Downed);
        _animator.SetBool(IsOnBoatHash, state == PlayerAnimationState.OnBoat);

        if (state == PlayerAnimationState.Downed || state == PlayerAnimationState.OnBoat)
        {
            _animator.ResetTrigger(IsDiggingHash);
            _animator.ResetTrigger(PickupHash);
        }

        if (state != PlayerAnimationState.OnBoat)
        {
            _animator.ResetTrigger(FireHash);
            _animator.ResetTrigger(StumbleHash);
        }
    }

    private void HandleAction(PlayerAnimationAction action)
    {
        if (!_animator || !_animator.runtimeAnimatorController)
        {
            return;
        }

        if ((action == PlayerAnimationAction.Dig ||
             action == PlayerAnimationAction.Pickup) &&
            (_animator.GetBool(IsDownedHash) || _animator.GetBool(IsOnBoatHash)))
        {
            return;
        }

        switch (action)
        {
            case PlayerAnimationAction.Dig:
                _animator.ResetTrigger(PickupHash);
                _animator.ResetTrigger(IsDiggingHash);
                _animator.SetTrigger(IsDiggingHash);
                break;

            case PlayerAnimationAction.Pickup:
                _animator.ResetTrigger(IsDiggingHash);
                _animator.ResetTrigger(PickupHash);
                _animator.SetTrigger(PickupHash);
                break;

            case PlayerAnimationAction.Fire:
                if (_animator.GetCurrentAnimatorStateInfo(0).IsName("Stumble") ||
                    (_animator.IsInTransition(0) && _animator.GetNextAnimatorStateInfo(0).IsName("Stumble")))
                {
                    return;
                }
                if (!_animator.GetBool(IsDownedHash) && _animator.GetBool(IsOnBoatHash))
                {
                    _animator.ResetTrigger(FireHash);
                    _animator.SetTrigger(FireHash);
                }
                break;

            case PlayerAnimationAction.Stumble:
                if (!_animator.GetBool(IsDownedHash) && _animator.GetBool(IsOnBoatHash))
                {
                    _animator.ResetTrigger(FireHash);
                    _animator.ResetTrigger(StumbleHash);
                    _animator.SetInteger(StumbleSequenceHash,
                        unchecked(_animator.GetInteger(StumbleSequenceHash) + 1));
                }
                break;
        }
    }
}
