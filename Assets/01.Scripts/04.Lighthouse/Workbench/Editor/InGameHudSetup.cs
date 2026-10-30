using Lighthouse.Network.Session.View;
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
        private const string JobIconFolder = "Assets/03.Images/VotingUiSketch/";
        // The small frame sprite's hollow (13..717 x 14..62 of 731x78), widened a little so track and fill tuck under it.
        private static readonly Rect FrameInset = new Rect(8f / 731f, 9f / 78f, 714f / 731f, 58f / 78f);
        // The inventory's carried card (InventoryChestScreen.Hud) size and fill, its left bottom over the boat panel
        // (mockup frame 46,728 → 53,244 on the 1920x1080 canvas; the panel top is near 227).
        private static readonly Vector2 CardSize = new Vector2(230f, 76f);
        private static readonly Vector2 CardPosition = new Vector2(53f, 244f);
        private static readonly Color CardFill = new Color(0.10f, 0.075f, 0.045f, 0.9f);
        // Every key row name a build has used, so an update clears old rows too.
        private static readonly string[] KeyRows = { "Exit", "Map", "Inventory", "Ability", "Menu" };

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

                AddKeys(hud);

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
                InGameHud view = root.AddComponent<InGameHud>();
                SerializedObject serialized = new SerializedObject(view);
                AddRole(hud, serialized);
                HudSketchLayout.Picture("PanelDivider", hud, "hud_panel_divider", corner, 66f, 803f, 370f, 9f).preserveAspect = false;
                HudSketchLayout.Picture("BoatIcon", hud, "hud_icon_boat", corner, 72f, 826f, 58f, 37f);
                HudSketchLayout.Picture("BoatTitle", hud, "hud_label_boat_durability", corner, 140f, 817f, 94f, 27f);
                Image boatFill = HudSketchLayout.Bar("BoatBar", hud, corner, 142f, 849f, 178f, 22f,
                    "hud_small_bar_track", "hud_small_bar_fill", "hud_small_bar_frame", FrameInset);
                Text boatText = HudSketchLayout.Label("BoatValue", hud, "- / -", 20f, HudSketchLayout.Cream, corner, 330f, 843f, 100f, 34f, TextAnchor.MiddleLeft);

                Set(serialized, "_root", hud.gameObject);
                Set(serialized, "_card", card.gameObject);
                Set(serialized, "_cardIcon", cardIcon);
                Set(serialized, "_cardName", cardName);
                Set(serialized, "_cardUse", cardUse);
                WorkbenchScreenSketchSetup.FillRecipeIcons(serialized.FindProperty("_recipeIcons"));
                card.gameObject.SetActive(false);
                Set(serialized, "_boatFill", boatFill);
                Set(serialized, "_boatText", boatText);
                SetBoatOnlyHints(serialized, hud);
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

        // Swaps the fixed navigator picture in the saved prefab for the per-job role, without rebuilding it, so pieces
        // changed by hand since the last build (such as the carried card's board) stay.
        [MenuItem("Lighthouse/UI/Update In-Game HUD Role")]
        public static void UpdateRole()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"[{nameof(InGameHudSetup)}] Stop play mode before updating.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                Transform hud = root.transform.Find("Hud");
                Transform oldIcon = hud ? hud.Find("RoleIcon") : null;
                Transform oldName = hud ? hud.Find("RoleName") : null;

                if (!oldIcon || !oldName)
                {
                    Debug.LogError($"[{nameof(InGameHudSetup)}] {PrefabPath} has no Hud/RoleIcon or Hud/RoleName.");
                    return;
                }

                int index = oldIcon.GetSiblingIndex();
                Object.DestroyImmediate(oldIcon.gameObject);
                Object.DestroyImmediate(oldName.gameObject);
                SerializedObject serialized = new SerializedObject(root.GetComponent<InGameHud>());
                AddRole(hud, serialized);
                hud.Find("RoleIcon").SetSiblingIndex(index);
                hud.Find("RoleName").SetSiblingIndex(index + 1);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[{nameof(InGameHudSetup)}] Updated the role in {PrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Lays the key hint rows out again in the saved prefab without rebuilding it, so pieces changed by hand since the
        // last build stay.
        [MenuItem("Lighthouse/UI/Update In-Game HUD Keys")]
        public static void UpdateKeys()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"[{nameof(InGameHudSetup)}] Stop play mode before updating.");
                return;
            }

            HudSketchLayout.ImportSprites();
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                Transform hud = root.transform.Find("Hud");

                if (!hud)
                {
                    Debug.LogError($"[{nameof(InGameHudSetup)}] {PrefabPath} has no Hud.");
                    return;
                }

                foreach (string row in KeyRows)
                {
                    DestroyChild(hud, row + "Key");
                    DestroyChild(hud, row + "Label");
                }

                AddKeys(hud);
                SerializedObject serialized = new SerializedObject(root.GetComponent<InGameHud>());
                SetBoatOnlyHints(serialized, hud);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[{nameof(InGameHudSetup)}] Updated the key hints in {PrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // One step for every key hint and station prompt in the saved prefabs (2026-10-31): the HUD's rows, the
        // lighthouse screen's layout and Esc, and the workbench, lighthouse and ghost ship E prompts, all with TMP captions.
        [MenuItem("Lighthouse/UI/Update Key Hints And Prompts")]
        public static void UpdateKeyHintsAndPrompts()
        {
            UpdateKeys();
            TowerScreenSketchSetup.UpdateLayout();
            UpdatePrompt("Assets/02.Prefabs/UI/Workbench/WorkbenchScreen_Sketch.prefab", WorkbenchScreenSketchSetup.PromptCaption, 1f);
            UpdatePrompt("Assets/02.Prefabs/UI/GhostShip/GhostShipLedger.prefab", GhostShipLedgerSetup.PromptCaption, GhostShipLedgerSetup.PromptUnit);
        }

        private static void UpdatePrompt(string prefabPath, string caption, float unit)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                RectTransform prompt = (RectTransform)root.transform.Find("Prompt");

                if (!prompt)
                {
                    Debug.LogError($"[{nameof(InGameHudSetup)}] {prefabPath} has no Prompt.");
                    return;
                }

                HudSketchLayout.BuildPrompt(prompt, caption, unit);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[{nameof(InGameHudSetup)}] Updated the prompt in {prefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Key hints, top right, one row per key under the final battle strip (its right side ends near 146). Esc leads
        // as "exit" (2026-10-30). Q is every job's active ability. No V row: the voting screen shows its own button
        // bottom right while voting is open.
        // The Q row shows only aboard (2026-10-31): InGameHud toggles both of its pieces.
        private static void SetBoatOnlyHints(SerializedObject serialized, Transform hud)
        {
            SerializedProperty hints = serialized.FindProperty("_boatOnlyHints");
            hints.arraySize = 2;
            hints.GetArrayElementAtIndex(0).objectReferenceValue = hud.Find("AbilityKey").gameObject;
            hints.GetArrayElementAtIndex(1).objectReferenceValue = hud.Find("AbilityLabel").gameObject;
        }

        private static void AddKeys(Transform hud)
        {
            Image exit = Key(hud, "Exit", HudSketchLayout.ExitKeySprite, HudSketchLayout.ExitKeyWidth, 0, HudSketchLayout.ExitCaption);
            // The HUD's Esc is the match's leave control, so the session screen hides its own fallback Esc button
            // (which sat where the Tab row is now).
            exit.raycastTarget = true;
            exit.gameObject.AddComponent<Button>().targetGraphic = exit;
            exit.gameObject.AddComponent<SessionLeaveButton>();
            Key(hud, "Map", "hud_key_m", 37f, 1, "지도");
            Key(hud, "Inventory", "hud_key_tab", 50f, 2, "소지품");
            Key(hud, "Ability", "hud_key_q", 37f, 3, "능력 사용");
        }

        private static void DestroyChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);

            if (child)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        // The role icon and name in the panel's top row. The mockup only drew the navigator, so the icons are the
        // voting screen's job portraits and the name is text in the cut labels' cream; InGameHud picks the local job.
        private static void AddRole(Transform hud, SerializedObject serialized)
        {
            Vector2 corner = HudSketchLayout.BottomLeft;
            Image icon = HudSketchLayout.Rect("RoleIcon", hud, corner, 72f, 757f, 44f, 44f).gameObject.AddComponent<Image>();
            icon.sprite = LoadJobIcon("navigator");
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            Text name = HudSketchLayout.Label("RoleName", hud, "항해사", 24f, HudSketchLayout.Cream, corner, 126f, 760f, 120f, 32f, TextAnchor.MiddleLeft);
            Set(serialized, "_roleIcon", icon);
            Set(serialized, "_roleName", name);
            Set(serialized, "_navigatorIcon", LoadJobIcon("navigator"));
            Set(serialized, "_mechanicIcon", LoadJobIcon("mechanic"));
            Set(serialized, "_guardIcon", LoadJobIcon("guard"));
            Set(serialized, "_gathererIcon", LoadJobIcon("gatherer"));
        }

        private static Sprite LoadJobIcon(string job)
        {
            string path = $"{JobIconFolder}vt_job_{job}.png";
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

            if (!sprite)
            {
                throw new System.InvalidOperationException($"Missing job icon: {path}");
            }

            return sprite;
        }

        // Captions are TMP text in the HUD's font (2026-10-31), so every row reads alike.
        private static Image Key(Transform parent, string name, string keySprite, float keyWidth, int row, string caption)
        {
            HudSketchLayout.KeyRow(row, keyWidth, out Rect key, out Rect label);
            Vector2 anchor = HudSketchLayout.TopRight;
            HudSketchLayout.KeyCaption(name + "Label", parent, caption, anchor, label);
            return HudSketchLayout.Picture(name + "Key", parent, keySprite, anchor, key.x, key.y, key.width, key.height);
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
