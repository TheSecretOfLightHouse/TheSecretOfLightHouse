using UnityEngine;
using UnityEngine.VFX;

namespace Lighthouse.Monsters.AI.View
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VisualEffect))]
    public sealed class MantaElectricTrailEmitter : MonoBehaviour
    {
        private static readonly int TrailPointId = Shader.PropertyToID("TrailPoint");
        private static readonly int TailSparkId = Shader.PropertyToID("TailSpark");
        private static readonly int Pos1Id = Shader.PropertyToID("Pos1");
        private static readonly int Pos4Id = Shader.PropertyToID("Pos4");
        private static readonly int PositionId = Shader.PropertyToID("position");
        private static readonly int VelocityId = Shader.PropertyToID("velocity");
        private static readonly int LifetimeId = Shader.PropertyToID("lifetime");
        private static readonly int AgeId = Shader.PropertyToID("age");
        private static readonly int DistanceId = Shader.PropertyToID("spawnTime");
        private static readonly int SpawnCountId = Shader.PropertyToID("spawnCount");
        [SerializeField, Min(0.01f)] private float _sampleSpacing = 0.08f;
        [SerializeField, Min(0.01f)] private float _lifetime = 3f;
        [SerializeField, Min(0.1f)] private float _teleportDistance = 4f;
        [SerializeField, Min(1)] private int _maxSamplesPerUpdate = 64;
        private VisualEffect _effect;
        private VFXEventAttribute _eventAttribute;
        private Vector3 _previousPosition;
        private Vector3 _lastDirection;
        private float _remainder;
        private float _distance;
        private double _previousCreatedAt;

        public bool IsEmitting { get; private set; }
        public int EmittedPointCount { get; private set; }
        public float Lifetime => _lifetime;

        private void Awake()
        {
            _effect = GetComponent<VisualEffect>();
            _eventAttribute = _effect.CreateVFXEventAttribute();
            // 기존 고정 접점의 상대 오프셋을 제거하고 이벤트의 월드 위치를 사용한다.
            _effect.SetVector3(Pos1Id, Vector3.zero);
            _effect.SetVector3(Pos4Id, Vector3.zero);
        }

        private void OnDisable()
        {
            ClearTrail();
        }

        private void OnDestroy()
        {
            if (_eventAttribute != null)
            {
                _eventAttribute.Dispose();
                _eventAttribute = null;
            }
        }

        public void BeginTrail(Vector3 position, Vector3 direction)
        {
            ClearTrail();
            if (!IsFinite(position) || !IsFinite(direction) || _eventAttribute == null)
            {
                return;
            }
            _previousPosition = position;
            _lastDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            IsEmitting = true;
            EmitPoint(position, _lastDirection);
        }

        public void AppendPosition(Vector3 position)
        {
            if (!IsEmitting || !IsFinite(position))
            {
                return;
            }
            Vector3 delta = position - _previousPosition;
            float length = delta.magnitude;
            if (length > _teleportDistance)
            {
                BeginTrail(position, delta);
                return;
            }
            if (length <= 0.0001f)
            {
                return;
            }
            _lastDirection = delta / length;
            float next = _sampleSpacing - _remainder;
            int count = 0;
            while (next <= length && count < _maxSamplesPerUpdate)
            {
                _distance += _sampleSpacing;
                EmitPoint(_previousPosition + _lastDirection * next, _lastDirection);
                next += _sampleSpacing;
                count++;
            }
            _remainder = Mathf.Max(0f, length - (next - _sampleSpacing));
            _previousPosition = position;
        }

        public void BeginTimedTrail(Vector3 position, double createdAt, double now, float lifetime)
        {
            ClearTrail();
            if (!IsFinite(position) || _eventAttribute == null || lifetime <= 0f)
            {
                return;
            }
            _previousPosition = position;
            _previousCreatedAt = createdAt;
            _lastDirection = Vector3.forward;
            IsEmitting = true;
            EmitTimedPoint(position, _lastDirection, createdAt, now, lifetime);
        }

        public void AppendTimedPosition(Vector3 position, double createdAt, double now, float lifetime)
        {
            if (!IsEmitting || !IsFinite(position) || lifetime <= 0f || createdAt < _previousCreatedAt)
            {
                return;
            }
            Vector3 delta = position - _previousPosition;
            float length = delta.magnitude;
            if (length > 0.0001f)
            {
                _lastDirection = delta / length;
                int count = Mathf.Min(_maxSamplesPerUpdate, Mathf.Max(1, Mathf.CeilToInt(length / _sampleSpacing)));
                for (int index = 1; index <= count; index++)
                {
                    float fraction = (float)index / count;
                    _distance += length / count;
                    double sampleTime = _previousCreatedAt + (createdAt - _previousCreatedAt) * fraction;
                    EmitTimedPoint(Vector3.Lerp(_previousPosition, position, fraction), _lastDirection,
                        sampleTime, now, lifetime);
                }
            }
            else if (createdAt > _previousCreatedAt)
            {
                EmitTimedPoint(position, _lastDirection, createdAt, now, lifetime);
            }
            _previousPosition = position;
            _previousCreatedAt = createdAt;
        }

        private void EmitTimedPoint(Vector3 position, Vector3 direction, double createdAt, double now, float lifetime)
        {
            float elapsed = Mathf.Max(0f, (float)(now - createdAt));
            if (elapsed < lifetime)
            {
                EmitPoint(position, direction, lifetime, elapsed);
            }
        }

        public void EmitSparks(Vector3 position, int count)
        {
            if (_eventAttribute == null || !IsFinite(position) || count <= 0)
            {
                return;
            }
            _eventAttribute.SetVector3(PositionId, position);
            _eventAttribute.SetFloat(SpawnCountId, count);
            _effect.SendEvent(TailSparkId, _eventAttribute);
        }

        public void EndTrail()
        {
            if (IsEmitting && _remainder > 0.0001f)
            {
                _distance += _remainder;
                EmitPoint(_previousPosition, _lastDirection);
            }
            IsEmitting = false;
            _remainder = 0f;
        }

        public void ClearTrail()
        {
            IsEmitting = false;
            EmittedPointCount = 0;
            _remainder = 0f;
            _distance = 0f;
            if (_effect != null)
            {
                _effect.Reinit();
                _effect.Stop();
            }
        }

        private void EmitPoint(Vector3 position, Vector3 direction)
        {
            EmitPoint(position, direction, _lifetime, 0f);
        }

        private void EmitPoint(Vector3 position, Vector3 direction, float lifetime, float age)
        {
            // A shared event keeps all four ribbons on the same sampled world-space path.
            _eventAttribute.SetVector3(PositionId, position);
            _eventAttribute.SetVector3(VelocityId, direction);
            _eventAttribute.SetFloat(LifetimeId, lifetime);
            _eventAttribute.SetFloat(AgeId, age);
            _eventAttribute.SetFloat(DistanceId, _distance);
            _eventAttribute.SetFloat(SpawnCountId, 1f);
            _effect.SendEvent(TrailPointId, _eventAttribute);
            EmittedPointCount++;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
