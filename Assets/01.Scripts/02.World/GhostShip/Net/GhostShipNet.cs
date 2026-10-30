using System;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.GhostShip.Rules;
using Lighthouse.World.GhostShip.Server;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.World.GhostShip.Net
{
    // One ghost ship per match, decided and moved on the server; position reaches clients through NetworkTransform.
    //   Appears: each night start rolls the chance once, then rises from below the sea after a random delay.
    //   Roams: sails open-sea legs around the roam center, turning at a ship's pace instead of snapping.
    //   Leaves: when its roam time ends or day breaks, it keeps drifting forward while sinking bow-first deep
    //   under the sea, and only then hides.
    //   W22 (2026-10-30): the last night brings it for sure if no night has yet, a night after the mafia took the
    //   hint brings it for sure (ForceNextNight), and once the final battle starts it stops for good (Retire).
    // Vertical wave motion is left to the World buoyancy component: with a non-kinematic Rigidbody assigned, roaming
    // only sets horizontal velocity and yaw. Rising and sinking switch the body to kinematic so buoyancy cannot
    // push the ship back up.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class GhostShipNet : NetworkBehaviour
    {
        private const float RouteRetrySeconds = 1f;

        [SerializeField] private GhostShipConfig _config;
        [SerializeField] private Transform _roamCenter;
        [SerializeField] private float _seaLevel;
        [SerializeField] private LayerMask _landLayers = ~0;
        [SerializeField] private Rigidbody _body;

        [SyncVar(hook = nameof(OnPhaseChanged))]
        private GhostShipPhase _phase;

        private NetworkTransformBase _networkTransform;
        private GhostShipRoute _route;
        private IGameClock _clock;
        private float _phaseTime;
        private float _pendingDelay = -1f;
        private Vector3 _destination;
        private bool _hasDestination;
        private float _routeRetryAt;
        private float _yaw;
        private float _sinkStartY;
        private float _sinkStartSpeed;
        private bool _wasBodyKinematic;
        // Server-only.
        private bool _hasAppeared;
        private bool _isNextNightForced;
        private bool _isRetired;

        public GhostShipPhase Phase => _phase;
        public bool IsVisible => _phase != GhostShipPhase.Hidden;
        // Server-only.
        public bool IsRetired => _isRetired;

        public event Action<GhostShipPhase> PhaseChanged;

        private void Awake()
        {
            _networkTransform = GetComponent<NetworkTransformBase>();
            if (_body != null)
            {
                _wasBodyKinematic = _body.isKinematic;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (_config == null)
            {
                Debug.LogError($"[{nameof(GhostShipNet)}] GhostShipConfig is not assigned.");
                enabled = false;
                return;
            }

            _route = new GhostShipRoute(_config, _landLayers, transform, System.Environment.TickCount);
            AttachClock();
            Park();
        }

        public override void OnStopServer()
        {
            DetachClock();
            base.OnStopServer();
        }

        // Late joiners and the host get the current phase without waiting for the next change.
        public override void OnStartClient()
        {
            base.OnStartClient();
            PhaseChanged?.Invoke(_phase);
        }

        private void Update()
        {
            if (!isServer || _route == null)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            _phaseTime += deltaTime;
            UpdatePendingAppearance(deltaTime);

            switch (_phase)
            {
                case GhostShipPhase.Rising:
                    StepRising(deltaTime);
                    break;
                case GhostShipPhase.Roaming:
                    if (_body == null || _body.isKinematic)
                    {
                        StepRoaming(deltaTime);
                    }

                    if (_phaseTime >= _config.RoamSeconds)
                    {
                        BeginSinking();
                    }

                    break;
                case GhostShipPhase.Sinking:
                    StepSinking(deltaTime);
                    break;
            }
        }

        private void FixedUpdate()
        {
            if (!isServer || _route == null || _phase != GhostShipPhase.Roaming || _body == null || _body.isKinematic)
            {
                return;
            }

            StepRoaming(Time.fixedDeltaTime);
        }

        private void OnDestroy()
        {
            DetachClock();
        }

        // Test shortcut: rise now regardless of night and chance. A ship already out is left alone (one per match).
        [Server]
        [ContextMenu("Summon Now (Server)")]
        public void Summon()
        {
            if (_phase != GhostShipPhase.Hidden || _isRetired)
            {
                return;
            }

            _pendingDelay = -1f;
            Appear();
        }

        [Server]
        [ContextMenu("Sink Now (Server)")]
        public void Dismiss()
        {
            if (_phase == GhostShipPhase.Rising || _phase == GhostShipPhase.Roaming)
            {
                BeginSinking();
            }
        }

        // The next night start skips the chance roll. Used when the mafia took a sighting's hint, so the citizens
        // get another chance (2026-10-30).
        [Server]
        public void ForceNextNight()
        {
            _isNextNightForced = true;
        }

        // No more sightings this match: the hint is for finding a hidden mafia, which the final battle is past.
        [Server]
        public void Retire()
        {
            _isRetired = true;
            _isNextNightForced = false;
            _pendingDelay = -1f;
            Dismiss();
        }

        [Server]
        private void AttachClock()
        {
            if (!WorldServices.TryGet(out _clock))
            {
                Debug.LogWarning($"[{nameof(GhostShipNet)}] No GameClock in this scene: the ghost ship only appears when summoned.");
                return;
            }

            _clock.NightStarted += HandleNightStarted;
            _clock.DayStarted += HandleDayStarted;
        }

        private void DetachClock()
        {
            if (_clock == null)
            {
                return;
            }

            _clock.NightStarted -= HandleNightStarted;
            _clock.DayStarted -= HandleDayStarted;
            _clock = null;
        }

        [Server]
        private void HandleNightStarted(int dayIndex)
        {
            if (_phase != GhostShipPhase.Hidden || _isRetired)
            {
                return;
            }

            bool isLastNightUnseen = _config.IsLastNightGuaranteed && !_hasAppeared && _clock != null
                && dayIndex >= _clock.TotalDays;
            bool isForced = _isNextNightForced || isLastNightUnseen;
            _isNextNightForced = false;

            if (!isForced && UnityEngine.Random.value >= _config.NightChance)
            {
                return;
            }

            _pendingDelay = UnityEngine.Random.Range(_config.NightDelayRange.x, _config.NightDelayRange.y);
        }

        [Server]
        private void HandleDayStarted(int dayIndex)
        {
            _pendingDelay = -1f;
            Dismiss();
        }

        [Server]
        private void UpdatePendingAppearance(float deltaTime)
        {
            if (_pendingDelay < 0f)
            {
                return;
            }

            _pendingDelay -= deltaTime;
            if (_pendingDelay > 0f)
            {
                return;
            }

            _pendingDelay = -1f;
            bool isNight = _clock == null || _clock.Phase == DayPhase.Night;
            if (isNight && _phase == GhostShipPhase.Hidden && !_isRetired)
            {
                Appear();
            }
        }

        [Server]
        private void Appear()
        {
            Vector3 center = RoamCenter();
            if (!_route.TryPickSpawn(center, _seaLevel, out Vector3 spawn))
            {
                Debug.LogWarning($"[{nameof(GhostShipNet)}] No open sea found around the roam center; check radius and land layers.");
                return;
            }

            // Face along the ring so the first leg starts as a turn, not a head-on charge at the island.
            Vector3 toCenter = center - spawn;
            toCenter.y = 0f;
            _yaw = Quaternion.LookRotation(Vector3.Cross(Vector3.up, toCenter.normalized)).eulerAngles.y;
            _hasDestination = _route.TryPickNext(center, _seaLevel, spawn, out _destination);

            _hasAppeared = true;
            SetBodyKinematic(true);
            // Starts behind and below the checked spot so the forward drift while rising ends on it.
            Vector3 riseRun = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward * (_config.Speed * _config.RiseSeconds);
            Vector3 start = spawn - riseRun + Vector3.down * _config.RiseDepth;
            Teleport(start, Quaternion.Euler(-_config.RisePitch, _yaw, 0f));
            SetPhase(GhostShipPhase.Rising);
        }

        // The sink played backwards: sailing speed and a steady climb keep it on one straight diagonal, bow-up
        // until it levels out near the surface, so it reads as surfacing rather than a lift going up.
        [Server]
        private void StepRising(float deltaTime)
        {
            float t = Mathf.Clamp01(_phaseTime / _config.RiseSeconds);
            Vector3 position = MoveForward(transform.position, _config.Speed, deltaTime);
            position.y = _seaLevel - _config.RiseDepth * (1f - t);
            float pitch = -_config.RisePitch * Mathf.SmoothStep(0f, 1f, (1f - t) * 2f);
            transform.SetPositionAndRotation(position, Quaternion.Euler(pitch, _yaw, 0f));

            if (t >= 1f)
            {
                SetBodyKinematic(_wasBodyKinematic);
                SetPhase(GhostShipPhase.Roaming);
            }
        }

        [Server]
        private void StepRoaming(float deltaTime)
        {
            float speed = _config.Speed * Steer(deltaTime);
            if (_body != null && !_body.isKinematic)
            {
                Vector3 forward = Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward;
                Vector3 velocity = _body.linearVelocity;
                _body.linearVelocity = new Vector3(forward.x * speed, velocity.y, forward.z * speed);
                Vector3 euler = _body.rotation.eulerAngles;
                _body.MoveRotation(Quaternion.Euler(euler.x, _yaw, euler.z));
                return;
            }

            Vector3 position = MoveForward(transform.position, speed, deltaTime);
            position.y = _seaLevel;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, _yaw, 0f));
        }

        // Turns toward the current waypoint at the configured rate and returns the speed factor:
        // the ship slows while the waypoint is well off its bow so turns stay wide and readable.
        [Server]
        private float Steer(float deltaTime)
        {
            Vector3 position = transform.position;
            if (!_hasDestination || FlatDistance(position, _destination) <= _config.ArriveDistance)
            {
                if (Time.time < _routeRetryAt)
                {
                    return 0.5f;
                }

                _hasDestination = _route.TryPickNext(RoamCenter(), _seaLevel, position, out _destination);
                if (!_hasDestination)
                {
                    _routeRetryAt = Time.time + RouteRetrySeconds;
                    return 0.5f;
                }
            }

            Vector3 toTarget = _destination - position;
            float targetYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
            _yaw = Mathf.MoveTowardsAngle(_yaw, targetYaw, _config.TurnSpeed * deltaTime);
            float offBow = Mathf.Abs(Mathf.DeltaAngle(_yaw, targetYaw));
            return Mathf.Lerp(1f, 0.4f, offBow / 180f);
        }

        [Server]
        private void BeginSinking()
        {
            SetBodyKinematic(true);
            _pendingDelay = -1f;
            _sinkStartY = transform.position.y;
            _sinkStartSpeed = _config.Speed;
            SetPhase(GhostShipPhase.Sinking);
        }

        // Keeps sailing forward while it goes down at a steady rate and pitches bow-down, so the whole sink is one
        // straight diagonal: a speed dying out under an accelerating drop read as a short slide and then an elevator.
        [Server]
        private void StepSinking(float deltaTime)
        {
            float t = Mathf.Clamp01(_phaseTime / _config.SinkSeconds);
            Vector3 position = MoveForward(transform.position, _sinkStartSpeed, deltaTime);
            position.y = _sinkStartY - _config.SinkDepth * t;
            float pitch = _config.SinkPitch * Mathf.SmoothStep(0f, 1f, t * 2f);
            transform.SetPositionAndRotation(position, Quaternion.Euler(pitch, _yaw, 0f));

            if (t >= 1f)
            {
                SetPhase(GhostShipPhase.Hidden);
                Park();
            }
        }

        // Hidden ships wait deep under the sea so nothing pops up if a view forgets to hide them.
        [Server]
        private void Park()
        {
            SetBodyKinematic(true);
            Vector3 position = RoamCenter();
            position.y = _seaLevel - _config.SinkDepth;
            Teleport(position, Quaternion.Euler(0f, _yaw, 0f));
        }

        [Server]
        private void SetPhase(GhostShipPhase phase)
        {
            _phaseTime = 0f;
            GhostShipPhase old = _phase;
            _phase = phase;

            // The hook does not run on a dedicated server; the host gets it, so only notify here when no client is local.
            if (!isClient)
            {
                OnPhaseChanged(old, phase);
            }
        }

        private void Teleport(Vector3 position, Quaternion rotation)
        {
            if (_networkTransform != null)
            {
                _networkTransform.ServerTeleport(position, rotation);
                return;
            }

            transform.SetPositionAndRotation(position, rotation);
        }

        private void SetBodyKinematic(bool isKinematic)
        {
            if (_body == null)
            {
                return;
            }

            if (isKinematic && !_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }

            _body.isKinematic = isKinematic;
        }

        private Vector3 MoveForward(Vector3 position, float speed, float deltaTime)
        {
            return position + Quaternion.Euler(0f, _yaw, 0f) * Vector3.forward * (speed * deltaTime);
        }

        private Vector3 RoamCenter()
        {
            Vector3 center = _roamCenter != null ? _roamCenter.position : Vector3.zero;
            center.y = _seaLevel;
            return center;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void OnPhaseChanged(GhostShipPhase oldValue, GhostShipPhase newValue)
        {
            PhaseChanged?.Invoke(newValue);
        }
    }
}
