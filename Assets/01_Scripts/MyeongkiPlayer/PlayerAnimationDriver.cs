using UnityEngine;

[RequireComponent(typeof(Animator))]
public sealed class PlayerAnimationDriver : MonoBehaviour
{
    [SerializeField, Min(0f)]
    private float _transitionDuration = 0.15f;

    [Header("Animator 상태 경로")]
    [SerializeField]
    private string _idleState = "Base Layer.Idle";

    [SerializeField]
    private string _walkState = "Base Layer.Walk";

    [SerializeField]
    private string _runState = "Base Layer.Walk";

    [SerializeField]
    private string _hammerState = "Base Layer.Idle";

    [SerializeField]
    private string _gatherState = "Base Layer.Idle";

    [SerializeField]
    private string _repairState = "Base Layer.Idle";

    [SerializeField]
    private string _onBoatState = "Base Layer.Idle";

    [SerializeField]
    private string _downedState = "Base Layer.Idle";

    private Animator _animator;
    private IPlayerAnimationSource _source;

    private bool _subscribed;
    private int _currentHash;

    public void Initialize(IPlayerAnimationSource source)
    {
        Unsubscribe();

        // 다른 컴포넌트의 Awake에서 먼저 호출되어도 준비.
        _animator = GetComponent<Animator>();
        _source = source;

        if (isActiveAndEnabled)
            Subscribe();
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
            return;

        _source.Changed += HandleChanged;
        _subscribed = true;
        PlayState(_source.Current, true);
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
            return;

        _source.Changed -= HandleChanged;
        _subscribed = false;
    }

    private void HandleChanged(PlayerAnimationState state)
    {
        PlayState(state, false);
    }

    private void PlayState(PlayerAnimationState state, bool immediately)
    {
        string path = GetStatePath(state);
        int hash = Animator.StringToHash(path);

        if (_animator.runtimeAnimatorController == null ||!_animator.HasState(0, hash))
        {
            return;
        }

        if (!immediately && _currentHash == hash)
        {
            return;
        }

        _currentHash = hash;

        if (immediately)
        {
            _animator.Play(hash, 0, 0f);
        }
        else
        {
            _animator.CrossFadeInFixedTime(hash,_transitionDuration,0,0f);
        }
    }

    private string GetStatePath(PlayerAnimationState state)
    {
        return state switch
        {
            PlayerAnimationState.Walk => _walkState,
            PlayerAnimationState.Run => _runState,
            //PlayerAnimationState.Hammer => _hammerState,
            //PlayerAnimationState.Gather => _gatherState,
            //PlayerAnimationState.Repair => _repairState,
            PlayerAnimationState.OnBoat => _onBoatState,
            PlayerAnimationState.Downed => _downedState,
            _ => _idleState
        };
    }
}
