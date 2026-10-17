using System.Collections.Generic;
using Lighthouse.World.MapGeneration.Net;
using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.View
{
    public sealed class StormWallView : MonoBehaviour
    {
        [SerializeField] private MapStateNet _map;
        [SerializeField] private LineRenderer[] _rings;
        [SerializeField, Min(16)] private int _segments = 160;
        [SerializeField] private float _height = 0.5f;

        private IReadOnlyList<StormWallField> _fields;

        private void Awake()
        {
            if (_map == null || _rings == null || _rings.Length != 2
                || _rings[0] == null || _rings[1] == null)
            {
                Debug.LogError($"[{nameof(StormWallView)}] Map and two ring renderers are required.");
                enabled = false;
                return;
            }

            _segments = Mathf.Max(16, _segments);
            for (int i = 0; i < _rings.Length; i++)
            {
                _rings[i].loop = true;
                _rings[i].useWorldSpace = true;
                _rings[i].positionCount = _segments;
                _rings[i].enabled = false;
            }
        }

        private void Update()
        {
            if (_map == null)
            {
                HideRings();
                return;
            }

            IReadOnlyList<StormWallField> fields = _map.StormWalls;
            if (!ReferenceEquals(fields, _fields))
            {
                _fields = fields;
                BuildRings();
            }

            for (int i = 0; i < _rings.Length; i++)
            {
                bool isVisible = _fields != null
                    && i < _fields.Count
                    && _fields[i].IsActive;

                if (_rings[i].enabled != isVisible)
                {
                    _rings[i].enabled = isVisible;
                }
            }
        }

        private void OnDisable()
        {
            HideRings();
        }

        private void BuildRings()
        {
            for (int i = 0; i < _rings.Length && i < _fields.Count; i++)
            {
                float radius = _fields[i].Radius;
                for (int j = 0; j < _segments; j++)
                {
                    float angle = j * Mathf.PI * 2f / _segments;
                    _rings[i].SetPosition(j, new Vector3(
                        Mathf.Cos(angle) * radius,
                        _height,
                        Mathf.Sin(angle) * radius));
                }
            }
        }

        private void HideRings()
        {
            if (_rings == null)
            {
                return;
            }

            for (int i = 0; i < _rings.Length; i++)
            {
                if (_rings[i] != null)
                {
                    _rings[i].enabled = false;
                }
            }
        }
    }
}
