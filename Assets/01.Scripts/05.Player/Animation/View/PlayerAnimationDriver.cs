using Lighthouse.World.Ocean;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;
using Lighthouse.Systems.Audio;
using Mirror;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.Islands.View;
using Lighthouse.World.MapGeneration.Net;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Query.Rules;

[RequireComponent(typeof(Animator))]
public sealed class PlayerAnimationDriver : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int LocomotionRateHash = Animator.StringToHash("LocomotionRate");
    private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
    private static readonly int IsFallingHash = Animator.StringToHash("IsFalling");
    private static readonly int IsDownedHash = Animator.StringToHash("IsDowned");
    private static readonly int IsOnBoatHash = Animator.StringToHash("IsOnBoat");
    private static readonly int IsDiggingHash = Animator.StringToHash("IsDigging");
    private static readonly int PickupHash = Animator.StringToHash("Pickup");
    private static readonly int FireHash = Animator.StringToHash("Fire");
    private static readonly int StumbleHash = Animator.StringToHash("Stumble");
    private static readonly int StumbleSequenceHash = Animator.StringToHash("StumbleSequence");

    [SerializeField]
    private PlayerActionEvents _actionEvents;

    [SerializeField, Min(0f)]
    private float _fallAnimationDelay = 0.08f;

    [SerializeField, Min(0f)]
    private float _fallSpeedThreshold = 0.1f;

    [SerializeField, Min(0.1f)]
    private float _walkMoveSpeed = 3f;

    [SerializeField, Min(0.1f)]
    private float _runMoveSpeed = 4.5f;

    [SerializeField, Min(0.1f)]
    private float _walkClipSpeed = 2f;

    [SerializeField, Min(0.1f)]
    private float _runClipSpeed = 3f;

    [SerializeField, Min(0f)]
    private float _stopSpeedThreshold = 0.08f;

    [SerializeField, Min(0.1f)]
    private float _walkBlendSpeed = 0.5f;

    [SerializeField, Min(0.1f)]
    private float _minimumLocomotionRate = 0.65f;

    [SerializeField, Min(0.1f)]
    private float _maximumLocomotionRate = 1.5f;

    [SerializeField] private SoundManager _soundManagerPrefab;
    [SerializeField, Range(0f, 1f)] private float _walkVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float _runVolume = 0.65f;
    [SerializeField, Range(0f, 1f)] private float _actionVolume = 0.65f;
    [SerializeField, Min(0.01f)] private float _minimumStepInterval = 0.12f;
    [SerializeField] private IslandFootstepEntry[] _islandFootsteps = System.Array.Empty<IslandFootstepEntry>();
    [SerializeField] private AudioClip _boatMovementClip;
    [SerializeField, Range(0f, 1f)] private float _boatMovementVolume = 0.3f;
    [SerializeField, Min(0.01f)] private float _boatMovementSpeedThreshold = 0.15f;
    [SerializeField, Min(0.01f)] private float _boatMovementFadeTime = 0.25f;
    [SerializeField] private AudioClip _swimMovementClip;
    [SerializeField, Range(0f, 1f)] private float _swimMovementVolume = 0.3f;
    [SerializeField, Min(0.01f)] private float _swimMovementSpeedThreshold = 0.15f;
    [SerializeField, Min(0.01f)] private float _swimMovementFadeTime = 0.25f;

    [SerializeField, Range(0f, 1f)]
    private float _waterEnterFraction = 0.25f;

    [SerializeField, Range(0f, 1f)]
    private float _waterExitFraction = 0.1f;

    private Animator _animator;
    private PlayerController _controller;
    private PlayerMotor _motor;
    private Rigidbody _body;
    private BuoyancyObject _buoyancy;
    private bool _isInWater;
    private float _fallDuration;
    private int _lastStumbleSequence;
    private IPlayerAnimationSource _source;
    private bool _stateSubscribed;
    private bool _actionSubscribed;
    private PlayerAnimationState _previousAnimationState;
    private BoatController _boardedBoat;
    private bool _hasAnimationState;
    private float _nextStepTime;
    private AudioSource _boatMovementAudio;
    private float _boatMovementGain;
    private AudioSource _swimMovementAudio;
    private float _swimMovementGain;
    private Collider _footstepGround;
    private MapLayout _footstepLayout;
    private int _footstepArchetypeId = -1;

    [System.Serializable]
    private struct IslandFootstepEntry
    {
        [SerializeField] private IslandArchetype _archetype;
        [SerializeField] private AudioClip _walkClip;
        [SerializeField] private AudioClip _runClip;

        public IslandArchetype Archetype => _archetype;
        public AudioClip GetClip(bool running) => running ? _runClip : _walkClip;
    }

    private bool CanPlaySound => _controller && _controller.isActiveAndEnabled
        && !(NetworkServer.active && !NetworkClient.active)
        && (!NetworkClient.active || _controller.isClient)
        && (!_controller.isLocalPlayer || _controller.CanProcessBoatInput)
        && (NetworkClient.active || _controller.CanProcessBoatInput);

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _controller = GetComponentInParent<PlayerController>();
        _motor = GetComponentInParent<PlayerMotor>();
        _body = GetComponentInParent<Rigidbody>();
        _buoyancy = GetComponentInParent<BuoyancyObject>();

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
        _hasAnimationState = false;
        _boardedBoat = null;

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

    private void Start()
    {
        EnsureSoundManager();
    }

    private void OnDisable()
    {
        StopBoatMovementSound();
        StopSwimMovementSound();
        _fallDuration = 0f;
        _isInWater = false;
        _hasAnimationState = false;
        _boardedBoat = null;
        _nextStepTime = 0f;
        UnsubscribeState();
        UnsubscribeActions();
    }

    private void Update()
    {
        UpdateBoatMovementSound();
        UpdateWater();
        UpdateSwimMovementSound();
        if (!_animator || !_animator.runtimeAnimatorController)
        {
            return;
        }

        UpdateFalling();
        UpdateLocomotion();

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

    public void PlayHarvestImpact()
    {
        if (_actionEvents && _controller && !_controller.IsDowned && !_controller.IsOnBoat)
        {
            _actionEvents.PublishHarvestImpact();
        }
    }

    public void PlayFootstep(AnimationEvent animationEvent)
    {
        if (!CanPlaySound || !_animator || !_animator.runtimeAnimatorController
            || Time.time < _nextStepTime || _animator.GetFloat(SpeedHash) <= 0f
            || _animator.GetBool(IsJumpingHash) || _animator.GetBool(IsFallingHash)
            || _animator.GetBool(IsDownedHash) || _animator.GetBool(IsOnBoatHash))
        {
            return;
        }
        if (animationEvent.isFiredByAnimator && animationEvent.animatorClipInfo.weight < 0.5f)
        {
            return;
        }

        bool running;
        if (_controller.CanProcessBoatInput)
        {
            if (_source == null || (_source.Current != PlayerAnimationState.Walk
                && _source.Current != PlayerAnimationState.Run) || !_body
                || (_motor && _motor.IsOffGround))
            {
                return;
            }
            Vector3 velocity = _body.linearVelocity;
            if (new Vector2(velocity.x, velocity.z).sqrMagnitude <= _stopSpeedThreshold * _stopSpeedThreshold)
            {
                return;
            }
            running = _source.Current == PlayerAnimationState.Run;
        }
        else
        {
            // Remote views use NetworkAnimator; their local HFSM does not simulate movement.
            running = animationEvent.intParameter == 1;
        }

        if (PlayFootstepSound(running))
        {
            _nextStepTime = Time.time + _minimumStepInterval;
        }
    }

    public void PlayRemoteJumpSound()
    {
        // The owner already plays on the HFSM transition; only remote animation consumes this event.
        if (CanPlaySound && !_controller.CanProcessBoatInput
            && _animator && _animator.GetBool(IsJumpingHash)
            && !_animator.GetBool(IsDownedHash) && !_animator.GetBool(IsOnBoatHash))
        {
            PlaySound(SfxType.PlayerJump, _actionVolume);
        }
    }

    private bool PlayFootstepSound(bool running)
    {
        float volume = running ? _runVolume : _walkVolume;
        AudioClip clip = GetIslandFootstepClip(running);
        if (!clip)
        {
            return PlaySound(running ? SfxType.PlayerRun : SfxType.PlayerWalk, volume);
        }

        EnsureSoundManager();
        return SoundManager.Instance && SoundManager.Instance.PlayEffectAt(clip, transform.position, volume);
    }

    private AudioClip GetIslandFootstepClip(bool running)
    {
        if (_islandFootsteps == null || _islandFootsteps.Length == 0 || !_motor
            || !_motor.TryGetGroundCollider(out Collider ground)
            || !WorldServices.TryGet(out MapStateNet map) || !map.IsLayoutReady || map.Layout == null)
        {
            return null;
        }

        if (_footstepGround != ground || !ReferenceEquals(_footstepLayout, map.Layout))
        {
            _footstepGround = ground;
            _footstepLayout = map.Layout;
            _footstepArchetypeId = -1;

            // Match the contacted island root; distance and generated names cannot prove ground ownership.
            IslandInstanceSet instances = ground.GetComponentInParent<IslandInstanceSet>();
            if (instances)
            {
                for (int i = 0; i < instances.Count; i++)
                {
                    Transform root = instances.GetTransform(i);
                    if (!root || !ground.transform.IsChildOf(root))
                    {
                        continue;
                    }

                    short index = instances.GetIndex(i);
                    for (int j = 0; j < map.Layout.Islands.Count; j++)
                    {
                        IslandPlacement island = map.Layout.Islands[j];
                        if (island.Index == index)
                        {
                            _footstepArchetypeId = island.ArchetypeId;
                            break;
                        }
                    }
                    break;
                }
            }
        }

        foreach (IslandFootstepEntry entry in _islandFootsteps)
        {
            if (entry.Archetype && entry.Archetype.Id == _footstepArchetypeId)
            {
                return entry.GetClip(running);
            }
        }
        return null;
    }

    private bool PlaySound(SfxType type, float volume)
    {
        if (!CanPlaySound)
        {
            return false;
        }
        EnsureSoundManager();
        return SoundManager.Instance && SoundManager.Instance.PlaySfxAt(type, transform.position, volume);
    }

    private void EnsureSoundManager()
    {
        if (!(NetworkServer.active && !NetworkClient.active)
            && !SoundManager.Instance && _soundManagerPrefab)
        {
            Instantiate(_soundManagerPrefab);
        }
    }

    private void PlayMotionSound(PlayerAnimationState state)
    {
        if (state == PlayerAnimationState.OnBoat)
        {
            StopSwimMovementSound();
        }
        if (state != PlayerAnimationState.OnBoat)
        {
            StopBoatMovementSound();
        }
        bool changed = _hasAnimationState && _previousAnimationState != state;
        if (changed)
        {
            if (state == PlayerAnimationState.Jump && _controller && _controller.CanProcessBoatInput)
            {
                PlaySound(SfxType.PlayerJump, _actionVolume);
            }
            else if (state == PlayerAnimationState.OnBoat)
            {
                PlaySound(SfxType.PlayerBoard, _actionVolume);
            }
            else if (_previousAnimationState == PlayerAnimationState.OnBoat
                && state != PlayerAnimationState.Downed && _boardedBoat && !_boardedBoat.IsWrecked)
            {
                PlaySound(SfxType.PlayerDisembark, _actionVolume);
            }
        }

        _boardedBoat = state == PlayerAnimationState.OnBoat && _controller ? _controller.CurrentBoat : null;
        _previousAnimationState = state;
        _hasAnimationState = true;
    }

    private void UpdateBoatMovementSound()
    {
        BoatController boat = _controller ? _controller.CurrentBoat : null;
        if (!CanPlaySound || !_controller.CanProcessBoatInput || !_boatMovementClip
            || _source == null || _source.Current != PlayerAnimationState.OnBoat
            || !boat || !boat.IsOccupied || boat.IsWrecked)
        {
            StopBoatMovementSound();
            return;
        }

        bool moving = boat.HorizontalSpeed >= _boatMovementSpeedThreshold;
        _boatMovementGain = Mathf.MoveTowards(_boatMovementGain, moving ? 1f : 0f,
            Time.deltaTime / _boatMovementFadeTime);
        if (_boatMovementGain <= 0f)
        {
            StopBoatMovementSound();
            return;
        }

        if (!_boatMovementAudio)
        {
            // Keep the rider's stereo loop separate from pooled one-shot effects.
            var child = new GameObject("BoatMovementAudio");
            child.transform.SetParent(transform, false);
            _boatMovementAudio = child.AddComponent<AudioSource>();
            _boatMovementAudio.playOnAwake = false;
            _boatMovementAudio.loop = true;
            _boatMovementAudio.spatialBlend = 0f;
            _boatMovementAudio.dopplerLevel = 0f;
            _boatMovementAudio.clip = _boatMovementClip;
        }

        EnsureSoundManager();
        SoundManager manager = SoundManager.Instance;
        float effectGain = manager ? (manager.IsMuted ? 0f : manager.MasterVolume * manager.EffectVolume) : 1f;
        _boatMovementAudio.volume = _boatMovementVolume * _boatMovementGain * effectGain;
        if (!_boatMovementAudio.isPlaying)
        {
            _boatMovementAudio.Play();
        }
    }

    private void StopBoatMovementSound()
    {
        _boatMovementGain = 0f;
        if (_boatMovementAudio)
        {
            _boatMovementAudio.Stop();
            _boatMovementAudio.volume = 0f;
        }
    }

    private void UpdateWater()
    {
        // Only the movement owner writes parameters; remote views consume NetworkAnimator.
        if (_source == null || !_controller || !_controller.CanProcessBoatInput || !_buoyancy
            || !_animator || !_animator.runtimeAnimatorController)
        {
            return;
        }

        bool canSwim = _source.Current != PlayerAnimationState.OnBoat;
        BuoyancyReading reading = _buoyancy.Reading;
        float threshold = _isInWater ? _waterExitFraction : _waterEnterFraction;
        bool wasInWater = _isInWater;
        _isInWater = canSwim && reading.IsValid && reading.SubmergedFraction >= threshold;
        if (!wasInWater && _isInWater)
        {
            PlaySound(SfxType.PlayerWaterEnter, _actionVolume);
        }

        // The downed clip is the treading-water pose, so reuse it while the survivor floats.
        bool downedPose = _source.Current == PlayerAnimationState.Downed || _isInWater;

        if (_animator.GetBool(IsDownedHash) != downedPose)
        {
            _animator.SetBool(IsDownedHash, downedPose);
        }
    }

    private void UpdateSwimMovementSound()
    {
        if (!CanPlaySound || !_controller.CanProcessBoatInput || !_swimMovementClip
            || !_isInWater || !_body || !_buoyancy || !_buoyancy.Reading.IsValid
            || _source == null || _source.Current == PlayerAnimationState.OnBoat)
        {
            StopSwimMovementSound();
            return;
        }

        Vector3 velocity = _body.linearVelocity;
        bool moving = new Vector2(velocity.x, velocity.z).sqrMagnitude
            >= _swimMovementSpeedThreshold * _swimMovementSpeedThreshold;
        _swimMovementGain = Mathf.MoveTowards(_swimMovementGain, moving ? 1f : 0f,
            Time.deltaTime / _swimMovementFadeTime);
        if (_swimMovementGain <= 0f)
        {
            StopSwimMovementSound();
            return;
        }

        if (!_swimMovementAudio)
        {
            // A sustained swim loop must not restart on every movement update or occupy one-shot voices.
            var child = new GameObject("SwimMovementAudio");
            child.transform.SetParent(transform, false);
            _swimMovementAudio = child.AddComponent<AudioSource>();
            _swimMovementAudio.playOnAwake = false;
            _swimMovementAudio.loop = true;
            _swimMovementAudio.spatialBlend = 0f;
            _swimMovementAudio.dopplerLevel = 0f;
            _swimMovementAudio.clip = _swimMovementClip;
        }

        EnsureSoundManager();
        SoundManager manager = SoundManager.Instance;
        float effectGain = manager ? (manager.IsMuted ? 0f : manager.MasterVolume * manager.EffectVolume) : 1f;
        _swimMovementAudio.volume = _swimMovementVolume * _swimMovementGain * effectGain;
        if (!_swimMovementAudio.isPlaying)
        {
            _swimMovementAudio.Play();
        }
    }

    private void StopSwimMovementSound()
    {
        _swimMovementGain = 0f;
        if (_swimMovementAudio)
        {
            _swimMovementAudio.Stop();
            _swimMovementAudio.volume = 0f;
        }
    }

    private void UpdateFalling()
    {
        if (_source == null || !_controller || !_controller.CanProcessBoatInput || !_motor || !_body)
        {
            return;
        }

        if (_isInWater)
        {
            _fallDuration = 0f;
            _animator.SetBool(IsFallingHash, false);
            return;
        }

        bool onFoot = _source.Current != PlayerAnimationState.OnBoat
            && _source.Current != PlayerAnimationState.Downed;
        bool offGround = onFoot && _motor.IsOffGround;
        bool descending = _body.linearVelocity.y < -_fallSpeedThreshold;
        _fallDuration = offGround && descending ? _fallDuration + Time.deltaTime : 0f;
        bool enteringFall = descending && _fallDuration >= _fallAnimationDelay;

        // A fall remains active until contact, even if a collision changes vertical velocity.
        bool falling = offGround && (_animator.GetBool(IsFallingHash) || enteringFall);
        _animator.SetBool(IsFallingHash, falling);
    }

    private void UpdateLocomotion()
    {
        // Only the movement owner writes parameters; remote views consume NetworkAnimator.
        if (_source == null || !_controller || !_controller.CanProcessBoatInput || !_body)
        {
            return;
        }

        PlayerAnimationState state = _source.Current;
        if (state != PlayerAnimationState.Idle && state != PlayerAnimationState.Walk && state != PlayerAnimationState.Run)
        {
            _animator.SetFloat(LocomotionRateHash, 1f);
            return;
        }

        if (_animator.GetBool(IsFallingHash))
        {
            _animator.SetFloat(LocomotionRateHash, 1f);
            return;
        }

        Vector3 velocity = _body.linearVelocity;
        float speed = new Vector2(velocity.x, velocity.z).magnitude;
        if (speed <= _stopSpeedThreshold)
        {
            _animator.SetFloat(SpeedHash, 0f);
            _animator.SetFloat(LocomotionRateHash, 1f);
            return;
        }

        float runBlend = Mathf.InverseLerp(_walkMoveSpeed, Mathf.Max(_walkMoveSpeed + 0.01f, _runMoveSpeed), speed);
        float movementBlend = speed < _walkMoveSpeed
            ? 0.5f * Mathf.Clamp01(speed / Mathf.Max(0.01f, _walkBlendSpeed))
            : Mathf.Lerp(0.5f, 1f, runBlend);
        float clipSpeed = Mathf.Lerp(_walkClipSpeed, _runClipSpeed, runBlend);
        float rate = Mathf.Clamp(speed / Mathf.Max(0.01f, clipSpeed), _minimumLocomotionRate, _maximumLocomotionRate);
        _animator.SetFloat(SpeedHash, movementBlend);
        _animator.SetFloat(LocomotionRateHash, rate);
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
        PlayMotionSound(state);
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

        bool locomotion = state == PlayerAnimationState.Idle || state == PlayerAnimationState.Walk || state == PlayerAnimationState.Run;
        if (!locomotion || !_controller || !_controller.CanProcessBoatInput || !_body)
        {
            _animator.SetFloat(SpeedHash, speed);
        }
        _animator.SetBool(IsJumpingHash, state == PlayerAnimationState.Jump);
        _animator.SetBool(IsDownedHash, state == PlayerAnimationState.Downed || _isInWater);
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
