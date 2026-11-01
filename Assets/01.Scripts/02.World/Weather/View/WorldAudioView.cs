using Lighthouse.World.Islands.Rules;
using Lighthouse.World.Weather.Rules;
using UnityEngine;

namespace Lighthouse.World.Weather.View
{
    [DisallowMultipleComponent]
    public sealed class WorldAudioView : MonoBehaviour
    {
        private const int WeatherCount = 5;
        private const int LayerCount = 4;
        private const int ImpactCapacity = 8;
        private static WorldAudioView _active;
        private readonly AudioSource[] _loops = new AudioSource[WeatherCount * LayerCount * 2];
        private readonly AudioSource[] _impacts = new AudioSource[ImpactCapacity];
        private WorldAudioProfile _profile;
        private int _nextImpact;
        private bool _isInitialized;
        private bool _hasObserver;

        private void OnEnable()
        {
            if (_active != null && _active != this)
            {
                enabled = false;
                return;
            }
            _active = this;
            if (_isInitialized)
            {
                return;
            }
            _profile = Resources.Load<WorldAudioProfile>(WorldAudioProfile.ResourceName);
            if (_profile == null)
            {
                return;
            }
            for (int i = 0; i < _loops.Length; i++)
            {
                _loops[i] = CreateSource("WorldAmbience", true);
            }
            for (int i = 0; i < _impacts.Length; i++)
            {
                _impacts[i] = CreateSource("CannonWaterImpact", false);
                _impacts[i].maxDistance = _profile.ImpactRange;
            }
            _isInitialized = true;
        }

        private void OnDisable()
        {
            Clear();
            if (_active == this)
            {
                _active = null;
            }
        }

        public void RenderWeather(Camera observer, WeatherType weather, float rainExposure)
        {
            if (!_isInitialized || !isActiveAndEnabled || _active != this)
            {
                return;
            }
            _hasObserver = true;
            float shore = WorldIslandAudioZone.GetNearest(observer.transform.position,
                _profile.ShoreFadeDistance, out IslandArchetype island, out short index);
            float step = Time.deltaTime * _profile.AmbienceVolume / _profile.FadeSeconds;
            for (int w = 0; w < WeatherCount; w++)
            {
                for (int layer = 0; layer < LayerCount; layer++)
                {
                    AudioClip clip = _profile.GetLoop((WeatherType)w, layer, island, index);
                    int offset = (w * LayerCount + layer) * 2;
                    AudioSource first = _loops[offset];
                    AudioSource second = _loops[offset + 1];
                    // Two voices preserve the outgoing shore loop when an island override changes.
                    AudioSource incoming = first.clip == clip ? first : second.clip == clip ? second :
                        first.volume <= second.volume ? first : second;
                    if (incoming.clip != clip)
                    {
                        incoming.Stop();
                        incoming.clip = clip;
                        incoming.volume = 0f;
                    }
                    float target = w == (int)weather && clip != null
                        ? _profile.AmbienceVolume * (layer == 3 ? shore : 1f) : 0f;
                    if (layer == 2)
                    {
                        target *= Mathf.Clamp01(rainExposure);
                    }
                    Fade(first, first == incoming ? target : 0f, step);
                    Fade(second, second == incoming ? target : 0f, step);
                }
            }
        }

        public void Clear()
        {
            _hasObserver = false;
            for (int i = 0; i < _loops.Length; i++)
            {
                if (_loops[i] != null)
                {
                    _loops[i].Stop();
                    _loops[i].volume = 0f;
                }
            }
            for (int i = 0; i < _impacts.Length; i++)
            {
                if (_impacts[i] != null)
                {
                    _impacts[i].Stop();
                }
            }
        }

        public static void PlayCannonWaterImpact(Vector3 position)
        {
            if (_active == null || !_active.isActiveAndEnabled || !_active._isInitialized || !_active._hasObserver)
            {
                return;
            }
            AudioClip clip = _active._profile.PickImpact();
            if (clip == null)
            {
                return;
            }
            AudioSource source = _active._impacts[_active._nextImpact++ % ImpactCapacity];
            source.Stop();
            source.transform.position = position;
            source.clip = clip;
            source.volume = _active._profile.ImpactVolume;
            source.Play();
        }

        private AudioSource CreateSource(string sourceName, bool isLoop)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = isLoop;
            source.spatialBlend = isLoop ? 0f : 1f;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.volume = 0f;
            source.outputAudioMixerGroup = isLoop ? _profile.AmbienceMixer : _profile.EffectsMixer;
            return source;
        }

        private static void Fade(AudioSource source, float target, float step)
        {
            source.volume = Mathf.MoveTowards(source.volume, target, step);
            if (source.volume > 0f && source.clip != null && !source.isPlaying)
            {
                source.Play();
            }
            else if (source.volume <= 0f && source.isPlaying)
            {
                source.Stop();
            }
        }
    }
}
