using System;
using Lighthouse.World.Islands.Rules;
using Lighthouse.World.Weather.Rules;
using UnityEngine;
using UnityEngine.Audio;

namespace Lighthouse.World.Weather.View
{
    [CreateAssetMenu(fileName = "WorldAudio", menuName = "TheLightHouse/World/World Audio")]
    public sealed class WorldAudioProfile : ScriptableObject
    {
        public const string ResourceName = "WorldAudio";

        [Serializable]
        private sealed class WeatherLoops
        {
            [SerializeField] private AudioClip _sea;
            [SerializeField] private AudioClip _wind;
            [SerializeField] private AudioClip _rain;
            [SerializeField] private AudioClip _shore;

            public AudioClip Get(int layer)
            {
                switch (layer)
                {
                    case 0: return _sea;
                    case 1: return _wind;
                    case 2: return _rain;
                    default: return _shore;
                }
            }
        }

        [Serializable]
        private sealed class IslandShore
        {
            [SerializeField] private IslandArchetype _archetype;
            [SerializeField, Tooltip("-1 matches every generated island of this archetype.")]
            private int _islandIndex = -1;
            [SerializeField] private AudioClip _calm;
            [SerializeField] private AudioClip _overcast;
            [SerializeField] private AudioClip _rain;
            [SerializeField] private AudioClip _storm;
            [SerializeField] private AudioClip _maelstrom;

            public bool Matches(IslandArchetype archetype, short index)
            {
                return _archetype == archetype && (_islandIndex < 0 || _islandIndex == index);
            }

            public AudioClip Get(WeatherType weather)
            {
                switch (weather)
                {
                    case WeatherType.Calm: return _calm;
                    case WeatherType.Overcast: return _overcast;
                    case WeatherType.Rain: return _rain;
                    case WeatherType.Storm: return _storm;
                    default: return _maelstrom;
                }
            }
        }

        [SerializeField] private WeatherLoops _calm = new WeatherLoops();
        [SerializeField] private WeatherLoops _overcast = new WeatherLoops();
        [SerializeField] private WeatherLoops _rain = new WeatherLoops();
        [SerializeField] private WeatherLoops _storm = new WeatherLoops();
        [SerializeField] private WeatherLoops _maelstrom = new WeatherLoops();
        [SerializeField] private IslandShore[] _islandShores = Array.Empty<IslandShore>();
        [SerializeField] private AudioClip[] _thunder = Array.Empty<AudioClip>();
        [SerializeField] private AudioClip[] _abyssThunder = Array.Empty<AudioClip>();
        [SerializeField] private AudioClip[] _cannonWaterImpact = Array.Empty<AudioClip>();
        [SerializeField] private AudioMixerGroup _ambienceMixer;
        [SerializeField] private AudioMixerGroup _effectsMixer;
        [SerializeField, Range(0f, 1f)] private float _ambienceVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _impactVolume = 0.8f;
        [SerializeField, Min(0.05f)] private float _fadeSeconds = 3f;
        [SerializeField, Min(1f)] private float _shoreFadeDistance = 20f;
        [SerializeField, Min(1f)] private float _impactRange = 80f;

        public AudioMixerGroup AmbienceMixer => _ambienceMixer;
        public AudioMixerGroup EffectsMixer => _effectsMixer;
        public float AmbienceVolume => _ambienceVolume;
        public float ImpactVolume => _impactVolume;
        public float FadeSeconds => Mathf.Max(0.05f, _fadeSeconds);
        public float ShoreFadeDistance => Mathf.Max(1f, _shoreFadeDistance);
        public float ImpactRange => Mathf.Max(1f, _impactRange);

        public AudioClip GetLoop(WeatherType weather, int layer, IslandArchetype island, short index)
        {
            if (layer == 3 && island != null)
            {
                for (int i = _islandShores.Length - 1; i >= 0; i--)
                {
                    if (_islandShores[i] != null && _islandShores[i].Matches(island, index))
                    {
                        AudioClip clip = _islandShores[i].Get(weather);
                        if (clip != null)
                        {
                            return clip;
                        }
                    }
                }
            }
            switch (weather)
            {
                case WeatherType.Calm: return _calm.Get(layer);
                case WeatherType.Overcast: return _overcast.Get(layer);
                case WeatherType.Rain: return _rain.Get(layer);
                case WeatherType.Storm: return _storm.Get(layer);
                default: return _maelstrom.Get(layer);
            }
        }

        public AudioClip PickThunder(bool isAbyss)
        {
            return Pick(isAbyss && _abyssThunder.Length > 0 ? _abyssThunder : _thunder);
        }

        public AudioClip PickImpact()
        {
            return Pick(_cannonWaterImpact);
        }

        private static AudioClip Pick(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
            {
                return null;
            }
            int start = UnityEngine.Random.Range(0, clips.Length);
            for (int i = 0; i < clips.Length; i++)
            {
                AudioClip clip = clips[(start + i) % clips.Length];
                if (clip != null)
                {
                    return clip;
                }
            }
            return null;
        }
    }
}
