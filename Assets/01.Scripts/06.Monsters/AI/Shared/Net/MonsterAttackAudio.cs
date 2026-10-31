using Lighthouse.Monsters.AI.Server;
using Lighthouse.Monsters.AI.Rules;
using Lighthouse.Systems.Audio;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Net
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SeaMonster))]
    public sealed class MonsterAttackAudio : NetworkBehaviour
    {
        [SerializeField] private AudioClip _attackClip;
        [SerializeField] private SoundManager _soundManagerPrefab;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.7f;
        [SerializeField, Min(0.01f)] private float _minDistance = 2f;
        [SerializeField, Min(0.02f)] private float _maxDistance = 40f;

        private SeaMonster _monster;
        private MantaElectricAttack _electricAttack;
        private AudioSource _electricSource;

        private void Awake()
        {
            _monster = GetComponent<SeaMonster>();
            _electricAttack = GetComponent<MantaElectricAttack>();
        }

        private void Update()
        {
            if (!isClient || _electricSource == null)
            {
                return;
            }

            var points = _electricAttack.ElectricPoints;
            bool active = !_monster.IsDead && _electricAttack.isActiveAndEnabled && points.Count > 0
                && NetworkTime.time - points[points.Count - 1].CreatedAt < _electricAttack.ActiveLifetime;
            if (!active)
            {
                _electricSource.Stop();
                return;
            }

            _electricSource.transform.position = _electricAttack.Phase == MantaAttackPhase.Laying
                ? _electricAttack.TailPosition : points[points.Count - 1].Position;
            SoundManager manager = SoundManager.Instance;
            _electricSource.volume = manager != null && !manager.IsMuted
                ? manager.MasterVolume * manager.EffectVolume * _volume : 0f;
            if (!_electricSource.isPlaying)
            {
                _electricSource.Play();
            }
        }

        private void OnDisable()
        {
            if (_electricSource != null)
            {
                _electricSource.Stop();
            }
        }

        public override void OnStartClient()
        {
            EnsureSoundManager();
            if (_electricAttack == null || _attackClip == null || _electricSource != null)
            {
                return;
            }

            var child = new GameObject("ElectricAttackAudio");
            child.transform.SetParent(transform, false);
            _electricSource = child.AddComponent<AudioSource>();
            _electricSource.playOnAwake = false;
            _electricSource.clip = _attackClip;
            _electricSource.loop = true;
            _electricSource.spatialBlend = 1f;
            _electricSource.dopplerLevel = 0f;
            _electricSource.rolloffMode = AudioRolloffMode.Linear;
            _electricSource.minDistance = _minDistance;
            _electricSource.maxDistance = Mathf.Max(_minDistance + 0.01f, _maxDistance);
        }

        public override void OnStopClient()
        {
            if (_electricSource != null)
            {
                _electricSource.Stop();
            }
        }

        [Server]
        public void PlayImpact(Vector3 position)
        {
            RpcPlayImpact(position, NetworkTime.time);
        }

        [ClientRpc]
        private void RpcPlayImpact(Vector3 position, double startedAt)
        {
            // Expired contact events must not sound after a stalled connection resumes.
            if (!isActiveAndEnabled || _attackClip == null
                || NetworkTime.time - startedAt >= _attackClip.length)
            {
                return;
            }
            EnsureSoundManager();
            SoundManager manager = SoundManager.Instance;
            if (manager != null)
            {
                manager.PlayEffectAt(_attackClip, position, _volume);
            }
        }

        private void EnsureSoundManager()
        {
            if (SoundManager.Instance == null && _soundManagerPrefab != null)
            {
                Instantiate(_soundManagerPrefab);
            }
        }
    }
}
