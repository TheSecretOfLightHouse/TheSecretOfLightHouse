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
            new Color(0.333f, 0.788f, 0.686f, 1f),
            new Color(0.078f, 0.482f, 0.471f, 1f),
            new Color(0.031f, 0.184f, 0.275f, 1f),
            0.85f,
            0.3f,
            0.8f);

        [SerializeField]
        private OceanOpticsState _night = new OceanOpticsState(
            new Color(0.055f, 0.145f, 0.165f, 1f),
            new Color(0.024f, 0.075f, 0.11f, 1f),
            new Color(0.012f, 0.031f, 0.055f, 1f),
            0.93f,
            0.55f,
            0.85f);

        [SerializeField]
        private OceanOpticsState _storm = new OceanOpticsState(
            new Color(0.18f, 0.30f, 0.29f, 1f),
            new Color(0.055f, 0.145f, 0.16f, 1f),
            new Color(0.02f, 0.055f, 0.082f, 1f),
            0.95f,
            0.65f,
            0.65f);

        [SerializeField]
        private OceanOpticsState _abyss = new OceanOpticsState(
            new Color(0.065f, 0.14f, 0.105f, 1f),
            new Color(0.02f, 0.065f, 0.055f, 1f),
            new Color(0.008f, 0.02f, 0.024f, 1f),
            0.98f,
            0.75f,
            0.6f);

        public OceanOpticsState Day => _day;
        public OceanOpticsState Night => _night;
        public OceanOpticsState Storm => _storm;
        public OceanOpticsState Abyss => _abyss;
    }
}
