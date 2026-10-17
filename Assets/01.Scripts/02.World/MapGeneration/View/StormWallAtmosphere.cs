using System;
using Lighthouse.World.Clock.Rules;
using Lighthouse.World.MapGeneration.Net;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.View
{
    public sealed class StormWallAtmosphere : MonoBehaviour
    {
        [SerializeField] private MapStateNet _map;
        [SerializeField, Range(0, 1)] private int _wallIndex;
        [SerializeField] private StormWallVisualConfig _config;
        [SerializeField] private Transform _fog;
        [SerializeField] private LineRenderer _lightning;

        private IClock _clock;
        private StormWallField _field;
        private Vector3[] _points;
        private long _lastSlot = long.MinValue;

        private void Awake()
        {
            if (_map == null || _config == null || !_config.IsValid
                || _fog == null || _fog.gameObject == gameObject
                || _lightning == null || _wallIndex < 0 || _wallIndex > 1)
            {
                Debug.LogError($"[{nameof(StormWallAtmosphere)}] Effect references or settings are invalid.");
                enabled = false;
                return;
            }

            _points = new Vector3[_config.LightningPoints];
            _lightning.useWorldSpace = true;
            _lightning.loop = false;
            _lightning.positionCount = _points.Length;
            HideEffects();
        }

        private void Start()
        {
            if (!WorldServices.TryGet<IClock>(out _clock))
            {
                Debug.LogError($"[{nameof(StormWallAtmosphere)}] Shared clock is unavailable.");
                enabled = false;
            }
        }

        private void Update()
        {
            if (_map == null || _clock == null)
            {
                HideEffects();
                return;
            }

            var fields = _map.StormWalls;
            if (_wallIndex >= fields.Count)
            {
                _field = null;
                HideEffects();
                return;
            }

            StormWallField field = fields[_wallIndex];
            if (!ReferenceEquals(field, _field))
            {
                _field = field;
                _lastSlot = long.MinValue;

                float scale = field.Radius / _config.FogReferenceRadius;
                _fog.localPosition = new Vector3(0f, _config.FogHeight, 0f);
                _fog.localRotation = Quaternion.identity;
                _fog.localScale = new Vector3(scale, _config.FogVerticalScale, scale);
            }

            if (!field.IsActive)
            {
                HideEffects();
                return;
            }

            if (!_fog.gameObject.activeSelf)
            {
                _fog.gameObject.SetActive(true);
            }

            double now = _clock.Now;
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0d)
            {
                _lightning.enabled = false;
                return;
            }

            long slot = (long)Math.Floor(now / _config.LightningInterval);
            double phase = now - slot * (double)_config.LightningInterval;

            if (slot != _lastSlot)
            {
                _lastSlot = slot;
                StormLightningPattern.Fill(
                    _points, field.Radius, field.Width,
                    _config.LightningHeight, _config.LightningJitter,
                    _map.Info.Seed, slot, _wallIndex);
                _lightning.SetPositions(_points);
            }

            bool isFlashing = phase < _config.LightningDuration;
            if (_lightning.enabled != isFlashing)
            {
                _lightning.enabled = isFlashing;
            }
        }

        private void OnDisable()
        {
            HideEffects();
        }

        private void HideEffects()
        {
            if (_fog != null && _fog.gameObject.activeSelf)
            {
                _fog.gameObject.SetActive(false);
            }

            if (_lightning != null)
            {
                _lightning.enabled = false;
            }

            _lastSlot = long.MinValue;
        }
    }
}
