using System;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Weather.Rules
{
    [CreateAssetMenu(fileName = "WeatherCatalog", menuName = "TheLightHouse/World/WeatherCatalog")]
    public class WeatherCatalog : ScriptableObject
    {
        [SerializeField] private WaveSlotLayout _layout;
        [SerializeField] private Entry[] _entries;

        public WaveSlotLayout Layout => _layout;

        public bool TryGetProfile(WeatherType type, out WeatherProfile profile)
        {
            if (_entries != null)
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    if (_entries[i].Type == type && _entries[i].Profile != null)
                    {
                        profile = _entries[i].Profile;
                        return true;
                    }
                }
            }

            profile = null;
            return false;
        }

        public bool Validate(out string error)
        {
            if (_layout == null || _layout.Count == 0)
            {
                error = "layout is missing or empty";
                return false;
            }

            if (_entries == null || _entries.Length == 0)
            {
                error = "no entries";
                return false;
            }

            for (int i = 0; i < _entries.Length; i++)
            {
                WeatherProfile profile = _entries[i].Profile;

                if (profile == null)
                {
                    error = $"entry {i} ({_entries[i].Type}) has no profile";
                    return false;
                }

                if (profile.LevelCount != _layout.Count)
                {
                    error = $"{profile.name} has {profile.LevelCount} levels, layout has {_layout.Count} slots";
                    return false;
                }

                for (int j = 0; j < i; j++)
                {
                    if (_entries[j].Type == _entries[i].Type)
                    {
                        error = $"duplicate type {_entries[i].Type}";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        private void OnValidate()
        {
            if (_layout != null && _entries != null && !Validate(out string error))
            {
                Debug.LogWarning($"[{nameof(WeatherCatalog)}] {name}: {error}", this);
            }
        }

        [Serializable]
        private struct Entry
        {
            [SerializeField] private WeatherType _type;
            [SerializeField] private WeatherProfile _profile;

            public WeatherType Type => _type;
            public WeatherProfile Profile => _profile;
        }
    }
}
