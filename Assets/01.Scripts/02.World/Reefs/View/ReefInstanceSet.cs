using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Reefs.View
{
    [DisallowMultipleComponent]
    public sealed class ReefInstanceSet : MonoBehaviour
    {
        private readonly List<GameObject> _instances = new List<GameObject>();

        public int Count => _instances.Count;

        private void OnDestroy()
        {
            Clear();
        }

        public void Create(GameObject prefab, Vector3 position, float yawDegrees, string instanceName)
        {
            if (prefab == null)
            {
                throw new System.ArgumentNullException(nameof(prefab));
            }

            GameObject instance = Instantiate(
                prefab,
                position,
                Quaternion.Euler(0f, yawDegrees, 0f),
                transform);

            instance.name = instanceName;
            _instances.Add(instance);
        }

        public bool TryGetTransform(int index, out Transform instanceTransform)
        {
            if (index < 0 || index >= _instances.Count || _instances[index] == null)
            {
                instanceTransform = null;
                return false;
            }

            instanceTransform = _instances[index].transform;
            return true;
        }

        public void Clear()
        {
            for (int i = 0; i < _instances.Count; i++)
            {
                if (_instances[i] != null)
                {
                    _instances[i].SetActive(false);
                    Destroy(_instances[i]);
                }
            }

            _instances.Clear();
        }
    }
}
