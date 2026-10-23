using System;
using Lighthouse.World.MapGeneration.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Net
{
    [DisallowMultipleComponent]
    public sealed class CompositeLocalMapBuilder : MonoBehaviour, ILocalMapBuilder
    {
        [SerializeField] private MonoBehaviour[] _sources = Array.Empty<MonoBehaviour>();

        private ILocalMapBuilder[] _builders = Array.Empty<ILocalMapBuilder>();
        private MapGenerationConfig _config;
        private MapLayout _layout;
        private MapSeedInfo _info;
        private int _current;
        private bool _isBuilding;

        private void Awake()
        {
            _builders = new ILocalMapBuilder[_sources.Length];
            for (int i = 0; i < _sources.Length; i++)
            {
                if (!(_sources[i] is ILocalMapBuilder builder))
                {
                    Debug.LogError($"[{nameof(CompositeLocalMapBuilder)}] Source {i} must implement ILocalMapBuilder.");
                    enabled = false;
                    return;
                }

                _builders[i] = builder;
            }

            if (_builders.Length == 0)
            {
                Debug.LogError($"[{nameof(CompositeLocalMapBuilder)}] At least one builder is required.");
                enabled = false;
            }
        }

        private void OnDisable()
        {
            Cancel();
        }

        public void Begin(MapGenerationConfig config, MapLayout layout, MapSeedInfo info)
        {
            Cancel();

            if (!isActiveAndEnabled)
            {
                throw new InvalidOperationException("Composite builder is unavailable.");
            }

            _config = config;
            _layout = layout;
            _info = info;
            _current = 0;
            _isBuilding = true;
            _builders[0].Begin(config, layout, info);
        }

        public bool Step()
        {
            if (!_isBuilding || !isActiveAndEnabled)
            {
                throw new InvalidOperationException("Composite build is not active.");
            }

            if (!_builders[_current].Step())
            {
                return false;
            }

            _current++;
            if (_current >= _builders.Length)
            {
                _isBuilding = false;
                return true;
            }

            _builders[_current].Begin(_config, _layout, _info);
            return false;
        }

        public void Cancel()
        {
            _isBuilding = false;
            _current = 0;
            _config = null;
            _layout = null;

            for (int i = 0; i < _builders.Length; i++)
            {
                _builders[i].Cancel();
            }
        }
    }
}
