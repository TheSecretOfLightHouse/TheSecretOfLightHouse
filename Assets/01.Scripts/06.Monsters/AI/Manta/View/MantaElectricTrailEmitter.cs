using UnityEngine;
using UnityEngine.VFX;
using Mirror;

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
        private static readonly int ColorId = Shader.PropertyToID("Color");
        [SerializeField, Min(0.01f)] private float _sampleSpacing = 0.08f;
        [SerializeField, Min(0.01f)] private float _lifetime = 3f;
        [SerializeField, Min(0.1f)] private float _teleportDistance = 4f;
        [SerializeField, Min(1)] private int _maxSamplesPerUpdate = 64;
        [SerializeField] private VisualEffect _directionalElectricity;
        [SerializeField, Min(0.1f)] private float _directionalSpacing = 1.2f;
        [SerializeField, Min(1)] private int _maxDirectionalEffects = 8;
        [SerializeField, Min(2)] private int _yellowEvery = 4;
        [SerializeField] private Vector4 _blueElectricColor = new Vector4(34.13f, 72.28f, 191.75f, 1f);
        [SerializeField] private Vector4 _yellowElectricColor = new Vector4(191.75f, 162.21f, 34.13f, 1f);
        private VisualEffect _effect;
        private VFXEventAttribute _eventAttribute;
        private Vector3 _previousPosition;
        private Vector3 _lastDirection;
        private float _remainder;
        private float _distance;
        private double _previousCreatedAt;
        private VisualEffect[] _directionalEffects;
        private Vector3[] _directionalPositions;
        private Quaternion[] _directionalRotations;
        private double[] _directionalExpiresAt;
        private float _lastDirectionalDistance;
        private int _directionalColorIndex;

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
            int count = Mathf.Max(1, _maxDirectionalEffects);
            _directionalEffects = new VisualEffect[count];
            _directionalPositions = new Vector3[count];
            _directionalRotations = new Quaternion[count];
            _directionalExpiresAt = new double[count];
        }

        private void OnDisable()
        {
            ClearTrail();
        }

        private void LateUpdate()
        {
            if (_directionalEffects == null)
            {
                return;
            }

            double now = NetworkTime.time;
            for (int index = 0; index < _directionalEffects.Length; index++)
            {
                VisualEffect effect = _directionalEffects[index];
                if (effect == null || _directionalExpiresAt[index] <= 0d)
                {
                    continue;
                }
                if (now >= _directionalExpiresAt[index])
                {
                    effect.Reinit();
                    effect.Stop();
                    _directionalExpiresAt[index] = 0d;
                    continue;
                }
                effect.transform.SetPositionAndRotation(_directionalPositions[index], _directionalRotations[index]);
            }
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
                if (_distance - _lastDirectionalDistance >= _directionalSpacing)
                {
                    EmitDirectional(position, direction, createdAt + lifetime, now);
                    _lastDirectionalDistance = _distance;
                }
            }
        }

        private void EmitDirectional(Vector3 position, Vector3 direction, double expiresAt, double now)
        {
            if (_directionalElectricity == null || _directionalEffects == null || direction.sqrMagnitude < 0.0001f)
            {
                return;
            }
            int slot = 0;
            for (int index = 0; index < _directionalEffects.Length; index++)
            {
                if (_directionalEffects[index] == null || _directionalExpiresAt[index] <= now)
                {
                    slot = index;
                    break;
                }
                if (_directionalExpiresAt[index] < _directionalExpiresAt[slot])
                {
                    slot = index;
                }
            }
            VisualEffect effect = _directionalEffects[slot];
            if (effect == null)
            {
                effect = Instantiate(_directionalElectricity, transform);
                _directionalEffects[slot] = effect;
            }
            effect.Reinit();
            effect.Stop();
            _directionalPositions[slot] = position;
            _directionalRotations[slot] = Quaternion.LookRotation(-direction, Vector3.up);
            _directionalExpiresAt[slot] = expiresAt;
            effect.transform.SetPositionAndRotation(position, _directionalRotations[slot]);
            _directionalColorIndex++;
            effect.SetVector4(ColorId, _directionalColorIndex % Mathf.Max(2, _yellowEvery) == 0
                ? _yellowElectricColor : _blueElectricColor);
            effect.Play();
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
            _lastDirectionalDistance = 0f;
            _directionalColorIndex = 0;
            if (_directionalElectricity != null)
            {
                _directionalElectricity.Reinit();
                _directionalElectricity.Stop();
            }
            if (_directionalEffects != null)
            {
                for (int index = 0; index < _directionalEffects.Length; index++)
                {
                    if (_directionalEffects[index] != null)
                    {
                        _directionalEffects[index].Reinit();
                        _directionalEffects[index].Stop();
                    }
                    _directionalExpiresAt[index] = 0d;
                }
            }
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
