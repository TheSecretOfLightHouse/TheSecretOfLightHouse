using System.Collections;
using UnityEngine;

namespace Lighthouse.Monsters.AI.View
{
    [DisallowMultipleComponent]
    public sealed class MonsterHitShake : MonoBehaviour
    {
        [SerializeField] private Transform[] _visualRoots;
        [SerializeField, Min(0.01f)] private float _duration = 0.3f;
        [SerializeField, Range(0f, 15f)] private float _angle = 4f;

        private Transform _visualPivot;
        private Coroutine _routine;

        private void Awake()
        {
            if (_visualRoots == null || _visualRoots.Length == 0)
            {
                return;
            }
            foreach (Transform root in _visualRoots)
            {
                if (root == null || root.parent != transform || root.GetComponentInChildren<Collider>(true) != null)
                {
                    Debug.LogWarning($"[{nameof(MonsterHitShake)}] Visual roots must be direct children without colliders.", this);
                    return;
                }
            }

            var pivot = new GameObject("Hit Shake Visual Pivot");
            _visualPivot = pivot.transform;
            _visualPivot.SetParent(transform, false);
            foreach (Transform root in _visualRoots)
            {
                root.SetParent(_visualPivot, true);
            }
        }

        private void OnDisable()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            if (_visualPivot != null)
            {
                _visualPivot.localRotation = Quaternion.identity;
            }
        }

        public void PlayHit()
        {
            if (_visualPivot == null || !isActiveAndEnabled)
            {
                return;
            }
            if (_routine != null)
            {
                StopCoroutine(_routine);
            }
            _routine = StartCoroutine(Shake());
        }

        public Vector3 GetUnshakenPosition(Transform target)
        {
            if (target == null)
            {
                return transform.position;
            }
            if (_visualPivot == null || !target.IsChildOf(_visualPivot))
            {
                return target.position;
            }
            return transform.TransformPoint(_visualPivot.InverseTransformPoint(target.position));
        }

        private IEnumerator Shake()
        {
            float elapsed = 0f;
            while (elapsed < _duration)
            {
                float progress = elapsed / _duration;
                float angle = _angle * Mathf.Sin(progress * Mathf.PI * 2f) * (1f - progress);
                _visualPivot.localRotation = Quaternion.Euler(0f, 0f, angle);
                elapsed += Time.deltaTime;
                yield return null;
            }
            _visualPivot.localRotation = Quaternion.identity;
            _routine = null;
        }
    }
}
