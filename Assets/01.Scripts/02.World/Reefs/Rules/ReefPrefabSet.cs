using System;
using UnityEngine;

namespace Lighthouse.World.Reefs.Rules
{
    [CreateAssetMenu(fileName = "ReefPrefabSet", menuName = "TheLightHouse/World/ReefPrefabSet")]
    public sealed class ReefPrefabSet : ScriptableObject
    {
        [SerializeField] private GameObject[] _prefabs = Array.Empty<GameObject>();

        public int Count => _prefabs != null ? _prefabs.Length : 0;

        public bool IsValid
        {
            get
            {
                if (Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < _prefabs.Length; i++)
                {
                    if (_prefabs[i] == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public bool TryGet(int index, out GameObject prefab)
        {
            if (index < 0 || index >= Count)
            {
                prefab = null;
                return false;
            }

            prefab = _prefabs[index];
            return prefab != null;
        }
    }
}
