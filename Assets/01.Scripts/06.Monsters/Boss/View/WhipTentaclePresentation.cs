using Mirror;
using Lighthouse.Systems.Audio;
using UnityEngine;

namespace Lighthouse.Monsters.Boss
{
    [DefaultExecutionOrder(20001)]
    [DisallowMultipleComponent]
    public sealed class WhipTentaclePresentation : MonoBehaviour
    {
        [SerializeField] private WhipTentacleSettings _settings;
        [SerializeField] private Animator[] _animators = new Animator[0];
        [SerializeField] private BoxCollider[] _hitBoxes = new BoxCollider[0];
        [SerializeField] private AudioSource _emergeAudio;
        [SerializeField] private AudioClip _impactSound;
        [SerializeField] private SoundManager _soundManagerPrefab;
        [SerializeField, Range(0f, 1f)] private float _soundVolume = 0.7f;

        private WhipTentacleBattleNet _battle;

        public int Count => _animators.Length;
        public WhipTentacleSettings Settings => _settings;
        public bool IsReady
        {
            get
            {
                if (_settings == null || !_settings.IsValid || Count == 0 || Count != _hitBoxes.Length)
                {
                    return false;
                }
                for (int i = 0; i < Count; i++)
                {
                    if (_animators[i] == null || _hitBoxes[i] == null)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public BoxCollider GetHitBox(int index)
        {
            return _hitBoxes[index];
        }

        public void Bind(WhipTentacleBattleNet battle)
        {
            _battle = battle;
            if (_battle != null)
            {
                _battle.BindPresentation(this);
            }
        }

        private void LateUpdate()
        {
            if (NetworkClient.active && NetworkClient.ready)
            {
                EnsureSoundManager();
                SoundManager manager = SoundManager.Instance;
                if (_emergeAudio != null)
                {
                    _emergeAudio.volume = manager != null && !manager.IsMuted
                        ? manager.MasterVolume * manager.EffectVolume * _soundVolume : 0f;
                }
            }
            if (!NetworkClient.active || !NetworkClient.ready || _battle == null || !_battle.HasStarted
                || _settings == null || _battle.AttackCount != Count)
            {
                return;
            }
            double now = _battle.IsStopped ? _battle.StoppedAt : NetworkTime.time;
            for (int i = 0; i < Count; i++)
            {
                WhipTentacleAttackState state = _battle.GetAttack(i);
                double elapsed = (now - state.StartedAt) * state.Speed;
                if (state.Sequence > 0 && elapsed >= 0d && elapsed < _settings.AttackClip.length)
                {
                    _settings.AttackClip.SampleAnimation(_animators[i].gameObject, (float)elapsed);
                }
                else
                {
                    float time = (float)((now - _battle.CombatStartedAt) % _settings.IdleClip.length);
                    _settings.IdleClip.SampleAnimation(_animators[i].gameObject, Mathf.Max(0f, time));
                }
            }
        }
        public void PlayImpactSound(Vector3 position, double startedAt)
        {
            if (!isActiveAndEnabled || _impactSound == null
                || NetworkTime.time - startedAt >= _impactSound.length)
            {
                return;
            }
            EnsureSoundManager();
            SoundManager manager = SoundManager.Instance;
            if (manager != null)
            {
                manager.PlayEffectAt(_impactSound, position, _soundVolume);
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
