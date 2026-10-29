using Lighthouse.Player.Inventory.View;
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
        // The inventory's carried card (InventoryChestScreen.Hud) size and fill, its left bottom over the boat panel
        // (mockup frame 46,728 → 53,244 on the 1920x1080 canvas; the panel top is near 227).
        private static readonly Vector2 CardSize = new Vector2(230f, 76f);
        private static readonly Vector2 CardPosition = new Vector2(53f, 244f);
        private static readonly Color CardFill = new Color(0.10f, 0.075f, 0.045f, 0.9f);

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

                // Carried module card, bottom left above the boat panel, shown only while a module is carried. Same look
                // as the inventory's carried card (InventoryChestScreen.Hud: its drawn frame, colours, sizes and the
                // workbench screen's icons), placed in canvas units over the panel. Name only, no use line.
                RectTransform card = CardRect("Card", hud, 0f, 0f, CardSize.x, CardSize.y);
                card.anchorMin = card.anchorMax = card.pivot = Vector2.zero;
                card.anchoredPosition = CardPosition;
                InventoryFrameGraphic cardFrame = CardRect("CardFrame", card, 0f, 0f, CardSize.x, CardSize.y).gameObject.AddComponent<InventoryFrameGraphic>();
                cardFrame.color = CardFill;
                cardFrame.Configure(HudSketchLayout.Cream);
                cardFrame.raycastTarget = false;
                Image cardIcon = CardRect("CardIcon", card, 10f, 9f, 58f, 58f).gameObject.AddComponent<Image>();
                cardIcon.preserveAspect = true;
                cardIcon.raycastTarget = false;
                CardText("CardCaption", card, "운반 중", 16, HudSketchLayout.Gold, 10f);
                Text cardName = CardText("CardName", card, "모듈", 17, HudSketchLayout.Cream, 36f);
                cardName.resizeTextForBestFit = true;
                cardName.resizeTextMinSize = 12;
                cardName.resizeTextMaxSize = 17;
                Text cardUse = null;

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
                Set(serialized, "_card", card.gameObject);
                Set(serialized, "_cardIcon", cardIcon);
                Set(serialized, "_cardName", cardName);
                Set(serialized, "_cardUse", cardUse);
                WorkbenchScreenSketchSetup.FillRecipeIcons(serialized.FindProperty("_recipeIcons"));
                card.gameObject.SetActive(false);
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

        // A card child placed from the card's top left in canvas units, as the inventory card lays out its pieces.
        private static RectTransform CardRect(string name, Transform parent, float x, float y, float width, float height)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        // The inventory card's text: bold, no outline, right of the icon.
        private static Text CardText(string name, Transform parent, string caption, int size, Color color, float y)
        {
            Text text = CardRect(name, parent, 82f, y, 135f, size == 16 ? 25f : 27f).gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontStyle = FontStyle.Bold;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = caption;
            text.raycastTarget = false;
            return text;
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
