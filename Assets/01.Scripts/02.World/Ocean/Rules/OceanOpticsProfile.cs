using UnityEngine;

namespace Lighthouse.World.Ocean.Rules
{
    [CreateAssetMenu(
        fileName = "OceanOpticsProfile",
        menuName = "TheLightHouse/World/OceanOpticsProfile")]
    public sealed class OceanOpticsProfile : ScriptableObject
    {
        [SerializeField]
        private OceanOpticsState _day = new OceanOpticsState(
            new Color(0.02f, 0.95f, 0.9f, 1f),
            new Color(0.015f, 0.65f, 0.95f, 1f),
            new Color(0.005f, 0.28f, 0.85f, 1f),
            0.96f,
            0.14f,
            0.75f,
            1f);

        [SerializeField]
        private OceanOpticsState _night = new OceanOpticsState(
            new Color(0.16f, 0.14f, 0.42f, 1f),
            new Color(0.08f, 0.07f, 0.28f, 1f),
            new Color(0.04f, 0.035f, 0.18f, 1f),
            0.93f,
            0.55f,
            0.85f,
            0.35f,
            1f);

        [SerializeField]
        private OceanOpticsState _storm = new OceanOpticsState(
            new Color(0.18f, 0.30f, 0.29f, 1f),
            new Color(0.055f, 0.145f, 0.16f, 1f),
            new Color(0.02f, 0.055f, 0.082f, 1f),
            0.95f,
            0.65f,
            0.65f,
            0f,
            0.12f);

        [SerializeField]
        private OceanOpticsState _abyss = new OceanOpticsState(
            new Color(0.065f, 0.14f, 0.105f, 1f),
            new Color(0.02f, 0.065f, 0.055f, 1f),
            new Color(0.008f, 0.02f, 0.024f, 1f),
            0.98f,
            0.75f,
            0.6f,
            0f,
            0.5f);

        [SerializeField]
        private OceanZoneOptics _innerZone = new OceanZoneOptics(
            Color.white, 1f, 1f);

        [SerializeField]
        private OceanZoneOptics _middleZone = new OceanZoneOptics(
            new Color(0.55f, 0.62f, 0.78f, 1f), 0.7f, 1f);

        [SerializeField]
        private OceanZoneOptics _outerZone = new OceanZoneOptics(
            new Color(0.15f, 0.32f, 0.22f, 1f), 0.1f, 0.7f);

        [SerializeField, Min(1f)]
        private float _zoneBlendHalfWidth = 60f;

        public OceanOpticsState Day => _day;
        public OceanOpticsState Night => _night;
        public OceanOpticsState Storm => _storm;
        public OceanOpticsState Abyss => _abyss;
        public OceanZoneOptics InnerZone => _innerZone;
        public OceanZoneOptics MiddleZone => _middleZone;
        public OceanZoneOptics OuterZone => _outerZone;
        public float ZoneBlendHalfWidth => Mathf.Max(1f, _zoneBlendHalfWidth);
    }
}
