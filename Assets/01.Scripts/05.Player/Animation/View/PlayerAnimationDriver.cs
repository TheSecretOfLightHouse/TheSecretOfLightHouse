using UnityEngine;

[RequireComponent(typeof(Animator))]
public sealed class PlayerAnimationDriver : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");

    private Animator _animator;
    private IPlayerAnimationSource _source;
    private bool _subscribed;

    public void Initialize(IPlayerAnimationSource source)
    {
        Unsubscribe();

        _animator = GetComponent<Animator>();
        _source = source;

        if (isActiveAndEnabled)
        {
            Subscribe();
        }
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (_source == null || _subscribed)
        {
            return;
        }

        _source.Changed += HandleChanged;
        _subscribed = true;

        HandleChanged(_source.Current);
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        _source.Changed -= HandleChanged;
        _subscribed = false;
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
        _animator.SetBool(
            IsJumpingHash,
            state == PlayerAnimationState.Jump);
    }
}
