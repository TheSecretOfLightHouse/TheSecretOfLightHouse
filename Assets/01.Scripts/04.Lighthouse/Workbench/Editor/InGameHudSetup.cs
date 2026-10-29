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

                // Key hints, top right, one row per key under the final battle strip (its right side ends near 146).
                // No V row: the voting screen shows its own button bottom right while voting is open.
                Key(hud, "Map", "hud_key_m", 37f, 0, "hud_label_map", 42f);
                Key(hud, "Inventory", "hud_key_tab", 50f, 1, "hud_label_inventory", 62f);
                Key(hud, "Menu", "hud_key_esc", 47f, 2, "hud_label_menu", 42f);

                // Carried item card, bottom left.
                HudSketchLayout.Backing("CardBacking", hud, HudSketchLayout.CardBacking, corner, 60f, 653f, 62f, 60f);
                HudSketchLayout.Picture("CardFrame", hud, "hud_card_frame", corner, 52f, 645f, 78f, 76f);
                HudSketchLayout.Picture("CardIcon", hud, "hud_icon_lens", corner, 62f, 656f, 58f, 51f);
                HudSketchLayout.Picture("CardName", hud, "hud_label_lens", corner, 144f, 648f, 56f, 33f);
                HudSketchLayout.Picture("CardUse", hud, "hud_label_for_lighthouse", corner, 144f, 682f, 60f, 24f);

                // Role and boat panel, bottom left. A little wider than the mockup, with every piece kept clear of
                // the panel's thick rounded rim (the mockup's spacing let the icons touch it).
                HudSketchLayout.Picture("Panel", hud, "hud_panel", corner, 46f, 743f, 410f, 150f).preserveAspect = false;
                HudSketchLayout.Picture("RoleIcon", hud, "hud_icon_navigator", corner, 72f, 758f, 44f, 39f);
                HudSketchLayout.Picture("RoleName", hud, "hud_label_navigator", corner, 126f, 762f, 70f, 28f);
                HudSketchLayout.Picture("PanelDivider", hud, "hud_panel_divider", corner, 66f, 803f, 370f, 9f).preserveAspect = false;
                HudSketchLayout.Picture("BoatIcon", hud, "hud_icon_boat", corner, 72f, 826f, 58f, 37f);
                HudSketchLayout.Picture("BoatTitle", hud, "hud_label_boat_durability", corner, 140f, 817f, 94f, 27f);
                Image boatFill = HudSketchLayout.Bar("BoatBar", hud, corner, 142f, 849f, 178f, 22f,
                    "hud_small_bar_track", "hud_small_bar_fill", "hud_small_bar_frame", FrameInset);
                Text boatText = HudSketchLayout.Label("BoatValue", hud, "- / -", 20f, HudSketchLayout.Cream, corner, 330f, 843f, 100f, 34f, TextAnchor.MiddleLeft);

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

            DayCycleHudSetup.Build();
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

        // A key cap and its caption on one row, 1.12x the mockup's size. Every key is centred in one shared column and
        // every caption starts at one shared left edge, so the rows line up whatever the key's width.
        private static void Key(Transform parent, string name, string keySprite, float keyWidth, int row, string labelSprite, float labelWidth)
        {
            const float size = 1.12f;
            const float right = 1648f;
            const float top = 160f;
            float labelX = right - 62f * size;
            float columnWidth = 50f * size;
            float columnX = labelX - 14f * size - columnWidth;
            float y = top + row * 46f * size;
            float width = keyWidth * size;
            Vector2 anchor = HudSketchLayout.TopRight;
            HudSketchLayout.Picture(name + "Key", parent, keySprite, anchor, columnX + (columnWidth - width) * 0.5f, y, width, 36f * size);
            HudSketchLayout.Picture(name + "Label", parent, labelSprite, anchor, labelX, y + 6f * size, labelWidth * size, 24f * size);
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
