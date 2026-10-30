using UnityEngine;

namespace Lighthouse.Common
{
    // Without a cap the focused window runs at 300+ FPS and starves other windows on the same PC down to 3~4 FPS,
    // which made remote movement stutter in multi-window tests (checked 2026-10-19). The listen host also runs the
    // server, so a fixed 60 matches Mirror's 60 Hz send rate and keeps host ticks steady on any monitor refresh rate.
    // VSync stays off so the cap does not follow 144/240 Hz monitors or drop to 30 when a frame misses a refresh.
    public static class FrameRateLimit
    {
        public const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
        }
    }
}
