using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Lighthouse.EditorTools.Build
{
    // Input System and App UI add their settings to Preloaded Assets for the build and remove them afterwards,
    // but only in memory. ProjectSettings.asset keeps the build-time list on disk until the next manual save,
    // which shows up as a spurious diff after every build. Saving once the build is done writes the restored list.
    public sealed class PostBuildSettingsSaver : IPostprocessBuildWithReport
    {
        // Run after every package cleanup (they use order 0).
        public int callbackOrder => int.MaxValue;

        public void OnPostprocessBuild(BuildReport report)
        {
            // Defer to the next editor tick so the save happens after the whole build pipeline has finished.
            EditorApplication.delayCall += SaveSettings;
        }

        private static void SaveSettings()
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"[{nameof(PostBuildSettingsSaver)}] Saved project settings after build.");
        }
    }
}
