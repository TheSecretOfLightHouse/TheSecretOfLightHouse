using System;
using Lighthouse.Monsters.Spawning.Rules;
using UnityEngine;

namespace Lighthouse.World.Spawning.Server
{
    [CreateAssetMenu(fileName = "MonsterSpawnBindings", menuName = "TheLightHouse/World/MonsterSpawnBindings")]
    public sealed class MonsterSpawnBindings : ScriptableObject
    {
        [SerializeField] private MonsterSpawnBinding[] _bindings = Array.Empty<MonsterSpawnBinding>();

        public bool TryGet(int profileId, out MonsterSpawnerData data)
        {
            for (int i = 0; i < _bindings.Length; i++)
            {
                MonsterSpawnBinding binding = _bindings[i];
                if (binding != null && binding.Profile != null && binding.Data != null
                    && binding.Profile.Id == profileId)
                {
                    data = binding.Data;
                    return true;
                }
            }

            data = null;
            return false;
        }

        public bool TryValidate(out string error)
        {
            for (int i = 0; i < _bindings.Length; i++)
            {
                MonsterSpawnBinding binding = _bindings[i];
                if (binding == null || binding.Profile == null || binding.Data == null)
                {
                    error = $"Binding {i}: profile and spawner data are required.";
                    return false;
                }

                for (int previous = 0; previous < i; previous++)
                {
                    if (_bindings[previous].Profile != null && _bindings[previous].Profile.Id == binding.Profile.Id)
                    {
                        error = $"Binding {i}: duplicate profile id {binding.Profile.Id}.";
                        return false;
                    }
                }

                if (!binding.Data.TryValidate(out string dataError))
                {
                    error = $"Binding {i}: {dataError}";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
