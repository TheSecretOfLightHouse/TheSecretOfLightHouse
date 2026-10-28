using Lighthouse.World.Ocean.Rules;
using UnityEngine;

namespace Lighthouse.World.Environment.View
{
    [CreateAssetMenu(
        fileName = "SeaEnvironmentProfile",
        menuName = "TheLightHouse/World/Environment/Sea")]
    public sealed class SeaEnvironmentProfile : ScriptableObject
    {
        [SerializeField] private SeaZone _zone = SeaZone.Inner;

        [SerializeField]
        private SkyEnvironmentState _day = new SkyEnvironmentState(
            new Color(0.12f, 0.25f, 0.42f),
            new Color(0.42f, 0.62f, 0.72f),
            new Color(0.12f, 0.45f, 0.65f),
            0f, 0.08f, 1f, 0.1f);

        [SerializeField]
        private SkyEnvironmentState _night = new SkyEnvironmentState(
            new Color(0.008f, 0.014f, 0.04f),
            new Color(0.025f, 0.08f, 0.15f),
            new Color(0.15f, 0.38f, 0.7f),
            1f, 0.65f, 1f, 0.1f);

        public SeaZone Zone => _zone;
        public SkyEnvironmentState Day => _day;
        public SkyEnvironmentState Night => _night;
    }
}
