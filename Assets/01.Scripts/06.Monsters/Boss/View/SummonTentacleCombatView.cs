using Lighthouse.Tower.Rules;
using Lighthouse.Systems.Audio;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.Boss
{
    [DefaultExecutionOrder(20000)]
    [DisallowMultipleComponent]
    public sealed class SummonTentacleCombatView : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private AnimationClip _emergeClip;
        [SerializeField] private AnimationClip _squeezeClip;
        [SerializeField] private LighthouseImpactParticles _impactParticles;
        [SerializeField, Range(0.01f, 0.99f)] private float _squeezePeakNormalizedTime = 0.3448276f;

        [SerializeField] private AudioClip _squeezeSound;
        [SerializeField] private SoundManager _soundManagerPrefab;
        [SerializeField, Range(0f, 1f)] private float _squeezeVolume = 0.25f;

        private BossTestEnvironment _boss;
        private AudioSource _squeezeSource;
        private uint _soundedSequence;

        private void Awake()
        {
            var child = new GameObject("TentacleSqueezeAudio");
            child.transform.SetParent(transform, false);
            _squeezeSource = child.AddComponent<AudioSource>();
            _squeezeSource.playOnAwake = false;
            _squeezeSource.loop = false;
            _squeezeSource.spatialBlend = 1f;
            _squeezeSource.dopplerLevel = 0f;
            _squeezeSource.rolloffMode = AudioRolloffMode.Linear;
            _squeezeSource.minDistance = 2f;
            _squeezeSource.maxDistance = 40f;
        }

        private void OnDisable()
        {
            StopSqueezeSound();
        }

        public void Bind(BossTestEnvironment boss)
        {
            if (_boss != boss)
            {
                StopSqueezeSound();
                _soundedSequence = 0;
            }
            _boss = boss;
            if (_impactParticles != null)
            {
                _impactParticles.Bind(boss);
            }
        }

        private void LateUpdate()
        {
            if (!NetworkClient.active || !NetworkClient.ready || _boss == null || _animator == null
                || _emergeClip == null || _squeezeClip == null || !_boss.HasSummoned
                || _boss.IsSpecialEnding || _boss.TowerMatch == null
                || _boss.TowerMatch.IsSummonCinematicPlaying || _boss.SqueezeDuration <= 0f)
            {
                StopSqueezeSound();
                return;
            }

            bool hasStopped = _boss.Phase == BossPhase.LighthouseDestroyed || _boss.Phase == BossPhase.BattleStopped;
            if (_boss.TowerMatch.Phase != TowerPhase.FinalBattle && !hasStopped)
            {
                StopSqueezeSound();
                return;
            }

            double now = hasStopped ? _boss.VisualStoppedAt : NetworkTime.time;
            double elapsed = now - _boss.AttackStartedAt;
            UpdateSqueezeSound(elapsed, hasStopped);
            if (_boss.AttackSequence == 0 || elapsed < 0d || elapsed >= _boss.SqueezeDuration)
            {
                _emergeClip.SampleAnimation(_animator.gameObject, _emergeClip.length);
                return;
            }

            // Presentation never applies damage; its peak follows the server's hit fraction.
            float progress = Mathf.Clamp01((float)(elapsed / _boss.SqueezeDuration));
            float hit = Mathf.Clamp(_boss.DamageNormalizedTime, 0.01f, 0.99f);
            float normalizedTime = progress <= hit
                ? Mathf.Lerp(0f, _squeezePeakNormalizedTime, Mathf.SmoothStep(0f, 1f, progress / hit))
                : Mathf.Lerp(_squeezePeakNormalizedTime, 1f, Mathf.SmoothStep(0f, 1f, (progress - hit) / (1f - hit)));
            // The paused Timeline still owns the actors, so combat samples after its evaluation.
            _squeezeClip.SampleAnimation(_animator.gameObject, normalizedTime * _squeezeClip.length);
        }

        private void UpdateSqueezeSound(double elapsed, bool hasStopped)
        {
            if (_squeezeSource == null || _squeezeSound == null)
            {
                return;
            }
            float contractionDuration = _boss.SqueezeDuration
                * Mathf.Clamp(_boss.DamageNormalizedTime, 0.01f, 0.99f);
            if (hasStopped || _boss.AttackSequence == 0 || elapsed < 0d || elapsed >= contractionDuration)
            {
                StopSqueezeSound();
                return;
            }
            if (SoundManager.Instance == null && _soundManagerPrefab != null)
            {
                Instantiate(_soundManagerPrefab);
            }
            SoundManager manager = SoundManager.Instance;
            _squeezeSource.volume = manager != null && !manager.IsMuted
                ? manager.MasterVolume * manager.EffectVolume * _squeezeVolume : 0f;
            _squeezeSource.transform.position = _animator.transform.position;
            if (_soundedSequence == _boss.AttackSequence)
            {
                return;
            }
            _soundedSequence = _boss.AttackSequence;
            _squeezeSource.Stop();
            _squeezeSource.clip = _squeezeSound;
            _squeezeSource.pitch = Mathf.Clamp(_squeezeSound.length / contractionDuration, 0.01f, 3f);
            float offset = (float)elapsed * _squeezeSource.pitch;
            if (offset < _squeezeSound.length)
            {
                // Seek into the contraction instead of replaying its onset after network delay.
                _squeezeSource.time = offset;
                _squeezeSource.Play();
            }
        }

        private void StopSqueezeSound()
        {
            if (_squeezeSource != null)
            {
                _squeezeSource.Stop();
            }
        }
    }
}
