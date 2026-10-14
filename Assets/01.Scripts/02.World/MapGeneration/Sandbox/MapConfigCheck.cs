using System.Collections.Generic;
using Lighthouse.World.MapGeneration.Rules;
using UnityEngine;

namespace Lighthouse.World.MapGeneration.Sandbox
{
    public class MapConfigCheck : MonoBehaviour
    {
        [SerializeField] private MapGenerationConfig _config;

        private readonly List<string> _errors = new List<string>();

        [ContextMenu("MapGeneration/Validate Config")]
        private void ValidateConfig()
        {
            bool isValid = MapGenerationConfigValidator.TryValidate(_config, _errors);
            for (int i = 0; i < _errors.Count; i++)
            {
                Debug.LogError($"[{nameof(MapConfigCheck)}] {_errors[i]}");
            }

            uint first = MapContentHasher.Compute(_config);
            uint second = MapContentHasher.Compute(_config);
            bool isStable = first == second;

            MapCheckLog.Report("Config validate", isValid, $"errors={_errors.Count}");
            MapCheckLog.Report("Config hash stable", isStable, $"0x{first:X8}");
        }

        [ContextMenu("MapGeneration/Run Layout Hash Tests")]
        private void RunLayoutHashTests()
        {
            bool isAllPassed = MapLayoutChecks.RunAll();
            Debug.Log($"[{nameof(MapConfigCheck)}] {(isAllPassed ? "ALL PASS" : "FAILED")}");
        }
    }
}
