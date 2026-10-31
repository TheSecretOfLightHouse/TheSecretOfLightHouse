using UnityEngine;

namespace Lighthouse.Monsters.Boss
{
    [CreateAssetMenu(fileName = "WhipTentacleSettings", menuName = "TheLightHouse/Monster/Whip Tentacle Settings")]
    public sealed class WhipTentacleSettings : ScriptableObject
    {
        [SerializeField] private AnimationClip _attackClip;
        [SerializeField] private AnimationClip _idleClip;
        [SerializeField, Min(0f)] private float _damage = 10f;
        [SerializeField, Min(0.01f)] private float _attackSpeed = 1f;
        [SerializeField, Range(0f, 1f)] private float _hitStart = 0.25f;
        [SerializeField, Range(0f, 1f)] private float _hitEnd = 0.7f;
        [SerializeField, HideInInspector] private Bounds _attackEnvelope;
        [SerializeField, HideInInspector] private Bounds[] _attackFrames = new Bounds[0];

        public AnimationClip AttackClip => _attackClip;
        public AnimationClip IdleClip => _idleClip;
        public float Damage => _damage;
        public float AttackSpeed => _attackSpeed;
        public float HitStart => _hitStart;
        public float HitEnd => _hitEnd;
        public Bounds AttackEnvelope => _attackEnvelope;
        public int FrameCount => _attackFrames.Length;
        public bool IsValid => _attackClip != null && _idleClip != null && _attackClip.length > 0f
            && _idleClip.length > 0f && FrameCount >= 2 && _hitStart >= 0f && _hitEnd <= 1f
            && _hitStart <= _hitEnd && IsFinite(_damage) && _damage >= 0f
            && IsFinite(_attackSpeed) && _attackSpeed > 0f;

        public Bounds GetFrame(int index)
        {
            return _attackFrames[Mathf.Clamp(index, 0, FrameCount - 1)];
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
