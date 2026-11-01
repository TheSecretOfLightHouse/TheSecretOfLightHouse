using UnityEngine;

namespace Lighthouse.World.Weather.View
{
    public static class LightningSkyFlash
    {
        private static readonly int AmountId = Shader.PropertyToID("_SkyLightning");
        private static readonly int DirectionId = Shader.PropertyToID("_SkyLightningDir");

        public static void Set(float amount, Vector3 direction)
        {
            Shader.SetGlobalFloat(AmountId, Mathf.Clamp01(amount));
            Shader.SetGlobalVector(DirectionId, direction);
        }

        public static void Clear()
        {
            Shader.SetGlobalFloat(AmountId, 0f);
        }
    }
}
