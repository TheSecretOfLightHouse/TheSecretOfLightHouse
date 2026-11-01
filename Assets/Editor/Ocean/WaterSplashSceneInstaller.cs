using Lighthouse.World.Ocean.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lighthouse.EditorTools.Ocean
{
    public static class WaterSplashSceneInstaller
    {
        private const string MainScenePath = "Assets/00.Scenes/Main/MainGame.unity";
        private const string ObjectName = "WaterSplashEffects";

        [MenuItem("TheLightHouse/World/Water/Install Splash Service In MainGame")]
        public static void Install()
        {
            if (EditorSceneManager.GetActiveScene().path != MainScenePath)
            {
                Debug.LogError($"[{nameof(WaterSplashSceneInstaller)}] Active scene must be {MainScenePath}.");
                return;
            }
            var impact = AssetDatabase.LoadAssetAtPath<WaterSplashEffect>(WaterSplashPrefabBuilder.ImpactPrefabPath);
            var muzzle = AssetDatabase.LoadAssetAtPath<WaterSplashEffect>(WaterSplashPrefabBuilder.MuzzlePrefabPath);
            if (impact == null || muzzle == null)
            {
                Debug.LogError($"[{nameof(WaterSplashSceneInstaller)}] Build the splash prefabs first.");
                return;
            }
            var service = Object.FindFirstObjectByType<WaterSplashEffectsService>(FindObjectsInactive.Include);
            if (service == null)
            {
                var go = new GameObject(ObjectName);
                Undo.RegisterCreatedObjectUndo(go, "Install water splash service");
                service = go.AddComponent<WaterSplashEffectsService>();
            }
            var serialized = new SerializedObject(service);
            SerializedProperty prefabs = serialized.FindProperty("_prefabs");
            prefabs.arraySize = 2;
            prefabs.GetArrayElementAtIndex(0).objectReferenceValue = impact;
            prefabs.GetArrayElementAtIndex(1).objectReferenceValue = muzzle;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(service.gameObject.scene);
            Debug.Log($"[{nameof(WaterSplashSceneInstaller)}] Installed {ObjectName}.");
        }
    }
}
