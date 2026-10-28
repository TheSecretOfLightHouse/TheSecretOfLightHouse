using System;
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
        [Header("전기 궤적·피해")]
        [Tooltip("각 전기 궤적 지점이 생성된 뒤 유지되는 시간(초).")]
        [SerializeField, Min(0.01f)] private float _electricDuration = 3f;
        [Tooltip("전기 영역 하나가 같은 배에 한 번 주는 피해. MonsterProfile의 공격력과 별도로 사용한다.")]
        [SerializeField, Min(0.01f)] private float _electricDamage = 20f;
        [SerializeField, Min(0.01f)] private float _lineLength = 8f;
        [SerializeField, Min(0.01f)] private float _lineWidth = 0.6f;
        [Header("꼬리 전깃줄 설치")]
        [Tooltip("가로 방향에서 배 전방으로 기울이는 각도. 0도는 가로, 90도는 전방이다.")]
        [SerializeField, Range(0f, 90f)] private float _layingForwardAngle = 30f;
        [SerializeField, Min(0.01f)] private float _layingSpeed = 10f;
        [Tooltip("설치 경로 중간이 직선에서 휘어지는 거리의 최소·최대값이다.")]
        [SerializeField] private Vector2 _curveOffsetRange = new Vector2(0.8f, 2f);
        [Tooltip("전기 경로의 기준이 되는 꼬리 뼈. 지정하지 않으면 로컬 오프셋을 사용한다.")]
        [SerializeField] private Transform _tailBone;
        [SerializeField] private Vector3 _tailLocalOffset = new Vector3(0f, 0f, -2.7f);

        [SyncVar] private MantaAttackPhase _phase;       
        [SyncVar] private float _activeLifetime;
        [SyncVar] private float _activeLineWidth;
        [SyncVar] private uint _layingCastId;
        private readonly SyncList<MantaTrailPoint> _electricPoints = new SyncList<MantaTrailPoint>();
        private readonly Dictionary<uint, HashSet<MonsterBoatTarget>> _castHits = new Dictionary<uint, HashSet<MonsterBoatTarget>>();
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
        private Vector3 _sweepStart;
        private Vector3 _sweepEnd;
        private Vector3 _sweepSide;
        private float _sweepProgress;
        private float _sweepCurveOffset;
        private bool _reverseSweep;
        private Vector3 _lastTail;
        private uint _nextCastId;
        private uint _currentCastId;
        private uint _nextPointSequence;
        private bool _hasTrailState;

        public MantaAttackPhase Phase => _phase;        
        public float ActiveLifetime => _activeLifetime;
        public uint LayingCastId => _layingCastId;
        public event Action<int, MantaTrailPoint> TrailPointAdded
        {
            add => _pointAdded += value;
            remove => _pointAdded -= value;
        }
        public event Action TrailCleared
        {
            add => _electricPoints.OnClear += value;
            remove => _electricPoints.OnClear -= value;
        }
        private event Action<int, MantaTrailPoint> _pointAdded;

        public float ActiveLineWidth => _activeLineWidth;
        public IReadOnlyList<MantaTrailPoint> ElectricPoints => _electricPoints;
        public Vector3 TailPosition => _tailBone != null ? _tailBone.position : transform.TransformPoint(_tailLocalOffset);
        public bool IsAttacking => _ownsMovement;
        public bool HasCompleted { get; private set; }       
        public bool CanAct => isServer && isActiveAndEnabled && _monster != null
            && _monster.IsInitialized && !_monster.IsDead && _monster.isActiveAndEnabled
            && _body != null && !_body.isKinematic && _motion != null && _motion.CanMove
            && _sensor != null && _sensor.isActiveAndEnabled && _monster.Profile != null
            && HasValidSettings();

        private void Awake()
        {
            _electricPoints.OnChange += OnTrailPointChanged;
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

            double now = NetworkTime.time;
            PruneExpiredPoints(now);
            if (_electricPoints.Count > 0)
            {
                SampleBoats(true);
                ApplyElectricDamage(now);
            }
            else
            {
                _previousBoats.Clear();
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
                if (_reverseSweep)
                {
                    layingDirection = -layingDirection;
                }
                Vector3 side = Vector3.Cross(Vector3.up, layingDirection);
                Vector3 initialDirection = layingDirection * _lineLength + side * (4f * _sweepCurveOffset);
                Vector3 start = center - layingDirection * (_lineLength * 0.5f);
                Vector3 tailOffset = Quaternion.LookRotation(initialDirection, Vector3.up)
                    * Vector3.Scale(GetTailLocalPosition(), transform.lossyScale);

                tailOffset.y = 0f;
                start -= tailOffset;
                start.y = _body.position.y;

                if (MoveTowards(start, _interceptSpeed))
                {
                    _sweepStart = _body.position;
                    _sweepEnd = start + layingDirection * _lineLength;
                    _sweepSide = side;
                    _sweepProgress = 0f;
                    Vector3 tangent = MantaElectricRules.SweepTangent(
                        _sweepStart, _sweepEnd, _sweepSide, _sweepCurveOffset, 0f);
                    if (tangent.sqrMagnitude < 0.0001f)
                    {
                        tangent = initialDirection;
                    }
                    _activeLineWidth = _lineWidth;
                    _activeLifetime = _electricDuration;
                    _lastTail = GetTailPosition();
                    _currentCastId = ++_nextCastId;
                    _layingCastId = _currentCastId;
                    _nextPointSequence = 0;
                    _hasTrailState = true;
                    _castHits.Add(_currentCastId, new HashSet<MonsterBoatTarget>());
                    AddTrailPoint(_lastTail, now);
                    _body.MoveRotation(Quaternion.LookRotation(tangent, Vector3.up));
                    SetPhase(MantaAttackPhase.Laying);
                    SampleBoats(true);
                    ApplyElectricDamage(now);
                }
            }
            else if (_phase == MantaAttackPhase.Laying)
            {
                RecordTailTrail(now);
                ApplyElectricDamage(now);
                if (!_ownsMovement)
                {
                    return;
                }
                if (AdvanceSweep())
                {
                    FinishLaying();
                }
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

            _reverseSweep = UnityEngine.Random.value < 0.5f;
            float curve = UnityEngine.Random.Range(_curveOffsetRange.x, _curveOffsetRange.y);
            _sweepCurveOffset = UnityEngine.Random.value < 0.5f ? -curve : curve;
            _previousTargetPosition = target.transform.position;
            _targetVelocity = _targetBody != null ? _targetBody.linearVelocity : Vector3.zero;
            _elapsed = 0f;
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

        private Vector3 GetTailLocalPosition()
        {
            return _tailBone != null ? transform.InverseTransformPoint(_tailBone.position) : _tailLocalOffset;
        }

        private Vector3 GetTailPosition()
        {
            return _body.position + _body.rotation * Vector3.Scale(GetTailLocalPosition(), transform.lossyScale);
        }

        private void RecordTailTrail(double now)
        {
            Vector3 tail = GetTailPosition();
            float distance = Vector3.Distance(_lastTail, tail);
            if (distance <= 0.0001f)
            {
                return;
            }

            // 정지 후 다시 움직일 때 정지 시간 전체에 걸쳐 이동한 것처럼 보간하지 않는다.
            MantaTrailPoint previous = _electricPoints[_electricPoints.Count - 1];
            double stepStart = now - Time.fixedDeltaTime;
            if (previous.CastId == _currentCastId && now - previous.CreatedAt > Time.fixedDeltaTime * 1.5d)
            {
                AddTrailPoint(previous.Position, stepStart);
            }

            // 이동 중인 끝부분도 즉시 판정되도록 물리 갱신마다 꼬리 위치와 생성 시각을 기록한다.
            AddTrailPoint(tail, now);
            _lastTail = tail;
        }

        private void AddTrailPoint(Vector3 position, double createdAt)
        {
            _electricPoints.Add(new MantaTrailPoint(_currentCastId, position, createdAt, ++_nextPointSequence));
        }

        private void OnTrailPointChanged(SyncList<MantaTrailPoint>.Operation operation, int index, MantaTrailPoint point)
        {
            if (operation == SyncList<MantaTrailPoint>.Operation.OP_ADD)
            {
                _pointAdded?.Invoke(index, point);
            }
        }

        private void PruneExpiredPoints(double now)
        {
            double cutoff = now - _activeLifetime;
            while (_electricPoints.Count > 0)
            {
                MantaTrailPoint first = _electricPoints[0];
                if (first.CreatedAt > cutoff)
                {
                    break;
                }
                bool hasNext = _electricPoints.Count > 1 && _electricPoints[1].CastId == first.CastId;
                if (hasNext && _electricPoints[1].CreatedAt > cutoff)
                {
                    break;
                }
                if (!hasNext && _ownsMovement && _phase == MantaAttackPhase.Laying && first.CastId == _currentCastId)
                {
                    break;
                }
                _electricPoints.RemoveAt(0);
                if (!hasNext)
                {
                    _castHits.Remove(first.CastId);
                }
            }
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

        private void ApplyElectricDamage(double now)
        {
            double cutoff = now - _activeLifetime;
            for (int index = 0; index < _boats.Count; index++)
            {
                MonsterBoatTarget boat = _boats[index];
                if (boat == null || !boat.IsEligible || !_previousBoats.TryGetValue(boat, out Vector3 previous))
                {
                    continue;
                }
                Vector3 current = boat.transform.position;
                float radius = _activeLineWidth * 0.5f + boat.HitRadius;
                for (int point = 0; point < _electricPoints.Count; point++)
                {
                    MantaTrailPoint end = _electricPoints[point];
                    if (!_castHits.TryGetValue(end.CastId, out HashSet<MonsterBoatTarget> hits) || hits.Contains(boat))
                    {
                        continue;
                    }
                    Vector3 startPosition = end.Position;
                    Vector3 endPosition = end.Position;
                    bool live = end.CreatedAt > cutoff;
                    if (point > 0 && _electricPoints[point - 1].CastId == end.CastId)
                    {
                        live = MantaElectricRules.TryClipLiveSegment(_electricPoints[point - 1], end, cutoff,
                            out startPosition, out endPosition);
                    }
                    if (live && MantaElectricRules.Intersects(previous, current, startPosition, endPosition, radius))
                    {
                        hits.Add(boat);
                        boat.TryApplyDamage(_electricDamage);
                        if (!boat.IsEligible || !isActiveAndEnabled)
                        {
                            break;
                        }
                    }
                }
                _previousBoats[boat] = current;
            }
        }

        private void FinishLaying()
        {
            _ownsMovement = false;
            HasCompleted = true;
            _target = null;
            _targetBody = null;
            _motion.SetMovementLocked(false);
            _layingCastId = 0;
            SetPhase(MantaAttackPhase.Idle);
        }

        private bool AdvanceSweep()
        {
            Vector3 waypoint = MantaElectricRules.SweepPoint(
                _sweepStart, _sweepEnd, _sweepSide, _sweepCurveOffset, _sweepProgress);
            Vector3 lag = waypoint - _body.position;
            lag.y = 0f;
            float step = _layingSpeed * Time.fixedDeltaTime;
            if (lag.magnitude <= Mathf.Max(0.05f, step * 1.5f) && _sweepProgress < 1f)
            {
                Vector3 tangent = MantaElectricRules.SweepTangent(
                    _sweepStart, _sweepEnd, _sweepSide, _sweepCurveOffset, _sweepProgress);
                _sweepProgress = Mathf.Min(1f, _sweepProgress + step / Mathf.Max(0.01f, tangent.magnitude));
                waypoint = MantaElectricRules.SweepPoint(
                    _sweepStart, _sweepEnd, _sweepSide, _sweepCurveOffset, _sweepProgress);
            }
            Vector3 offset = waypoint - _body.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (_sweepProgress >= 1f && distance <= Mathf.Min(_alignmentRadius, 0.05f))
            {
                return true;
            }
            if (distance > 0.0001f)
            {
                Vector3 horizontal = offset.normalized * Mathf.Min(_layingSpeed, distance / Time.fixedDeltaTime);
                _body.linearVelocity = new Vector3(horizontal.x, _body.linearVelocity.y, horizontal.z);
                _body.MoveRotation(Quaternion.LookRotation(offset, Vector3.up));
            }
            return false;
        }

        private bool MoveTowards(Vector3 destination, float speed)
        {
            Vector3 offset = destination - _body.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance <= _alignmentRadius)
            {
                _body.linearVelocity = new Vector3(0f, _body.linearVelocity.y, 0f);
                return true;
            }
            Vector3 horizontal = offset.normalized * Mathf.Min(speed, distance / Time.fixedDeltaTime);
            _body.linearVelocity = new Vector3(horizontal.x, _body.linearVelocity.y, horizontal.z);
            if (offset.sqrMagnitude > 0.001f)
            {
                _body.MoveRotation(Quaternion.LookRotation(offset, Vector3.up));
            }
            return false;
        }

        private void StopAttack(bool completed)
        {
            bool ownedMovement = _ownsMovement;
            _ownsMovement = false;
            HasCompleted = completed;
            _target = null;
            _targetBody = null;
            _previousBoats.Clear();
            _castHits.Clear();
            _boats.Clear();
            if (ownedMovement && _motion != null)
            {
                _motion.SetMovementLocked(false);
                _motion.Stop();
            }
            if (ownedMovement && _body != null && !_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
            }
            if (isServer)
            {
                if (_hasTrailState || _electricPoints.Count > 0)
                {
                    _electricPoints.Clear();
                    _hasTrailState = false;
                }
                _layingCastId = 0;
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
                && Positive(_electricDuration) && Positive(_electricDamage)
                && Positive(_lineLength) && Positive(_lineWidth)
                && NonNegative(_predictionSeconds) && Positive(_minimumLead)
                && Positive(_timeoutSeconds)
                && Positive(_layingSpeed) && MonsterMotion.IsFinite(_tailLocalOffset)
                && NonNegative(_curveOffsetRange.x) && NonNegative(_curveOffsetRange.y)
                && _curveOffsetRange.y >= _curveOffsetRange.x
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
            Vector3 tail = _tailBone != null ? _tailBone.position : transform.TransformPoint(_tailLocalOffset);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(tail, 0.15f);
            Gizmos.DrawLine(transform.position, tail);
        }
    }
}
