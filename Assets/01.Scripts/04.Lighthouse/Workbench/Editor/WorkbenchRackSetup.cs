using Lighthouse.Dev.Tower;
using Lighthouse.Dev.Workbench;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Targets.Net;
using Lighthouse.Workbench.Net;
using Lighthouse.Workbench.View;
using Lighthouse.World.Harvest.Rules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lighthouse.Workbench.Editor
{
    // Puts the shelf rack on the workbench of the open scene (2026-10-20 trial) and hands the rack and the screen UIs
    // to the trial panel, which switches between them.
    // Only the open scene changes, and nothing is saved: look at it, then save the scene yourself.
    public static class WorkbenchRackSetup
    {
        private const string ResourceFolder = "Assets/02.Prefabs/World/Data/MapGeneration/ResourceDefs/";
        private const string MaterialPath = "Assets/02.Prefabs/Lighthouse/Data/Trial/M_JH04_TowerBase.mat";
        private const string BoxName = "WorkbenchBox";

        // Same block the bench prefab used before the rack.
        private static readonly Vector3 BoxSize = new Vector3(2f, 1f, 1f);

        [MenuItem("Lighthouse/Workbench/Set Up Rack In Open Scene")]
        public static void SetUp()
        {
            WorkbenchNet bench = Object.FindFirstObjectByType<WorkbenchNet>(FindObjectsInactive.Include);

            if (!bench)
            {
                Debug.LogError($"[{nameof(WorkbenchRackSetup)}] No {nameof(WorkbenchNet)} in the open scene.");
                return;
            }

            Undo.SetCurrentGroupName("Set Up Workbench Rack");
            int group = Undo.GetCurrentGroup();
            Transform benchTransform = bench.transform;
            // Running again on a scene already set up only adds what is missing (the box came later).
            WorkbenchRack rack = bench.GetComponentInChildren<WorkbenchRack>(true);

            if (!rack)
            {
                rack = CreateRack(bench);
            }

            GameObject box = FindChild(benchTransform, BoxName);

            if (!box)
            {
                box = CreateBox(benchTransform, rack.transform);
            }

            // The screen UI stays in the scene; the trial panel switches between it (opened at the box) and the rack.
            WorkbenchScreen screen = Object.FindFirstObjectByType<WorkbenchScreen>(FindObjectsInactive.Include);
            WorkbenchTestPanel panel = Object.FindFirstObjectByType<WorkbenchTestPanel>(FindObjectsInactive.Include);

            foreach (TowerTrialPanel trialPanel in Object.FindObjectsByType<TowerTrialPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                SerializedObject trialSerialized = new SerializedObject(trialPanel);
                trialSerialized.FindProperty("_rack").objectReferenceValue = rack;
                trialSerialized.FindProperty("_workbenchScreen").objectReferenceValue = screen ? screen.gameObject : null;
                trialSerialized.FindProperty("_workbenchPanel").objectReferenceValue = panel;
                trialSerialized.FindProperty("_screenBox").objectReferenceValue = box;
                trialSerialized.ApplyModifiedProperties();
            }

            PrefabUtility.RecordPrefabInstancePropertyModifications(benchTransform);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(bench.gameObject.scene);
            Selection.activeObject = rack.gameObject;
            Debug.Log($"[{nameof(WorkbenchRackSetup)}] Rack and box set up on {bench.name}. Save the scene to keep it.");
        }

        private static WorkbenchRack CreateRack(WorkbenchNet bench)
        {
            Transform benchTransform = bench.transform;
            Vector3 floor = benchTransform.position;
            Renderer benchRenderer = bench.GetComponent<Renderer>();

            // The old bench block is replaced by the rack and the box; its root is unscaled so children keep their shape.
            if (benchRenderer)
            {
                floor.y = benchRenderer.bounds.min.y;
                Undo.RecordObject(benchRenderer, "Hide bench block");
                benchRenderer.enabled = false;
            }

            Collider benchCollider = bench.GetComponent<Collider>();

            if (benchCollider)
            {
                Undo.RecordObject(benchCollider, "Disable bench block collider");
                benchCollider.enabled = false;
            }

            Undo.RecordObject(benchTransform, "Unscale bench");
            benchTransform.localScale = Vector3.one;
            InteractionTarget target = bench.GetComponent<InteractionTarget>();
            AddSupportedAction(target, InteractionAction.Withdraw);

            // Facing the scene origin, where players start in the trial scenes.
            Vector3 facing = -benchTransform.position;
            facing.y = 0f;

            if (facing.sqrMagnitude < 0.01f)
            {
                facing = Vector3.back;
            }

            GameObject rackObject = new GameObject("WorkbenchRack");
            Undo.RegisterCreatedObjectUndo(rackObject, "Create rack");
            rackObject.transform.SetParent(benchTransform, false);
            rackObject.transform.SetPositionAndRotation(floor, Quaternion.LookRotation(facing));
            WorkbenchRack rack = Undo.AddComponent<WorkbenchRack>(rackObject);

            SerializedObject serialized = new SerializedObject(rack);
            serialized.FindProperty("_workbench").objectReferenceValue = bench;
            serialized.FindProperty("_target").objectReferenceValue = target;
            serialized.FindProperty("_material").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            SerializedProperty styles = serialized.FindProperty("_styles");
            styles.arraySize = 6;
            // Plank, rebar and friends: stand-ins until resource models exist.
            SetStyle(styles.GetArrayElementAtIndex(0), "Resource_Wood", RackPieceShape.Box, new Vector3(0.13f, 0.035f, 0.42f), new Color(0.62f, 0.42f, 0.24f));
            SetStyle(styles.GetArrayElementAtIndex(1), "Resource_Iron", RackPieceShape.Rod, new Vector3(0.05f, 0.05f, 0.44f), new Color(0.32f, 0.34f, 0.38f));
            SetStyle(styles.GetArrayElementAtIndex(2), "Resource_Stone", RackPieceShape.Box, new Vector3(0.15f, 0.12f, 0.15f), new Color(0.55f, 0.55f, 0.53f));
            SetStyle(styles.GetArrayElementAtIndex(3), "Resource_Fiber", RackPieceShape.Rod, new Vector3(0.13f, 0.13f, 0.22f), new Color(0.82f, 0.74f, 0.5f));
            SetStyle(styles.GetArrayElementAtIndex(4), "Resource_Glass", RackPieceShape.Box, new Vector3(0.13f, 0.2f, 0.02f), new Color(0.7f, 0.9f, 0.95f));
            SetStyle(styles.GetArrayElementAtIndex(5), "Resource_Crystal", RackPieceShape.Ball, new Vector3(0.12f, 0.16f, 0.12f), new Color(0.62f, 0.4f, 0.9f));
            serialized.ApplyModifiedProperties();
            return rack;
        }

        // The plain bench block for screen mode: it sits on the bench's own spot, so the screen's range check (taken
        // from the bench) matches where players walk up to it.
        private static GameObject CreateBox(Transform benchTransform, Transform rack)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(box, "Create workbench box");
            box.name = BoxName;
            box.transform.SetParent(benchTransform, false);
            box.transform.SetPositionAndRotation(rack.position + Vector3.up * BoxSize.y * 0.5f, rack.rotation);
            box.transform.localScale = BoxSize;
            box.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            box.SetActive(false);
            return box;
        }

        private static GameObject FindChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            return child ? child.gameObject : null;
        }

        // The shared Workbench prefab lists Deposit and Craft only, so the server answers ActionNotSupported to a
        // withdraw. Only this scene's instance gains it; JH03 scenes keep the prefab as is.
        private static void AddSupportedAction(InteractionTarget target, InteractionAction action)
        {
            if (!target || target.Supports(action))
            {
                return;
            }

            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty actions = serialized.FindProperty("_supportedActions");
            actions.arraySize++;
            actions.GetArrayElementAtIndex(actions.arraySize - 1).intValue = (int)action;
            serialized.ApplyModifiedProperties();
        }

        private static void SetStyle(SerializedProperty style, string resourceAsset, RackPieceShape shape, Vector3 size, Color color)
        {
            ResourceDefinition resource = AssetDatabase.LoadAssetAtPath<ResourceDefinition>($"{ResourceFolder}{resourceAsset}.asset");

            if (!resource)
            {
                Debug.LogWarning($"[{nameof(WorkbenchRackSetup)}] Missing {resourceAsset}; that resource uses the fallback look.");
            }

            style.FindPropertyRelative("_resource").objectReferenceValue = resource;
            style.FindPropertyRelative("_shape").enumValueIndex = (int)shape;
            style.FindPropertyRelative("_size").vector3Value = size;
            style.FindPropertyRelative("_color").colorValue = color;
        }
    }
}
