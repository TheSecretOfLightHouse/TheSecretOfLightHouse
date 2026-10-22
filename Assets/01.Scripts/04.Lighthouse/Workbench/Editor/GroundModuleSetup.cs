using Lighthouse.Dev.Modules;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Targets.Net;
using Lighthouse.Modules.Net;
using Lighthouse.Modules.Server;
using Lighthouse.Tower.Net;
using Lighthouse.World.Debris.Rules;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lighthouse.Workbench.Editor
{
    // Sets up shore modules (2026-10-22, T34): the GroundModule prefab, the module handler that spawns it and the network
    // manager that must know it, in every open scene. A second item adds the JH04 stand-in for World's shore services.
    // Running either again only fills in what is missing. Scenes are not saved: look at them, then save them yourself.
    public static class GroundModuleSetup
    {
        private const string PrefabPath = "Assets/02.Prefabs/Lighthouse/GroundModule.prefab";
        private const string TrialConfigPath = "Assets/02.Prefabs/Lighthouse/Data/Trial/DebrisConfig_JH04.asset";
        private const string InteractionLayerName = "Interaction";
        // Short enough to watch a module rot during a trial; the game value lives on World's DebrisConfig.
        private const float TrialModuleLifetimeSeconds = 20f;
        private const float StubPointDistance = 8f;
        private const int StubPointCount = 3;

        [MenuItem("Lighthouse/Modules/Set Up Ground Module In Open Scenes")]
        public static void SetUp()
        {
            GroundModule prefab = LoadOrCreatePrefab();

            if (!prefab)
            {
                return;
            }

            int handlers = 0;
            int managers = 0;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);

                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (ModuleInteractionHandler handler in root.GetComponentsInChildren<ModuleInteractionHandler>(true))
                    {
                        SerializedObject serialized = new SerializedObject(handler);
                        serialized.FindProperty("_groundModulePrefab").objectReferenceValue = prefab;
                        serialized.ApplyModifiedProperties();
                        EditorSceneManager.MarkSceneDirty(scene);
                        handlers++;
                    }

                    foreach (NetworkManager manager in root.GetComponentsInChildren<NetworkManager>(true))
                    {
                        if (!manager.spawnPrefabs.Contains(prefab.gameObject))
                        {
                            Undo.RecordObject(manager, "Register GroundModule");
                            manager.spawnPrefabs.Add(prefab.gameObject);
                            EditorUtility.SetDirty(manager);
                            EditorSceneManager.MarkSceneDirty(scene);
                        }

                        managers++;
                    }
                }
            }

            Debug.Log($"[{nameof(GroundModuleSetup)}] {PrefabPath}: {handlers} module handler(s) and {managers} network manager(s) set up in the open scenes. "
                + "A scene with only one of the two needs the other scene opened too (for JH04: JH04TestScene and JH04Offline). Save the scenes to keep it.");
        }

        [MenuItem("Lighthouse/Modules/Add Shore Drop Stub To Open Scene")]
        public static void AddStub()
        {
            ModuleInteractionHandler handler = Object.FindFirstObjectByType<ModuleInteractionHandler>(FindObjectsInactive.Include);
            TowerNet tower = Object.FindFirstObjectByType<TowerNet>(FindObjectsInactive.Include);

            if (!handler || !tower)
            {
                Debug.LogError($"[{nameof(GroundModuleSetup)}] The open scene needs a {nameof(ModuleInteractionHandler)} and a {nameof(TowerNet)}.");
                return;
            }

            if (Object.FindFirstObjectByType<ShoreDropStub>(FindObjectsInactive.Include))
            {
                Debug.Log($"[{nameof(GroundModuleSetup)}] The open scene already has a {nameof(ShoreDropStub)}.");
                return;
            }

            GameObject root = new GameObject("ShoreDropStub");
            SceneManager.MoveGameObjectToScene(root, handler.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(root, "Add Shore Drop Stub");
            Vector3 center = tower.transform.position;
            Transform[] points = new Transform[StubPointCount];

            for (int i = 0; i < StubPointCount; i++)
            {
                // Spread on one side of the tower; move them onto real ground before playing.
                float angle = i * 25f * Mathf.Deg2Rad;
                Transform point = new GameObject($"ShorePoint_{i}").transform;
                point.SetParent(root.transform, false);
                point.position = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * StubPointDistance;
                points[i] = point;
            }

            ShoreDropStub stub = root.AddComponent<ShoreDropStub>();
            SerializedObject serialized = new SerializedObject(stub);
            SerializedProperty pointList = serialized.FindProperty("_points");
            pointList.arraySize = points.Length;

            for (int i = 0; i < points.Length; i++)
            {
                pointList.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            }

            serialized.FindProperty("_config").objectReferenceValue = LoadOrCreateTrialConfig();
            serialized.FindProperty("_modules").objectReferenceValue = handler;
            serialized.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"[{nameof(GroundModuleSetup)}] Added {nameof(ShoreDropStub)} with {StubPointCount} shore points {StubPointDistance}m from the tower. "
                + "Move the points onto the ground, then save the scene.");
        }

        private static GroundModule LoadOrCreatePrefab()
        {
            GroundModule existing = AssetDatabase.LoadAssetAtPath<GroundModule>(PrefabPath);

            // An existing prefab keeps whatever was tuned on it.
            if (existing)
            {
                return existing;
            }

            int layer = LayerMask.NameToLayer(InteractionLayerName);

            if (layer < 0)
            {
                Debug.LogError($"[{nameof(GroundModuleSetup)}] No '{InteractionLayerName}' layer; players select targets on it.");
                return null;
            }

            GameObject root = new GameObject("GroundModule");

            try
            {
                // The player's selector only looks at targets on the interaction layer.
                root.layer = layer;
                root.AddComponent<NetworkIdentity>();
                InteractionTarget target = root.AddComponent<InteractionTarget>();
                SerializedObject serialized = new SerializedObject(target);
                serialized.FindProperty("_defaultAction").intValue = (int)InteractionAction.PickUpModule;
                SerializedProperty actions = serialized.FindProperty("_supportedActions");
                actions.arraySize = 1;
                actions.GetArrayElementAtIndex(0).intValue = (int)InteractionAction.PickUpModule;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                root.AddComponent<GroundModule>();

                // Placeholder look until art exists. No collider: selection is by distance, and it should not block anyone.
                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                Object.DestroyImmediate(body.GetComponent<Collider>());
                body.transform.SetParent(root.transform, false);
                body.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return saved ? saved.GetComponent<GroundModule>() : null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static DebrisConfig LoadOrCreateTrialConfig()
        {
            DebrisConfig config = AssetDatabase.LoadAssetAtPath<DebrisConfig>(TrialConfigPath);

            if (config)
            {
                return config;
            }

            config = ScriptableObject.CreateInstance<DebrisConfig>();
            AssetDatabase.CreateAsset(config, TrialConfigPath);
            SerializedObject serialized = new SerializedObject(config);
            serialized.FindProperty("_isModuleExpiryEnabled").boolValue = true;
            serialized.FindProperty("_moduleLifetimeSeconds").floatValue = TrialModuleLifetimeSeconds;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return config;
        }
    }
}
