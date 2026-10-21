using System;
using System.Collections;
using Lighthouse.World.MapGeneration.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Net
{
    public sealed class LocalMapBuildSession
    {
        private readonly MonoBehaviour _source;
        private readonly ILocalMapBuilder _builder;
        private int _generation;

        public LocalMapBuildSession(MonoBehaviour source, ILocalMapBuilder builder)
        {
            _source = source;
            _builder = builder;
        }

        public IEnumerator Run(
            MapGenerationConfig config,
            MapLayout layout,
            MapSeedInfo info,
            Action completed)
        {
            int generation = _generation;
            bool isStarted = false;
            bool isComplete = false;

            yield return null;

            while (!isComplete)
            {
                if (generation != _generation)
                {
                    yield break;
                }

                if (_source == null || !_source.isActiveAndEnabled)
                {
                    Debug.LogError($"[{nameof(LocalMapBuildSession)}] Local builder is unavailable.");
                    Cancel();
                    yield break;
                }

                bool hasFailed = false;
                try
                {
                    if (!isStarted)
                    {
                        _builder.Begin(config, layout, info);
                        isStarted = true;
                    }

                    isComplete = _builder.Step();
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[{nameof(LocalMapBuildSession)}] Build failed: {exception}");
                    Cancel();
                    hasFailed = true;
                }

                if (hasFailed)
                {
                    yield break;
                }

                yield return null;
            }

            if (generation != _generation)
            {
                yield break;
            }

            if (_source == null || !_source.isActiveAndEnabled)
            {
                Cancel();
                yield break;
            }

            try
            {
                completed();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{nameof(LocalMapBuildSession)}] Completion failed: {exception}");
                Cancel();
            }
        }

        public void Cancel()
        {
            _generation++;

            if (_source == null)
            {
                return;
            }

            try
            {
                _builder.Cancel();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{nameof(LocalMapBuildSession)}] Cleanup failed: {exception}");
            }
        }
    }
}
