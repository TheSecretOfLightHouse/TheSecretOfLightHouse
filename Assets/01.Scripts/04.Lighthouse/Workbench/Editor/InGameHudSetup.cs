using Lighthouse.UI.Hud;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.Editor
{
    // Builds the always-on HUD from the 2026-10-29 in-game HUD mockup (HudSketchLayout). The final battle strip at the
    // top is its own prefab (FinalBattleHudSetup). Kept in this editor assembly so no new asmdef is needed.
    // Rebuilding overwrites the prefab, so later look changes made by hand belong in the inspector, not here.
    public static class InGameHudSetup
    {
        private const string PrefabPath = "Assets/02.Prefabs/UI/Hud/InGameHud.prefab";
        // The small frame sprite's hollow (13..717 x 14..62 of 731x78), widened a little so track and fill tuck under it.
        private static readonly Rect FrameInset = new Rect(8f / 731f, 9f / 78f, 714f / 731f, 58f / 78f);

        [MenuItem("Lighthouse/UI/Build In-Game HUD Prefab")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"[{nameof(InGameHudSetup)}] Stop play mode before building.");
                return;
            }

            HudSketchLayout.ImportSprites();
            var previousScene = EditorSceneManager.GetActiveScene();
            var buildScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(buildScene);
            GameObject root = new GameObject("InGameHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.layer = LayerMask.NameToLayer("UI");

            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // Under the final battle strip (60) and the station screens (80).
                canvas.sortingOrder = 55;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                RectTransform hud = HudSketchLayout.Layer("Hud", root.transform);
                Vector2 corner = HudSketchLayout.BottomLeft;
                Vector2 bottom = HudSketchLayout.Bottom;

                // Key hints, right. Keys share a right edge; the wide ones grow to the left.
                Key(hud, "Map", "hud_key_m", 1537f, 37f, 230f, "hud_label_map", 42f);
                Key(hud, "Vote", "hud_key_v", 1537f, 37f, 277f, "hud_label_vote", 42f);
                Key(hud, "Inventory", "hud_key_tab", 1524f, 50f, 323f, "hud_label_inventory", 62f);
                Key(hud, "Menu", "hud_key_esc", 1527f, 47f, 369f, "hud_label_menu", 42f);

                // Carried item card, bottom left.
                HudSketchLayout.Backing("CardBacking", hud, HudSketchLayout.CardBacking, corner, 60f, 653f, 62f, 60f);
                HudSketchLayout.Picture("CardFrame", hud, "hud_card_frame", corner, 52f, 645f, 78f, 76f);
                HudSketchLayout.Picture("CardIcon", hud, "hud_icon_lens", corner, 62f, 656f, 58f, 51f);
                HudSketchLayout.Picture("CardName", hud, "hud_label_lens", corner, 144f, 648f, 56f, 33f);
                HudSketchLayout.Picture("CardUse", hud, "hud_label_for_lighthouse", corner, 144f, 682f, 60f, 24f);

                // Role and boat panel, bottom left.
                HudSketchLayout.Picture("Panel", hud, "hud_panel", corner, 48f, 745f, 396f, 146f).preserveAspect = false;
                HudSketchLayout.Picture("RoleIcon", hud, "hud_icon_navigator", corner, 66f, 752f, 54f, 48f);
                HudSketchLayout.Picture("RoleName", hud, "hud_label_navigator", corner, 136f, 764f, 80f, 32f);
                HudSketchLayout.Picture("PanelDivider", hud, "hud_panel_divider", corner, 62f, 801f, 368f, 10f).preserveAspect = false;
                HudSketchLayout.Picture("BoatIcon", hud, "hud_icon_boat", corner, 64f, 822f, 70f, 45f);
                HudSketchLayout.Picture("BoatTitle", hud, "hud_label_boat_durability", corner, 144f, 814f, 106f, 30f);
                Image boatFill = HudSketchLayout.Bar("BoatBar", hud, corner, 146f, 847f, 188f, 24f,
                    "hud_small_bar_track", "hud_small_bar_fill", "hud_small_bar_frame", FrameInset);
                Text boatText = HudSketchLayout.Label("BoatValue", hud, "- / -", 22f, HudSketchLayout.Cream, corner, 342f, 842f, 96f, 34f, TextAnchor.MiddleLeft);

                // Interaction hint, bottom centre.
                HudSketchLayout.Picture("PromptKey", hud, "hud_key_e", bottom, 745f, 838f, 46f, 45f);
                HudSketchLayout.Picture("PromptLabel", hud, "hud_label_use_workbench", bottom, 804f, 846f, 122f, 28f);

                InGameHud view = root.AddComponent<InGameHud>();
                SerializedObject serialized = new SerializedObject(view);
                Set(serialized, "_root", hud.gameObject);
                Set(serialized, "_boatFill", boatFill);
                Set(serialized, "_boatText", boatText);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                hud.gameObject.SetActive(false);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[{nameof(InGameHudSetup)}] Built {PrefabPath}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(buildScene, true);
            }
        }

        [MenuItem("Lighthouse/UI/Use In-Game HUD In Open Scene")]
        public static void UseInOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (EditorApplication.isPlayingOrWillChangePlaymode || !prefab)
            {
                Debug.LogError($"[{nameof(InGameHudSetup)}] Stop play mode and build the prefab first.");
                return;
            }

            foreach (InGameHud existing in Object.FindObjectsByType<InGameHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == prefab)
                {
                    Selection.activeGameObject = existing.gameObject;
                    Debug.Log($"[{nameof(InGameHudSetup)}] The scene already has the in-game HUD.");
                    return;
                }
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Add in-game HUD");
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = instance;
            Debug.Log($"[{nameof(InGameHudSetup)}] In-game HUD added. Save the scene to keep it.");
        }

        // A key cap and its caption, centred on the same line. Captions start at one shared left edge.
        private static void Key(Transform parent, string name, string keySprite, float x, float width, float y, string labelSprite, float labelWidth)
        {
            Vector2 anchor = HudSketchLayout.TopRight;
            HudSketchLayout.Picture(name + "Key", parent, keySprite, anchor, x, y, width, 36f);
            HudSketchLayout.Picture(name + "Label", parent, labelSprite, anchor, 1588f, y + 6f, labelWidth, 24f);
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
