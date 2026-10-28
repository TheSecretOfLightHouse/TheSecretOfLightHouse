using System.Collections.Generic;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public class BuoyancyTestSpawner : MonoBehaviour
    {
        private const float SurfaceFollow = 1f;
        private const float SubmergedFollow = 0.3f;
        private const float VerticalDrag = 5f;
        private const float HorizontalDrag = 0.3f;

        private static readonly float[] Ratios = { 0.6f, 2f, 5f };

        private static readonly PrimitiveType[] Shapes =
        {
            PrimitiveType.Sphere,
            PrimitiveType.Cube,
            PrimitiveType.Capsule
        };

        [SerializeField] private BuoyancyTestProbe _probe;
        [SerializeField] private bool _spawnOnStart = true;
        [SerializeField, Min(0.1f)] private float _mass = 10f;
        [SerializeField, Min(1f)] private float _spacing = 4f;
        [SerializeField] private float _dropHeight = 1.5f;

        private readonly List<BuoyancyObject> _spawned = new List<BuoyancyObject>();

        private bool _isCycled;

        private void Start()
        {
            if (_spawnOnStart)
            {
                Spawn();
            }
        }

        [ContextMenu("Spawn")]
        public void Spawn()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Clear();

            for (int row = 0; row < Shapes.Length; row++)
            {
                for (int column = 0; column < Ratios.Length; column++)
                {
                    Vector3 position = transform.position + new Vector3(
                        (column - 1) * _spacing,
                        _dropHeight,
                        (row - 1) * _spacing);

                    float ratio = Ratios[column];
                    BuoyancyObject spawned = Create(Shapes[row], ratio, position);

                    _spawned.Add(spawned);

                    if (_probe != null)
                    {
                        _probe.Track(spawned, ratio >= 1f ? 1f / ratio : float.NaN);
                    }
                }
            }
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Destroy(_spawned[i].gameObject);
                }
            }

            _spawned.Clear();
            _isCycled = false;

            if (_probe != null)
            {
                _probe.Clear();
            }
        }

        [ContextMenu("Cycle Kinematic")]
        public void CycleKinematic()
        {
            _isCycled = !_isCycled;

            for (int i = 0; i < _spawned.Count; i += 2)
            {
                if (_spawned[i] != null
                    && _spawned[i].TryGetComponent(out Rigidbody body))
                {
                    body.isKinematic = _isCycled;
                }
            }
        }

        public void SetProbe(BuoyancyTestProbe probe)
        {
            _probe = probe;
        }

        private BuoyancyObject Create(PrimitiveType shape, float ratio, Vector3 position)
        {
            GameObject instance = GameObject.CreatePrimitive(shape);

            instance.SetActive(false);
            instance.name = $"Test_{shape}_{ratio:F1}";
            instance.transform.SetParent(transform, true);
            instance.transform.position = position;

            Rigidbody body = instance.AddComponent<Rigidbody>();
            body.mass = _mass;

            BuoyancyObject buoyant = instance.AddComponent<BuoyancyObject>();

            buoyant.Configure(
                new BuoyancyProfile(
                    ratio,
                    0f,
                    SurfaceFollow,
                    SubmergedFollow,
                    0f,
                    VerticalDrag,
                    HorizontalDrag),
                SamplerPool.Drift);

            instance.SetActive(true);

            return buoyant;
        }
    }
}
