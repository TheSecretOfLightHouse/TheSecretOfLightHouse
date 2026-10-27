using System.Collections.Generic;
using Lighthouse.Monsters.AI.Rules;
using Lighthouse.Monsters.AI.Server;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.AI.View
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MantaElectricAttack))]
    public sealed class MantaElectricView : MonoBehaviour
    {
        private sealed class CastView
        {
            public readonly SortedDictionary<uint, MantaTrailPoint> Pending = new SortedDictionary<uint, MantaTrailPoint>();
            public MantaElectricTrailEmitter Emitter;
            public uint LastSequence;
            public double LastCreatedAt;
            public float SparkRemainder;
        }

        [SerializeField] private MantaElectricTrailEmitter _trailPrefab;
        [Tooltip("공격 중 꼬리에서 초당 생성할 스파크 개수.")]
        [SerializeField, Min(0f)] private float _sparksPerSecond = 140f;
        private readonly Dictionary<uint, CastView> _casts = new Dictionary<uint, CastView>();
        private readonly List<uint> _finished = new List<uint>();
        private MantaElectricAttack _attack;

        private void Awake()
        {
            _attack = GetComponent<MantaElectricAttack>();
        }

        private void OnEnable()
        {
            _attack.TrailPointAdded += OnTrailPointAdded;
            _attack.TrailCleared += ClearAll;
        }

        private void LateUpdate()
        {
            if (!_attack.isClient || !_attack.isActiveAndEnabled || _trailPrefab == null
                || _attack.ActiveLifetime <= 0f)
            {
                return;
            }

            // 늦은 접속이나 재활성화 시 이미 동기화된 경로도 복원한다.
            IReadOnlyList<MantaTrailPoint> points = _attack.ElectricPoints;
            for (int index = 0; index < points.Count; index++)
            {
                ReceivePoint(points[index]);
            }

            double now = NetworkTime.time;
            _finished.Clear();
            foreach (KeyValuePair<uint, CastView> entry in _casts)
            {
                CastView cast = entry.Value;
                while (cast.Pending.Count > 0)
                {
                    MantaTrailPoint point = FirstPending(cast.Pending);
                    if (point.CreatedAt > now)
                    {
                        break;
                    }
                    cast.Pending.Remove(point.Sequence);
                    if (cast.Emitter == null)
                    {
                        cast.Emitter = Instantiate(_trailPrefab, transform);
                        cast.Emitter.BeginTimedTrail(point.Position, point.CreatedAt, now, _attack.ActiveLifetime);
                    }
                    else
                    {
                        cast.Emitter.AppendTimedPosition(point.Position, point.CreatedAt, now, _attack.ActiveLifetime);
                    }
                    cast.LastSequence = point.Sequence;
                    cast.LastCreatedAt = point.CreatedAt;
                }

                if (entry.Key == _attack.LayingCastId && cast.Emitter != null)
                {
                    cast.SparkRemainder += Mathf.Max(0f, _sparksPerSecond) * Time.deltaTime;
                    int count = Mathf.Min(32, Mathf.FloorToInt(cast.SparkRemainder));
                    if (count > 0)
                    {
                        cast.SparkRemainder = Mathf.Min(1f, cast.SparkRemainder - count);
                        cast.Emitter.EmitSparks(_attack.TailPosition, count);
                    }
                }

                if (cast.Pending.Count == 0 && entry.Key != _attack.LayingCastId
                    && cast.LastSequence != 0 && now >= cast.LastCreatedAt + _attack.ActiveLifetime)
                {
                    _finished.Add(entry.Key);
                }
            }
            for (int index = 0; index < _finished.Count; index++)
            {
                RemoveCast(_finished[index]);
            }
        }

        private void OnTrailPointAdded(int index, MantaTrailPoint point)
        {
            if (_attack.isClient)
            {
                ReceivePoint(point);
            }
        }

        private void ReceivePoint(MantaTrailPoint point)
        {
            if (!_casts.TryGetValue(point.CastId, out CastView cast))
            {
                cast = new CastView();
                _casts.Add(point.CastId, cast);
            }
            if (point.Sequence > cast.LastSequence)
            {
                cast.Pending[point.Sequence] = point;
            }
        }

        private static MantaTrailPoint FirstPending(SortedDictionary<uint, MantaTrailPoint> pending)
        {
            using (SortedDictionary<uint, MantaTrailPoint>.Enumerator iterator = pending.GetEnumerator())
            {
                iterator.MoveNext();
                return iterator.Current.Value;
            }
        }

        private void OnDisable()
        {
            _attack.TrailPointAdded -= OnTrailPointAdded;
            _attack.TrailCleared -= ClearAll;
            ClearAll();
        }

        private void ClearAll()
        {
            foreach (CastView cast in _casts.Values)
            {
                if (cast.Emitter != null)
                {
                    cast.Emitter.ClearTrail();
                    Destroy(cast.Emitter.gameObject);
                }
            }
            _casts.Clear();
        }

        private void RemoveCast(uint id)
        {
            CastView cast = _casts[id];
            if (cast.Emitter != null)
            {
                cast.Emitter.ClearTrail();
                Destroy(cast.Emitter.gameObject);
            }
            _casts.Remove(id);
        }
    }
}
