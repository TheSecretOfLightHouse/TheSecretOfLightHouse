using System.Collections.Generic;
using Lighthouse.Monsters.AI.Net;
using Lighthouse.Monsters.AI.Rules;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Lighthouse.Monsters.AI.Server", "Lighthouse.Runtime", "MantaRiseAttack")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster), typeof(MonsterMotion), typeof(SeaMonsterSensor))]
    public sealed class MantaElectricAttack : NetworkBehaviour
    {
        [Header("이동 예측 선점")]
        [SerializeField, Min(0.01f)] private float _interceptSpeed = 16f;
        [SerializeField, Min(0.01f)] private float _alignmentRadius = 0.6f;
        [SerializeField, Min(0f)] private float _predictionSeconds = 1.5f;
        [SerializeField, Min(0.01f)] private float _minimumLead = 4f;
        [SerializeField] private float _fallbackWaterHeight;
        [SerializeField, Min(0.01f)] private float _timeoutSeconds = 20f;
        [Header("전기 예고·피해")]
        [Tooltip("꼬리로 전깃줄 설치를 마친 뒤 피해가 시작되기까지의 시간(초).")]
        [SerializeField, Min(0f)] private float _preparationSeconds = 0.7f;
        [Tooltip("예고를 제외한 실제 전기 영역의 유지 시간(초).")]
        [SerializeField, Min(0.01f)] private float _electricDuration = 3f;
        [Tooltip("전기 영역 하나가 같은 배에 한 번 주는 피해. MonsterProfile의 공격력과 별도로 사용한다.")]
        [SerializeField, Min(0.01f)] private float _electricDamage = 20f;
        [SerializeField, Min(0.01f)] private float _lineLength = 8f;
        [SerializeField, Min(0.01f)] private float _lineWidth = 0.6f;
        [SerializeField, Min(0f)] private float _zigzagWidth = 0.7f;
        [SerializeField, Range(3, 32)] private int _linePointCount = 13;
        [SerializeField, Min(0f)] private float _surfaceOffset = 0.15f;
        [Header("꼬리 전깃줄 설치")]
        [Tooltip("가로 방향에서 배 전방으로 기울이는 각도. 0도는 가로, 90도는 전방이다.")]
        [SerializeField, Range(0f, 90f)] private float _layingForwardAngle = 30f;
        [SerializeField, Min(0.01f)] private float _layingSpeed = 10f;
        [Tooltip("설치 후 추적으로 전환할 때의 초당 회전 각도.")]
        [SerializeField, Min(0.01f)] private float _chaseTurnSpeed = 120f;
        [SerializeField, Min(0.01f)] private float _postAttackAcceleration = 10f;
        [SerializeField] private Vector3 _tailLocalOffset = new Vector3(0f, 0f, -2.7f);

        [SyncVar] private MantaAttackPhase _phase;       
        [SyncVar] private double _dischargeAt;
        [SyncVar] private double _expiresAt;
        [SyncVar] private float _activeLineWidth;
        private readonly SyncList<Vector3> _electricPoints = new SyncList<Vector3>();
        private readonly HashSet<MonsterBoatTarget> _hitTargets = new HashSet<MonsterBoatTarget>();
        private readonly Dictionary<MonsterBoatTarget, Vector3> _previousBoats = new Dictionary<MonsterBoatTarget, Vector3>(8);
        private readonly List<MonsterBoatTarget> _boats = new List<MonsterBoatTarget>(8);
        private SeaMonster _monster;
        private MonsterMotion _motion;
        private SeaMonsterSensor _sensor;
        private Rigidbody _body;
        private IWorldQuery _world;
        private MonsterBoatTarget _target;
        private Rigidbody _targetBody;
        private Vector3 _previousTargetPosition;
        private Vector3 _targetVelocity;
        private float _elapsed;
        private bool _ownsMovement;
        private Vector3 _sweepEnd;
        private Vector3 _sweepDirection;
        private Vector3 _lastTail;
        private float _trailDistance;
        private float _nextTrailDistance;

        public MantaAttackPhase Phase => _phase;        
        public double DischargeAt => _dischargeAt;
        public double ExpiresAt => _expiresAt;
        public float ActiveLineWidth => _activeLineWidth;
        public IReadOnlyList<Vector3> ElectricPoints => _electricPoints;
        public Vector3 TailPosition => transform.TransformPoint(_tailLocalOffset);
        public bool IsAttacking => _ownsMovement;
        public bool HasCompleted { get; private set; }       
        public bool CanAct => isServer && isActiveAndEnabled && _monster != null
            && _monster.IsInitialized && !_monster.IsDead && _monster.isActiveAndEnabled
            && _body != null && !_body.isKinematic && _motion != null && _motion.CanMove
            && _sensor != null && _sensor.isActiveAndEnabled && _monster.Profile != null
            && HasValidSettings();

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            _motion = GetComponent<MonsterMotion>();
            _sensor = GetComponent<SeaMonsterSensor>();
            _body = GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (!isServer)
            {
                return;
            }

            if (!CanAct)
            {
                StopAttack(false);

                return;
            }

            if (!_ownsMovement)
            {
                return;
            }

            _elapsed += Time.fixedDeltaTime;

            if (_phase == MantaAttackPhase.Laying && _elapsed >= _timeoutSeconds)
            {
                StopAttack(false);

                return;
            }

            if (_phase == MantaAttackPhase.Approaching)
            {
                if (_elapsed >= _timeoutSeconds || !HasValidTarget())
                {
                    StopAttack(false);

                    return;
                }

                Vector3 position = _target.transform.position;
                Vector3 sampledVelocity = (position - _previousTargetPosition) / Time.fixedDeltaTime;

                if (sampledVelocity.sqrMagnitude < 0.01f && _targetBody != null)
                {
                    sampledVelocity = _targetBody.linearVelocity;
                }

                // 네트워크 갱신 사이의 공백으로 선점 위치가 튀지 않도록 소유 클라이언트가 보고한 배의 이동 속도를 평활화한다.
                _targetVelocity = Vector3.Lerp(_targetVelocity, sampledVelocity, 1f - Mathf.Exp(-8f * Time.fixedDeltaTime));
                _previousTargetPosition = position;
                Vector3 center = MantaElectricRules.PredictCenter(position, _targetVelocity,
                    _target.transform.forward, _predictionSeconds, _minimumLead);
                Vector3 heading = _targetVelocity;
                heading.y = 0f;

                if (heading.sqrMagnitude < 0.01f)
                {
                    heading = _target.transform.forward;
                    heading.y = 0f;
                }

                if (heading.sqrMagnitude < 0.01f)
                {
                    heading = Vector3.forward;
                }

                Vector3 forward = heading.normalized;
                float angle = _layingForwardAngle * Mathf.Deg2Rad;
                Vector3 layingDirection = Vector3.Cross(Vector3.up, forward) * Mathf.Cos(angle)
                    + forward * Mathf.Sin(angle);
                Vector3 start = center - layingDirection * (_lineLength * 0.5f);
                Vector3 tailOffset = Quaternion.LookRotation(layingDirection, Vector3.up)
                    * Vector3.Scale(_tailLocalOffset, transform.lossyScale);

                tailOffset.y = 0f;
                start -= tailOffset;
                start.y = _body.position.y;

                if (MoveTowards(start, _interceptSpeed))
                {
                    _sweepDirection = layingDirection;
                    _sweepEnd = start + layingDirection * _lineLength;
                    _body.rotation = Quaternion.LookRotation(_sweepDirection, Vector3.up);
                    _activeLineWidth = _lineWidth;
                    _dischargeAt = double.MaxValue;
                    _expiresAt = double.MaxValue;
                    _trailDistance = 0f;
                    _nextTrailDistance = _lineLength / (_linePointCount - 1);
                    _lastTail = GetTailPosition();
                    _electricPoints.Add(_lastTail);
                    SetPhase(MantaAttackPhase.Laying);
                }
            }
            else if (_phase == MantaAttackPhase.Laying)
            {
                RecordTailTrail();

                if (MoveTowards(_sweepEnd, _layingSpeed, Mathf.Min(_alignmentRadius, 0.05f), true))
                {
                    Vector3 tail = GetTailPosition();

                    if ((_electricPoints[_electricPoints.Count - 1] - tail).sqrMagnitude > 0.0001f)
                    {
                        _electricPoints.Add(tail);
                    }

                    _dischargeAt = NetworkTime.time + _preparationSeconds;
                    _expiresAt = _dischargeAt + _electricDuration;
                    SetPhase(MantaAttackPhase.Preparing);
                    ContinueAfterLaying(false);
                }
            }
            else if (_phase == MantaAttackPhase.Preparing)
            {
                ContinueAfterLaying(false);

                if (NetworkTime.time >= _expiresAt)
                {
                    StopAttack(true);

                    return;
                }

                if (NetworkTime.time >= _dischargeAt)
                {
                    SetPhase(MantaAttackPhase.Discharging);
                    SampleBoats(false);
                    ApplyElectricDamage();
                }
            }
            else if (_phase == MantaAttackPhase.Discharging)
            {
                ContinueAfterLaying(true);

                if (NetworkTime.time >= _expiresAt)
                {
                    StopAttack(true);

                    return;
                }

                SampleBoats(true);
                ApplyElectricDamage();
            }
        }

        private void OnDisable()
        {
            StopAttack(false);
        }

        public override void OnStopServer()
        {
            StopAttack(false);
            base.OnStopServer();
        }

        [Server]
        public bool BeginAttack(GameObject target)
        {
            if (!CanAct || _ownsMovement || _motion.IsMovementLocked || target == null)
            {
                return false;
            }

            _target = target.GetComponent<MonsterBoatTarget>();
            _targetBody = target.GetComponent<Rigidbody>();

            if (!HasValidTarget() || !TryWaterHeight(target.transform.position, out _))
            {
                _target = null;

                return false;
            }

            _previousTargetPosition = target.transform.position;
            _targetVelocity = _targetBody != null ? _targetBody.linearVelocity : Vector3.zero;
            _elapsed = 0f;
            _hitTargets.Clear();
            _previousBoats.Clear();
            _electricPoints.Clear();            
            HasCompleted = false;
            _ownsMovement = true;
            _motion.SetMovementLocked(true);
            _body.linearVelocity = Vector3.zero;
            SetPhase(MantaAttackPhase.Approaching);
            _monster.TrySetBehaviorState(MonsterState.Attack);

            return _ownsMovement;
        }

        [Server]
        public void Cancel()
        {
            StopAttack(false);
        }

        private Vector3 GetTailPosition()
        {
            Vector3 tail = _body.position + _body.rotation * Vector3.Scale(_tailLocalOffset, transform.lossyScale);

            if (TryWaterHeight(tail, out float water))
            {
                tail.y = water + _surfaceOffset;
            }

            return tail;
        }

        private void RecordTailTrail()
        {
            Vector3 tail = GetTailPosition();
            float distance = Vector3.Distance(_lastTail, tail);
            float spacing = _lineLength / (_linePointCount - 1);

            // 물리 갱신 주기가 달라도 설치되는 선의 밀도가 일정하도록 이동 거리 기준으로 좌표를 기록한다.
            while (distance > 0f && _nextTrailDistance <= _trailDistance + distance && _electricPoints.Count < _linePointCount)
            {
                float fraction = (_nextTrailDistance - _trailDistance) / distance;
                Vector3 point = Vector3.Lerp(_lastTail, tail, fraction);
                Vector3 side = Vector3.Cross(Vector3.up, _sweepDirection);
                point += side * (Mathf.Sin(_electricPoints.Count * 2.399963f) * _zigzagWidth);
                _electricPoints.Add(point);
                _nextTrailDistance += spacing;
            }

            _trailDistance += distance;
            _lastTail = tail;
        }

        private void SampleBoats(bool retainPrevious)
        {
            _boats.Clear();
            IReadOnlyList<MonsterBoatTarget> targets = MonsterBoatTarget.Targets;

            for (int index = 0; index < targets.Count; index++)
            {
                MonsterBoatTarget boat = targets[index];

                if (boat != null && boat.IsEligible)
                {
                    _boats.Add(boat);

                    if (!retainPrevious || !_previousBoats.ContainsKey(boat))
                    {
                        _previousBoats[boat] = boat.transform.position;
                    }
                }
            }
        }

        private void ApplyElectricDamage()
        {
            // 피해 콜백에서 배가 난파되어 등록 목록에서 빠질 수 있으므로 미리 복사한 목록을 순회한다.
            for (int index = 0; index < _boats.Count && _ownsMovement; index++)
            {
                MonsterBoatTarget boat = _boats[index];

                if (boat == null || !boat.IsEligible || _hitTargets.Contains(boat))
                {
                    continue;
                }

                Vector3 current = boat.transform.position;
                Vector3 previous = _previousBoats[boat];
                _previousBoats[boat] = current;
                float radius = _activeLineWidth * 0.5f + boat.HitRadius;

                for (int point = 1; point < _electricPoints.Count; point++)
                {
                    if (MantaElectricRules.Intersects(previous, current, _electricPoints[point - 1], _electricPoints[point], radius))
                    {
                        _hitTargets.Add(boat);
                        boat.TryApplyDamage(_electricDamage);
                        
                        break;
                    }
                }
            }
        }

        private void ContinueAfterLaying(bool chase)
        {
            Vector3 direction = _sweepDirection;
            float speed = _layingSpeed;

            if (chase && HasValidTarget())
            {
                direction = _target.transform.position - _body.position;
                direction.y = 0f;
                speed = direction.magnitude <= _motion.ChaseStoppingDistance ? 0f : _monster.Profile.ChaseSpeed;
                _monster.TrySetBehaviorState(MonsterState.Chase);
            }

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion rotation = Quaternion.RotateTowards(_body.rotation,
                    Quaternion.LookRotation(direction, Vector3.up), _chaseTurnSpeed * Time.fixedDeltaTime);
                _body.MoveRotation(rotation);
                direction = rotation * Vector3.forward;
            }

            Vector3 velocity = _body.linearVelocity;
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            horizontal = Vector3.MoveTowards(horizontal, direction.normalized * speed,
                _postAttackAcceleration * Time.fixedDeltaTime);
            _body.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
        }

        private bool MoveTowards(Vector3 destination, float speed, float arrivalRadius = -1f, bool passThrough = false)
        {
            Vector3 offset = destination - _body.position;
            offset.y = 0f;
            float distance = offset.magnitude;

            if (distance <= (arrivalRadius >= 0f ? arrivalRadius : _alignmentRadius)
                || (passThrough && Vector3.Dot(offset, _sweepDirection) <= 0f))
            {
                if (!passThrough)
                {
                    _body.linearVelocity = new Vector3(0f, _body.linearVelocity.y, 0f);
                }

                return true;
            }

            Vector3 horizontal = offset.normalized * (passThrough ? speed : Mathf.Min(speed, distance / Time.fixedDeltaTime));
            _body.linearVelocity = new Vector3(horizontal.x, _body.linearVelocity.y, horizontal.z);

            if (offset.sqrMagnitude > 0.001f)
            {
                _body.MoveRotation(Quaternion.LookRotation(offset, Vector3.up));
            }

            return false;
        }

        private void StopAttack(bool completed)
        {
            if (!_ownsMovement)
            {
                return;
            }

            _ownsMovement = false;
            HasCompleted = completed;           

            _target = null;
            _targetBody = null;
            _previousBoats.Clear();
            _hitTargets.Clear();
            _boats.Clear();

            if (_motion != null)
            {
                _motion.SetMovementLocked(false);
                _motion.Stop();
            }

            if (_body != null && !_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
            }

            if (isServer)
            {
                _electricPoints.Clear();
                _expiresAt = 0d;
                SetPhase(MantaAttackPhase.Idle);
            }
        }

        private bool HasValidTarget()
        {
            return _target != null && _target.IsEligible
                && MonsterMotion.IsFinite(_target.transform.position)
                && _sensor.CanTrack(_target.gameObject, _monster.Profile.MaxChaseDistance);
        }

        private void SetPhase(MantaAttackPhase phase)
        {
            _phase = phase;            
        }

        private bool TryWaterHeight(Vector3 position, out float height)
        {
            if (_world == null)
            {
                WorldServices.TryGet(out _world);
            }

            height = _world == null ? _fallbackWaterHeight : _world.SampleWaterHeight(position);

            return !float.IsNaN(height) && !float.IsInfinity(height);
        }

        private bool HasValidSettings()
        {
            return Positive(_interceptSpeed) && Positive(_alignmentRadius)
                && NonNegative(_preparationSeconds) && Positive(_electricDuration) && Positive(_electricDamage)
                && Positive(_lineLength) && Positive(_lineWidth) && NonNegative(_zigzagWidth)
                && NonNegative(_predictionSeconds) && Positive(_minimumLead) && NonNegative(_surfaceOffset)
                && Positive(_timeoutSeconds) && _linePointCount >= 3 && _linePointCount <= 32
                && Positive(_layingSpeed) && MonsterMotion.IsFinite(_tailLocalOffset)
                && Positive(_chaseTurnSpeed) && Positive(_postAttackAcceleration)
                && NonNegative(_layingForwardAngle) && _layingForwardAngle <= 90f;
        }

        private static bool Positive(float value)
        {
            return NonNegative(value) && value > 0f;
        }

        private static bool NonNegative(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 tail = transform.TransformPoint(_tailLocalOffset);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(tail, 0.15f);
            Gizmos.DrawLine(transform.position, tail);
        }
    }
}
