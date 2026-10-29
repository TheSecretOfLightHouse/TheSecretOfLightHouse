using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Lighthouse.Systems.Audio
{
    [DisallowMultipleComponent]
    public sealed class SoundManager : MonoBehaviour
    {
        private const string MasterVolumeKey = "Lighthouse.Audio.MasterVolume";
        private const string MusicVolumeKey = "BGMVolume";
        private const string EffectVolumeKey = "SFXVolume";
        private const string MutedKey = "Lighthouse.Audio.Muted";

        public static SoundManager Instance { get; private set; }

        [SerializeField] private BgmEntry[] _bgmClips = Array.Empty<BgmEntry>();
        [SerializeField] private SfxEntry[] _sfxClips = Array.Empty<SfxEntry>();
        [SerializeField, Min(0f)] private float _fadeTime = 0.7f;
        [SerializeField, Min(1)] private int _effectVoiceCount = 16;
        [SerializeField, Min(0.01f)] private float _minDistance = 2f;
        [SerializeField, Min(0.02f)] private float _maxDistance = 40f;
        [SerializeField] private AudioMixerGroup _musicOutput;
        [SerializeField] private AudioMixerGroup _effectOutput;
        [SerializeField, Range(0f, 1f)] private float _masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float _musicVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float _effectVolume = 1f;

        private AudioSource _music;
        private AudioSource[] _effects;
        private float[] _effectGains;
        private Coroutine _bgmCoroutine;
        private float _fadeGain = 1f;

        public float MasterVolume => _masterVolume;
        public float MusicVolume => _musicVolume;
        public float EffectVolume => _effectVolume;
        public bool IsMuted { get; private set; }
        public bool IsSoundOn => !IsMuted && _masterVolume > 0f && (_musicVolume > 0f || _effectVolume > 0f);

        [Serializable]
        private struct BgmEntry
        {
            [SerializeField] private BgmType _type;
            [SerializeField] private AudioClip _clip;

            public BgmType Type => _type;
            public AudioClip Clip => _clip;
        }

        [Serializable]
        private struct SfxEntry
        {
            [SerializeField] private SfxType _type;
            [SerializeField] private AudioClip _clip;

            public SfxType Type => _type;
            public AudioClip Clip => _clip;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            // Keep pooled world-space voices independent from moving scene parents.
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            _masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, _masterVolume));
            _musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, _musicVolume));
            _effectVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(EffectVolumeKey, _effectVolume));
            IsMuted = PlayerPrefs.GetInt(MutedKey, 0) != 0;
            _music = CreateSource("Music", _musicOutput);
            _effects = new AudioSource[Mathf.Max(1, _effectVoiceCount)];
            _effectGains = new float[_effects.Length];
            for (int i = 0; i < _effects.Length; i++)
            {
                _effects[i] = CreateSource($"Effect {i}", _effectOutput);
            }
            ApplyVolumes();
        }

        private void OnDisable()
        {
            CancelFade();
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused && Instance == this)
            {
                SaveSettings();
            }
        }

        private void OnApplicationQuit()
        {
            if (Instance == this)
            {
                SaveSettings();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool PlayBgm(BgmType type, bool fade = true)
        {
            foreach (BgmEntry entry in _bgmClips)
            {
                if (entry.Type == type)
                {
                    return PlayMusicInternal(entry.Clip, true, fade);
                }
            }
            return false;
        }

        public bool PlaySfx(SfxType type, float volume = 1f)
        {
            return PlayEffect(GetSfxClip(type), volume);
        }

        public bool PlaySfxAt(SfxType type, Vector3 position, float volume = 1f)
        {
            return PlayEffectAt(GetSfxClip(type), position, volume);
        }

        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            PlayMusicInternal(clip, loop, false);
        }

        public void StopMusic()
        {
            CancelFade();
            if (_music != null)
            {
                _music.Stop();
                _music.clip = null;
            }
        }

        public bool PlayEffect(AudioClip clip, float volume = 1f)
        {
            return PlayEffectInternal(clip, Vector3.zero, false, volume);
        }

        public bool PlayEffectAt(AudioClip clip, Vector3 position, float volume = 1f)
        {
            return PlayEffectInternal(clip, position, true, volume);
        }

        public void StopEffects()
        {
            if (_effects == null)
            {
                return;
            }
            foreach (AudioSource source in _effects)
            {
                source.Stop();
                source.clip = null;
            }
        }

        public void ToggleSound()
        {
            SetMuted(!IsMuted);
        }

        public void SetMuted(bool isMuted)
        {
            // Preserve volume preferences while muted, including slider changes.
            IsMuted = isMuted;
            PlayerPrefs.SetInt(MutedKey, isMuted ? 1 : 0);
            ApplyVolumes();
        }

        public void SetMasterVolume(float volume)
        {
            _masterVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(MasterVolumeKey, _masterVolume);
            ApplyVolumes();
        }

        public void SetMusicVolume(float volume)
        {
            _musicVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(MusicVolumeKey, _musicVolume);
            ApplyVolumes();
        }

        public void SetEffectVolume(float volume)
        {
            _effectVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(EffectVolumeKey, _effectVolume);
            ApplyVolumes();
        }

        public void SaveSettings()
        {
            PlayerPrefs.Save();
        }

        private bool PlayMusicInternal(AudioClip clip, bool loop, bool fade)
        {
            if (_music == null || clip == null || !isActiveAndEnabled)
            {
                return false;
            }
            if (_bgmCoroutine == null && _music.clip == clip && _music.isPlaying)
            {
                _music.loop = loop;
                return true;
            }
            float startGain = _fadeGain;
            CancelFade();
            if (fade && _fadeTime > 0f)
            {
                _fadeGain = startGain;
                _bgmCoroutine = StartCoroutine(ChangeMusic(clip, loop));
            }
            else
            {
                _music.clip = clip;
                _music.loop = loop;
                ApplyVolumes();
                _music.Play();
            }
            return true;
        }

        private IEnumerator ChangeMusic(AudioClip clip, bool loop)
        {
            float duration = _fadeTime;
            float startGain = _music.isPlaying ? _fadeGain : 0f;
            if (startGain > 0f)
            {
                for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    _fadeGain = Mathf.Lerp(startGain, 0f, elapsed / duration);
                    ApplyMusicVolume();
                    yield return null;
                }
            }
            _music.Stop();
            _fadeGain = 0f;
            ApplyMusicVolume();
            _music.clip = clip;
            _music.loop = loop;
            _music.Play();
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                _fadeGain = Mathf.Clamp01(elapsed / duration);
                ApplyMusicVolume();
                yield return null;
            }
            _fadeGain = 1f;
            ApplyMusicVolume();
            _bgmCoroutine = null;
        }

        private void CancelFade()
        {
            if (_bgmCoroutine != null)
            {
                StopCoroutine(_bgmCoroutine);
                _bgmCoroutine = null;
            }
            _fadeGain = 1f;
            ApplyMusicVolume();
        }

        private AudioClip GetSfxClip(SfxType type)
        {
            foreach (SfxEntry entry in _sfxClips)
            {
                if (entry.Type == type)
                {
                    return entry.Clip;
                }
            }
            return null;
        }

        private bool PlayEffectInternal(AudioClip clip, Vector3 position, bool isSpatial, float volume)
        {
            if (clip == null || _effects == null || !isActiveAndEnabled)
            {
                return false;
            }
            for (int i = 0; i < _effects.Length; i++)
            {
                AudioSource source = _effects[i];
                if (source.isPlaying)
                {
                    continue;
                }
                source.transform.position = position;
                source.spatialBlend = isSpatial ? 1f : 0f;
                source.clip = clip;
                _effectGains[i] = Mathf.Clamp01(volume);
                source.volume = IsMuted ? 0f : _masterVolume * _effectVolume * _effectGains[i];
                source.Play();
                return true;
            }
            // Preserve playing sounds when all voices are occupied.
            return false;
        }

        private AudioSource CreateSource(string objectName, AudioMixerGroup output)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.outputAudioMixerGroup = output;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = Mathf.Max(0.01f, _minDistance);
            source.maxDistance = Mathf.Max(source.minDistance + 0.01f, _maxDistance);
            return source;
        }

        private void ApplyMusicVolume()
        {
            if (_music != null)
            {
                _music.volume = IsMuted ? 0f : _masterVolume * _musicVolume * _fadeGain;
            }
        }

        private void ApplyVolumes()
        {
            ApplyMusicVolume();
            if (_effects == null)
            {
                return;
            }
            for (int i = 0; i < _effects.Length; i++)
            {
                _effects[i].volume = IsMuted ? 0f : _masterVolume * _effectVolume * _effectGains[i];
            }
        }
    }
}
