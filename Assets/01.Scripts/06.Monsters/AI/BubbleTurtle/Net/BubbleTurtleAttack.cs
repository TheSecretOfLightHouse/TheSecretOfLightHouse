using Lighthouse.Monsters.AI.Server;
using Lighthouse.Monsters.AI.View;
using Lighthouse.Systems.Audio;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Net
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster), typeof(MonsterMotion), typeof(MonsterAttackRange))]
    public sealed class BubbleTurtleAttack : NetworkBehaviour
    {
        [SerializeField] private AnimationClip _attackClip;
        [SerializeField] private Transform _firePoint;
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField, Range(0f, 1f)] private float _fireNormalizedTime = 0.5f;
        [SerializeField, Min(0.01f)] private float _playbackSpeed = 1f;
        [SerializeField] private AudioClip _chargeSound;
        [SerializeField] private AudioClip _fireSound;
        [SerializeField] private SoundManager _soundManagerPrefab;
        [SerializeField, Range(0f, 1f)] private float _soundVolume = 0.7f;

        [SyncVar] private double _attackStartedAt;
        [SyncVar] private float _activePlaybackSpeed = 1f;
        [SyncVar] private uint _attackSequence;
        [SyncVar] private bool _isAttacking;
        [SyncVar] private bool _hasFired;
        [SyncVar] private float _preparationDuration;

        private SeaMonster _monster;
        private MonsterMotion _motion;
        private SeaMonsterSensor _sensor;
        private MonsterAttackRange _range;
        private Rigidbody _body;
        private MonsterHitShake _hitShake;
        private NetworkIdentity _projectileIdentity;
        private BubbleProjectile _projectileTemplate;
        private MonsterBoatTarget _targetBoat;
        private GameObject _target;
        private double _fireAt;
        private double _endsAt;
        private double _nextAttackAt;
        private AudioSource _chargeSource;
        private uint _chargeSequence;
        private uint _firedSoundSequence;

        public bool IsAttacking => _isAttacking;
        public bool HasFired => _hasFired;
        public float PreparationDuration => _preparationDuration;
        public bool HasCompleted { get; private set; }
        public double AttackStartedAt => _attackStartedAt;
        public float ActivePlaybackSpeed => _activePlaybackSpeed;
        public uint AttackSequence => _attackSequence;
        public uint ShotCount { get; private set; }
        public double LastFiredAt { get; private set; }
        public double NextAttackAt => _nextAttackAt;
        public bool IsCooldownReady => isServer && NetworkTime.time >= _nextAttackAt;
        public bool HasValidSetup => _attackClip != null && IsPositive(_attackClip.length)
            && _firePoint != null && _projectilePrefab != null && _projectileIdentity != null
            && _projectileTemplate != null && _projectileTemplate.HasValidSettings
            && IsPositive(_playbackSpeed) && IsFinite(_fireNormalizedTime)
            && _fireNormalizedTime >= 0f && _fireNormalizedTime <= 1f;

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            _motion = GetComponent<MonsterMotion>();
            _sensor = GetComponent<SeaMonsterSensor>();
            _range = GetComponent<MonsterAttackRange>();
            _body = GetComponent<Rigidbody>();
            _hitShake = GetComponent<MonsterHitShake>();

            if (_projectilePrefab != null)
            {
                _projectileIdentity = _projectilePrefab.GetComponent<NetworkIdentity>();
                _projectileTemplate = _projectilePrefab.GetComponent<BubbleProjectile>();
            }
        }

        private void Update()
        {
            if (isClient)
            {
                UpdateChargeSound();
            }
            if (!isServer || !_isAttacking)
            {
                return;
            }

            if (!CanAct() || !_sensor.CanTrack(_target, _monster.Profile.MaxChaseDistance))
            {
                Cancel();
                return;
            }

            double now = NetworkTime.time;

            if (!_hasFired && now >= _fireAt)
            {
                Fire(now);
            }

            if (now >= _endsAt)
            {
                Finish(true);
            }
        }

        private void OnDisable()
        {
            StopChargeSound();
            if (isServer)
            {
                Finish(false);
            }
        }

        public override void OnStartServer()
        {
            _nextAttackAt = 0d;
            _isAttacking = false;
            HasCompleted = false;
            ShotCount = 0;
        }

        public override void OnStartClient()
        {
            EnsureSoundManager();
            if (_chargeSource == null)
            {
                var child = new GameObject("BubbleChargeAudio");
                child.transform.SetParent(_firePoint != null ? _firePoint : transform, false);
                _chargeSource = child.AddComponent<AudioSource>();
                _chargeSource.playOnAwake = false;
                _chargeSource.spatialBlend = 1f;
                _chargeSource.dopplerLevel = 0f;
                _chargeSource.rolloffMode = AudioRolloffMode.Linear;
                _chargeSource.minDistance = 2f;
                _chargeSource.maxDistance = 40f;
            }
            // 공용 매니저를 수정하지 않고 거북이 기능에서 투사체 프리팹을 등록한다.
            if (_projectileIdentity != null && _projectileIdentity.assetId != 0)
            {
                if (!NetworkClient.prefabs.ContainsKey(_projectileIdentity.assetId))
                {
                    NetworkClient.RegisterPrefab(_projectilePrefab);
                }
            }
        }

        public override void OnStopServer()
        {
            Finish(false);
        }

        [Server]
        public bool BeginAttack(GameObject target)
        {
            if (_isAttacking || !CanAct() || !IsCooldownReady || !_range.IsTargetInRange(target))
            {
                return false;
            }

            MonsterBoatTarget targetBoat = target.GetComponent<MonsterBoatTarget>();
            if (targetBoat == null || !targetBoat.IsEligible)
            {
                return false;
            }

            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                _body.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }

            _motion.SetMovementLocked(true);
            _target = target;
            _targetBoat = targetBoat;
            _hasFired = false;
            HasCompleted = false;
            _activePlaybackSpeed = _playbackSpeed;
            _attackStartedAt = NetworkTime.time;
            double duration = _attackClip.length / _activePlaybackSpeed;
            _preparationDuration = (float)(duration * _fireNormalizedTime);
            _fireAt = _attackStartedAt + _preparationDuration;
            _endsAt = _attackStartedAt + duration;
            _attackSequence++;
            _isAttacking = true;
            _monster.TrySetBehaviorState(MonsterState.Attack);

            return true;
        }

        [Server]
        public void Cancel()
        {
            Finish(false);
        }

        private bool CanAct()
        {
            return isActiveAndEnabled && _monster != null && _monster.IsInitialized && !_monster.IsDead
                && _motion != null && _motion.CanMove && _sensor != null && _sensor.isActiveAndEnabled
                && _range != null && _range.CanAct && _body != null && HasValidSetup;
        }

        private void Fire(double now)
        {
            // 호스트의 중복 생성을 막기 위해 애니메이션 콜백 대신 서버에서 한 번만 생성한다.
            _hasFired = true;

            Vector3 firePosition = _hitShake != null ? _hitShake.GetUnshakenPosition(_firePoint) : _firePoint.position;
            Vector3 direction = _target.transform.position - firePosition;
            Quaternion rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction, Vector3.up) : transform.rotation;
            GameObject projectile = Instantiate(_projectilePrefab, firePosition, rotation);
            BubbleProjectile movement = projectile.GetComponent<BubbleProjectile>();
            if (movement == null || !movement.Initialize(_targetBoat, netIdentity, _monster.Profile.AttackDamage, _attackStartedAt))
            {
                Destroy(projectile);
                Cancel();
                return;
            }

            NetworkServer.Spawn(projectile);
            ShotCount++;
            LastFiredAt = now;
            RpcPlayFireSound(firePosition, now, _attackSequence);
            _nextAttackAt = now + _monster.Profile.AttackCooldown;
        }

        private void Finish(bool completed)
        {
            if (!_isAttacking)
            {
                return;
            }

            _isAttacking = false;
            HasCompleted = completed;
            _target = null;
            _targetBoat = null;

            if (_motion != null)
            {
                _motion.SetMovementLocked(false);
                _motion.Stop();
            }
        }

        public override void OnStopClient()
        {
            StopChargeSound();
            _chargeSequence = 0;
            _firedSoundSequence = 0;
        }

        private void UpdateChargeSound()
        {
            float elapsed = (float)System.Math.Max(0d, NetworkTime.time - _attackStartedAt);
            if (_chargeSource == null || _chargeSound == null || _monster.IsDead || !_isAttacking
                || _hasFired || _attackSequence == _firedSoundSequence || _preparationDuration <= 0f
                || elapsed >= _preparationDuration)
            {
                StopChargeSound();
                return;
            }
            SoundManager manager = SoundManager.Instance;
            _chargeSource.volume = manager != null && !manager.IsMuted
                ? manager.MasterVolume * manager.EffectVolume * _soundVolume : 0f;
            if (_chargeSequence != _attackSequence)
            {
                _chargeSequence = _attackSequence;
                _chargeSource.clip = _chargeSound;
                // Fit the rising charge to the same preparation interval as the bubble effect.
                _chargeSource.pitch = Mathf.Clamp(_chargeSound.length / _preparationDuration, 0.01f, 3f);
                _chargeSource.time = Mathf.Min(elapsed * _chargeSource.pitch, _chargeSound.length - 0.001f);
                _chargeSource.Play();
            }
        }

        [ClientRpc]
        private void RpcPlayFireSound(Vector3 position, double startedAt, uint sequence)
        {
            _firedSoundSequence = sequence;
            StopChargeSound();
            // A short pop must still be heard when normal network latency exceeds its duration.
            if (_fireSound == null || NetworkTime.time - startedAt >= Mathf.Max(1f, _fireSound.length))
            {
                return;
            }
            EnsureSoundManager();
            SoundManager manager = SoundManager.Instance;
            if (manager != null)
            {
                manager.PlayEffectAt(_fireSound, position, _soundVolume);
            }
        }

        private void StopChargeSound()
        {
            if (_chargeSource != null)
            {
                _chargeSource.Stop();
            }
        }

        private void EnsureSoundManager()
        {
            if (SoundManager.Instance == null && _soundManagerPrefab != null)
            {
                Instantiate(_soundManagerPrefab);
            }
        }

        private static bool IsPositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
