using System;
using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    [Serializable]
    public struct OceanZoneOptics
    {
        [SerializeField] private Color _tint;
        [SerializeField, Range(0f, 1.5f)] private float _clarityScale;
        [SerializeField, Range(0f, 2f)] private float _skyScale;

        public Color Tint => _tint;
        public float ClarityScale => Mathf.Clamp(_clarityScale, 0f, 1.5f);
        public float SkyScale => Mathf.Clamp(_skyScale, 0f, 2f);

        public OceanZoneOptics(Color tint, float clarityScale, float skyScale)
        {
            _tint = tint;
            _clarityScale = Mathf.Clamp(clarityScale, 0f, 1.5f);
            _skyScale = Mathf.Clamp(skyScale, 0f, 2f);
        }
    }
}
