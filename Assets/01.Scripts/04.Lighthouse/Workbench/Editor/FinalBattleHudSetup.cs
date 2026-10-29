using Lighthouse.Tower.Net;
using Lighthouse.Tower.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.Editor
{
    // Builds the lighthouse HUD from the 2026-10-29 in-game HUD mockup: repair on the left (whole match), the title in
    // the centre and the lighthouse's health on the right (final battle only, under Battle), each kept to its own edge
    // of the screen (HudSketchLayout).
    public static class FinalBattleHudSetup
    {
        private const string PrefabPath = "Assets/02.Prefabs/UI/Tower/FinalBattleHud.prefab";
        // The frame sprite's hollow (20..783 x 23..65 of 805x86), widened a little so track and fill tuck under the ring.
        private static readonly Rect FrameInset = new Rect(14f / 805f, 17f / 86f, 777f / 805f, 54f / 86f);
        private static readonly Rect FullInset = new Rect(0f, 0f, 1f, 1f);

        [MenuItem("Lighthouse/Tower/Build Final Battle HUD Prefab")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"[{nameof(FinalBattleHudSetup)}] Stop play mode before building.");
                return;
            }

            HudSketchLayout.ImportSprites();
            var previousScene = EditorSceneManager.GetActiveScene();
            var buildScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(buildScene);
            GameObject root = new GameObject("FinalBattleHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.layer = LayerMask.NameToLayer("UI");

            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // Under the station screens (80), which cover it while open.
                canvas.sortingOrder = 60;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                RectTransform hud = HudSketchLayout.Layer("Hud", root.transform);
                Vector2 left = HudSketchLayout.TopLeft;
                Vector2 centre = HudSketchLayout.Top;
                Vector2 right = HudSketchLayout.TopRight;
                RectTransform battle = HudSketchLayout.Layer("Battle", hud);

                // Repair, left.
                HudSketchLayout.Picture("Lighthouse", hud, "hud_icon_lighthouse", left, 96f, 28f, 80f, 94f);
                HudSketchLayout.Picture("RepairTitle", hud, "hud_label_repair", left, 184f, 42f, 146f, 40f);
                Image repairFill = HudSketchLayout.Bar("RepairBar", hud, left, 186f, 82f, 276f, 30f,
                    "hud_bar_track", "hud_bar_fill_yellow", "hud_bar_frame", FrameInset);
                Text repairText = HudSketchLayout.Label("RepairValue", hud, "0%", 30f, HudSketchLayout.Gold, left, 472f, 74f, 100f, 40f, TextAnchor.MiddleLeft);
                HudSketchLayout.Picture("RepairGoal", hud, "hud_goal_citizen", left, 184f, 116f, 162f, 30f);

                // Title, centre.
                HudSketchLayout.Picture("DividerLeft", battle, "hud_divider_left", centre, 566f, 84f, 134f, 18f);
                HudSketchLayout.Picture("LanternRayLeft", battle, "hud_ray_left", centre, 792f, 30f, 22f, 27f);
                HudSketchLayout.Picture("Lantern", battle, "hud_icon_lantern", centre, 820f, 20f, 34f, 50f);
                HudSketchLayout.Picture("LanternRayRight", battle, "hud_ray_right", centre, 862f, 30f, 22f, 27f);
                HudSketchLayout.Picture("Title", battle, "hud_title_final_battle", centre, 728f, 64f, 214f, 66f);
                HudSketchLayout.Picture("DividerRight", battle, "hud_divider_right", centre, 976f, 84f, 134f, 18f);

                // Health, right. No cream frame here, as in the mockup.
                HudSketchLayout.Picture("Shield", battle, "hud_icon_shield", right, 1152f, 42f, 54f, 68f);
                HudSketchLayout.Picture("HealthTitle", battle, "hud_label_health", right, 1218f, 44f, 146f, 40f);
                Text healthText = HudSketchLayout.Label("HealthValue", battle, "0 / 0", 26f, HudSketchLayout.Cream, right, 1400f, 46f, 166f, 36f, TextAnchor.MiddleRight);
                Image healthFill = HudSketchLayout.Bar("HealthBar", battle, right, 1220f, 84f, 348f, 28f,
                    "hud_bar_track", "hud_bar_fill_red", null, FullInset);
                HudSketchLayout.Picture("HealthGoal", battle, "hud_goal_mafia", right, 1376f, 116f, 186f, 30f);
                Text damageText = HudSketchLayout.Label("HealthDamage", battle, "-0", 26f, HudSketchLayout.Hit, right, 1400f, 14f, 166f, 34f, TextAnchor.MiddleRight);

                FinalBattleHud view = root.AddComponent<FinalBattleHud>();
                SerializedObject serialized = new SerializedObject(view);
                Set(serialized, "_root", hud.gameObject);
                Set(serialized, "_battleRoot", battle.gameObject);
                Set(serialized, "_repairFill", repairFill);
                Set(serialized, "_repairText", repairText);
                Set(serialized, "_healthFill", healthFill);
                Set(serialized, "_healthText", healthText);
                Set(serialized, "_damageText", damageText);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                battle.gameObject.SetActive(false);
                hud.gameObject.SetActive(false);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[{nameof(FinalBattleHudSetup)}] Built {PrefabPath}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(buildScene, true);
            }
        }

        [MenuItem("Lighthouse/Tower/Use Final Battle HUD In Open Scene")]
        public static void UseInOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            TowerMatchNet[] matches = Object.FindObjectsByType<TowerMatchNet>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (EditorApplication.isPlayingOrWillChangePlaymode || matches.Length != 1 || !prefab)
            {
                Debug.LogError($"[{nameof(FinalBattleHudSetup)}] Open a scene with one lighthouse match in edit mode and build the prefab first.");
                return;
            }

            FinalBattleHud hud = null;

            foreach (FinalBattleHud existing in Object.FindObjectsByType<FinalBattleHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == prefab)
                {
                    hud = existing;
                }
            }

            if (!hud)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Add final battle HUD");
                hud = instance.GetComponent<FinalBattleHud>();
            }

            SerializedObject links = new SerializedObject(hud);
            Set(links, "_match", matches[0]);
            links.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = hud.gameObject;
            Debug.Log($"[{nameof(FinalBattleHudSetup)}] Final battle HUD connected. Save the scene to keep it.");
        }

        private static void Set(SerializedObject serialized, string name, Object value)
        {
            SerializedProperty property = serialized.FindProperty(name);

            if (property == null)
            {
                throw new System.InvalidOperationException($"Missing HUD field: {name}");
            }

            property.objectReferenceValue = value;
        }
    }
}
