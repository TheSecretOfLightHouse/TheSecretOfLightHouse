using Lighthouse.Monsters.AI.Net;
using Lighthouse.Monsters.AI.Server;
using Lighthouse.Monsters.MosaWhale.Rules;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.MosaWhale.Server
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MosaWhaleCycle), typeof(Rigidbody))]
    public sealed class MosaWhaleMovement : NetworkBehaviour
    {
        [SerializeField] private MosaWhalePoseProfile _poseProfile;

        [SyncVar] private Vector3 _attackCenter;
        [SyncVar] private float _attackRadius;
        [SyncVar] private bool _hasAttackCenter;
        [SyncVar] private bool _isTelegraphVisible;

        private MosaWhaleCycle _cycle;
        private MosaWhaleTargeting _targeting;
        private MonsterMotion _motion;
        private SeaMonster _monster;
        private Rigidbody _body;
        private MonsterWaterBody _water;
        private NetworkTransformBase _networkTransform;
        private MosaWhaleCollision _collision;
        private RigidbodyConstraints _savedConstraints;
        private Quaternion _heading = Quaternion.identity;
        private Vector3 _diveStart;
        private float _surfaceHeight;
        private float _patrolStartDepth;
        private bool _controlsBody;
        private bool _hasCrossedSurface;
        private double _surfaceCrossedAt;
        private Vector3 _headPosition;
        private bool _isObstacleReturn;
        private Vector3 _obstacleAnchor;
        private Vector3 _returnStartPosition;
        private Quaternion _returnStartRotation;
        private float _recoveryStartDepth;
        private bool _isDescending;

        public Vector3 AttackCenter => _attackCenter;
        public float AttackRadius => _attackRadius;
        public bool HasAttackCenter => _hasAttackCenter;
        public bool IsTelegraphVisible => _isTelegraphVisible;
        public bool HasValidSetup => _poseProfile != null && _poseProfile.HasValidCurves;
        public bool HasValidDamagePose => HasValidSetup && _poseProfile.HasValidHeadCurve;
        public bool HasCrossedSurface => _hasCrossedSurface;
        public double SurfaceCrossedAt => _surfaceCrossedAt;
        public Vector3 HeadPosition => _headPosition;
        public bool IsDescending => _isDescending;

        public bool CanLeaveDepthPhase
        {
            get
            {
                if (_water == null)
                {
                    return true;
                }
                bool reached = _water.HasReachedDepth(_water.Buoyancy.TargetDepth);
                return reached && (_cycle.Phase != MosaWhalePhase.Diving || _water.IsFullySubmerged(0.2f));
            }
        }

        public float DepthTransitionTimeout => _water != null ? _water.TransitionTimeout : 15f;

        private void Awake()
        {
            _cycle = GetComponent<MosaWhaleCycle>();
            _targeting = GetComponent<MosaWhaleTargeting>();
            _motion = GetComponent<MonsterMotion>();
            _monster = GetComponent<SeaMonster>();
            _body = GetComponent<Rigidbody>();
            _water = GetComponent<MonsterWaterBody>();
            _networkTransform = GetComponent<NetworkTransformBase>();
            _collision = GetComponent<MosaWhaleCollision>();
        }

        private void FixedUpdate()
        {
            if (!isServer || !_controlsBody || !_cycle.IsRunning || _monster.IsDead
                || _body.isKinematic || !HasValidSetup)
            {
                return;
            }
            MosaWhaleSettings settings = _cycle.Settings;
            float progress = Mathf.Clamp01((float)(NetworkTime.time - _cycle.PhaseStartedAt)
                / Mathf.Max(0.01f, _cycle.PhaseDuration));
            Vector3 position = _body.position;
            Quaternion rotation = _body.rotation;
            if (_cycle.Phase == MosaWhalePhase.Patrol)
            {
                float settle = Mathf.Clamp01((float)(NetworkTime.time - _cycle.PhaseStartedAt)
                    / settings.PatrolSettleDuration);
                position.y = SampleSurface(position) - Mathf.Lerp(_patrolStartDepth, settings.PatrolDepth,
                    Mathf.SmoothStep(0f, 1f, settle));
            }
            else if (_cycle.Phase == MosaWhalePhase.Diving)
            {
                position = _diveStart;
                position.y = Mathf.Lerp(_diveStart.y, _surfaceHeight - settings.DiveDepth,
                    Mathf.SmoothStep(0f, 1f, progress));
                rotation = _heading * _poseProfile.EvaluateRotation(_cycle.Phase, progress);
            }
            else if (_cycle.Phase == MosaWhalePhase.Telegraph)
            {
                position = _attackCenter + Vector3.down * settings.DiveDepth;
                rotation = _heading * _poseProfile.EvaluateRotation(_cycle.Phase, 1f);
            }
            else if (_cycle.Phase == MosaWhalePhase.Recovering)
            {
                position = _isObstacleReturn ? _obstacleAnchor : _attackCenter;
                float shallowStart = Mathf.Max(0f, _cycle.PhaseDuration - settings.RecoveryExposureDuration);
                float elapsed = (float)(NetworkTime.time - _cycle.PhaseStartedAt);
                float depth = elapsed < shallowStart && shallowStart > 0f
                    ? Mathf.Lerp(_recoveryStartDepth, settings.PatrolDepth,
                        Mathf.SmoothStep(0f, 1f, elapsed / shallowStart))
                    : Mathf.Lerp(shallowStart > 0f ? settings.PatrolDepth : _recoveryStartDepth, settings.RecoveryDepth,
                        Mathf.SmoothStep(0f, 1f, (elapsed - shallowStart)
                            / Mathf.Max(0.01f, _cycle.PhaseDuration - shallowStart)));
                position.y -= depth;
                rotation = _heading * _poseProfile.EvaluateRotation(_cycle.Phase, progress);
            }
            else if (_isObstacleReturn && _cycle.Phase == MosaWhalePhase.Reentering)
            {
                position = _obstacleAnchor + Vector3.down * settings.DiveDepth;
                position = Vector3.Lerp(_returnStartPosition, position, Mathf.SmoothStep(0f, 1f, progress));
                float breach = Mathf.Clamp01((float)(NetworkTime.time - _cycle.BreachStartedAt)
                    / Mathf.Max(0.01f, _cycle.BreachDuration));
                rotation = Quaternion.Slerp(_returnStartRotation,
                    _heading * _poseProfile.EvaluateRotation(_cycle.Phase, breach), progress);
            }
            else if (_hasAttackCenter)
            {
                float breach = Mathf.Clamp01((float)(NetworkTime.time - _cycle.BreachStartedAt)
                    / Mathf.Max(0.01f, _cycle.BreachDuration));
                _isDescending = _poseProfile.EvaluateLift(breach)
                    < _poseProfile.EvaluateLift(Mathf.Max(0f, breach - 0.001f));
                position = _attackCenter;
                position.y += Mathf.Lerp(-settings.DiveDepth, settings.BreachHeight,
                    _poseProfile.EvaluateLift(breach));
                rotation = _heading * _poseProfile.EvaluateRotation(_cycle.Phase, breach);
            }
            if (_cycle.IsAttackCancelled)
            {
                _isTelegraphVisible = false;
            }
            if (_isObstacleReturn)
            {
                // 막힌 동안 경과 시간이 흘러도 복귀 위치·자세가 한 프레임에 뛰지 않도록 제한한다.
                position = Vector3.MoveTowards(_body.position, position,
                    settings.ObstacleReturnSpeed * Time.fixedDeltaTime);
                rotation = Quaternion.RotateTowards(_body.rotation, rotation,
                    settings.ObstacleTurnSpeed * Time.fixedDeltaTime);
            }
            if (_collision != null && _collision.WouldHitObstacle(_body.position, _body.rotation, position, rotation))
            {
                _cycle.StopAtObstacle();
                return;
            }
            bool controlsDepth = _water != null && (_cycle.Phase == MosaWhalePhase.Patrol
                || _cycle.Phase == MosaWhalePhase.Diving || _cycle.Phase == MosaWhalePhase.Telegraph
                || _cycle.Phase == MosaWhalePhase.Recovering);
            if (controlsDepth)
            {
                _water.Buoyancy.SetTargetDepth(_water.DepthForRoot(SampleSurface(position) - position.y));
                position.y = _body.position.y;
            }
            // Preserve the combat trajectory above water and let buoyancy own underwater height.
            _body.position = position;
            if (_cycle.Phase != MosaWhalePhase.Patrol)
            {
                _body.rotation = rotation;
                _body.linearVelocity = controlsDepth ? new Vector3(0f, _body.linearVelocity.y, 0f) : Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }
            else
            {
                Vector3 velocity = _body.linearVelocity;
                if (!controlsDepth)
                {
                    velocity.y = 0f;
                }
                _body.linearVelocity = velocity;
            }
            if (_cycle.Phase == MosaWhalePhase.Rising && HasValidDamagePose)
            {
                float breach = Mathf.Clamp01((float)(NetworkTime.time - _cycle.BreachStartedAt)
                    / Mathf.Max(0.01f, _cycle.BreachDuration));
                Vector3 localHead = _poseProfile.EvaluateHeadLocalPosition(breach);
                _headPosition = position + rotation * Vector3.Scale(localHead, transform.lossyScale);
                if (!_hasCrossedSurface && MonsterMotion.IsFinite(_headPosition)
                    && _headPosition.y >= _attackCenter.y)
                {
                    _hasCrossedSurface = true;
                    _surfaceCrossedAt = NetworkTime.time;
                    _isTelegraphVisible = false;
                }
            }
        }

        private void OnDisable()
        {
            if (isServer)
            {
                Release();
            }
        }

        public override void OnStopServer()
        {
            Release();
        }

        [Server]
        public void HandlePhase(MosaWhalePhase phase)
        {
            if (!HasValidSetup || _cycle.Settings == null)
            {
                return;
            }
            if (!_controlsBody)
            {
                _savedConstraints = _body.constraints;
                _controlsBody = true;
            }
            if (_water != null)
            {
                bool scripted = phase == MosaWhalePhase.Rising || phase == MosaWhalePhase.Apex
                    || phase == MosaWhalePhase.Reentering;
                _water.SetScriptedMotion(scripted);
                if (!scripted)
                {
                    float rootDepth = phase == MosaWhalePhase.Patrol ? _cycle.Settings.PatrolDepth
                        : phase == MosaWhalePhase.Recovering ? _cycle.Settings.RecoveryDepth : _cycle.Settings.DiveDepth;
                    _water.Buoyancy.SetTargetDepth(_water.DepthForRoot(rootDepth));
                }
            }
            _body.constraints = _savedConstraints & ~RigidbodyConstraints.FreezePositionY;
            _hasCrossedSurface = false;
            _surfaceCrossedAt = 0d;
            _isDescending = false;
            if (phase == MosaWhalePhase.Patrol)
            {
                _isObstacleReturn = false;
                _isTelegraphVisible = false;
                _hasAttackCenter = false;
                _patrolStartDepth = Mathf.Max(0f, SampleSurface(_body.position) - _body.position.y);
                _motion.SetMovementLocked(false);
                _body.rotation = Quaternion.Euler(0f, _body.rotation.eulerAngles.y, 0f);
            }
            else if (phase == MosaWhalePhase.Diving)
            {
                _isObstacleReturn = false;
                _diveStart = _body.position;
                _heading = Quaternion.Euler(0f, _body.rotation.eulerAngles.y, 0f);
                _surfaceHeight = SampleSurface(_diveStart);
                _motion.SetMovementLocked(true);
            }
            else if (phase == MosaWhalePhase.Telegraph)
            {
                Vector3 center = _targeting.LockedAttackPosition;
                center.y = SampleSurface(center);
                _attackCenter = center;
                _attackRadius = _cycle.Settings.AttackRadius;
                _hasAttackCenter = true;
                _isTelegraphVisible = true;
                // 깊이 잠긴 뒤의 수평 이동에서만 보간을 초기화해 바다를 가로지르는 잔상을 막는다.
                Vector3 submerged = center + Vector3.down * _cycle.Settings.DiveDepth;
                _body.position = submerged;
                if (_networkTransform != null)
                {
                    ApplySubmergedTeleport(submerged, _body.rotation);
                    RpcSubmergedTeleport(submerged, _body.rotation);
                }
            }
            else if (phase == MosaWhalePhase.Recovering)
            {
                Vector3 anchor = _isObstacleReturn ? _obstacleAnchor : _attackCenter;
                _recoveryStartDepth = Mathf.Max(0f, anchor.y - _body.position.y);
            }
            if (phase != MosaWhalePhase.Patrol)
            {
                _body.constraints &= ~(RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ);
            }
        }

        [ClientRpc]
        private void RpcSubmergedTeleport(Vector3 position, Quaternion rotation)
        {
            if (!isServer)
            {
                ApplySubmergedTeleport(position, rotation);
            }
        }

        private void ApplySubmergedTeleport(Vector3 position, Quaternion rotation)
        {
            _body.position = position;
            _body.rotation = rotation;
            if (_water != null)
            {
                _water.Buoyancy.InvalidateSamples();
            }
            if (_networkTransform != null)
            {
                // Reliable의 직전 송수신 기준은 유지해야 다음 위치 델타에 오프셋이 더해지지 않는다.
                _networkTransform.target.SetPositionAndRotation(position, rotation);
                _networkTransform.serverSnapshots.Clear();
                _networkTransform.clientSnapshots.Clear();
                Physics.SyncTransforms();
            }
        }

        [Server]
        public void HideTelegraph()
        {
            _isTelegraphVisible = false;
        }

        [Server]
        public void BeginObstacleReturn()
        {
            _isObstacleReturn = true;
            _obstacleAnchor = _body.position;
            _obstacleAnchor.y = SampleSurface(_obstacleAnchor);
            _returnStartPosition = _body.position;
            _returnStartRotation = _body.rotation;
            _isTelegraphVisible = false;
        }

        [Server]
        public void Release()
        {
            _isTelegraphVisible = false;
            _hasAttackCenter = false;
            _hasCrossedSurface = false;
            _surfaceCrossedAt = 0d;
            _isObstacleReturn = false;
            _isDescending = false;
            if (_water != null)
            {
                _water.SetScriptedMotion(false);
                _water.RestoreDefaultDepth();
            }
            if (!_controlsBody)
            {
                return;
            }
            _motion.SetMovementLocked(false);
            _motion.Stop();
            if (!_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }
            _body.constraints = _savedConstraints;
            _controlsBody = false;
        }

        private float SampleSurface(Vector3 position)
        {
            if (WorldServices.TryGet<IWorldQuery>(out IWorldQuery world))
            {
                float height = world.SampleWaterHeight(position);
                if (!float.IsNaN(height) && !float.IsInfinity(height))
                {
                    return height;
                }
            }
            if (WorldServices.TryGet<WaterSamplerHost>(out WaterSamplerHost water)
                && WorldServices.TryGet<IClock>(out IClock clock))
            {
                float height = WaveModel.HeightAt(water.Inputs.Waves, water.Inputs.Modifiers,
                    position.x, position.z, (float)clock.Now);
                if (!float.IsNaN(height) && !float.IsInfinity(height))
                {
                    return height;
                }
            }
            return _cycle.Settings.PrototypeSurfaceHeight;
        }
    }
}
