using System.Collections.Generic;
using Lighthouse.Interaction.Targets.Net;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.Rules;
using Lighthouse.Tower.View;
using Lighthouse.Workbench.Rules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.Editor
{
    // Builds the lighthouse install screen from the 2026-10-21 sketch (v3) with the workbench sketch's sprites, so the
    // two screens share one look. Positions are the sketch's pixels at 1672x941.
    public static class TowerScreenSketchSetup
    {
        private const string PrefabPath = "Assets/02.Prefabs/UI/Tower/TowerScreen_Sketch.prefab";
        private const string LayoutPath = "Assets/02.Prefabs/Lighthouse/Data/Trial/TowerLayout_JH04Trial.asset";
        private const string SketchFolder = "Assets/03.Images/WorkbenchUiSketch/";
        private const string TowerFolder = "Assets/03.Images/TowerUiSketch/";
        private const string HudFolder = "Assets/03.Images/InGameHud/";
        private const string PromptCaption = "등대 사용";
        private const float CardX = 651f;
        private const float CardY = 55f;
        private const float CardScale = 440f / 480f;
        private const float RowTop = 228f;
        private const float RowHeight = 133f;
        private const float SlotSize = 76f;
        private const float SlotX = 810f;
        private const float SlotStep = 86f;
        private static readonly Color Cream = new Color(0.98f, 0.92f, 0.79f);
        private static readonly Color Dark = new Color(0.12f, 0.09f, 0.05f);
        private static readonly Dictionary<string, string> RecipeSprites = new Dictionary<string, string>
        {
            { "ModuleRecipe_Frame", "ws_icon_frame" },
            { "ModuleRecipe_Frame_JH04Trial", "ws_icon_frame" },
            { "ModuleRecipe_Gear", "ws_icon_gear" },
            { "ModuleRecipe_Gear_JH04Trial", "ws_icon_gear" },
            { "ModuleRecipe_Lens", "ws_icon_lens" },
            { "ModuleRecipe_LightCore", "ws_icon_core" },
            { "ModuleRecipe_BoatSearchlight", "ws_icon_boat_searchlight" },
            { "ModuleRecipe_BoatDash", "ws_icon_boat_dash" },
            { "ModuleRecipe_BoatObstacleAlarm", "ws_icon_boat_alarm" },
            { "ModuleRecipe_BoatStabilizer", "ws_icon_boat_stabilizer" }
        };

        [MenuItem("Lighthouse/Tower/Build Sketch Screen Prefab")]
        public static void Build()
        {
            TowerLayout layout = AssetDatabase.LoadAssetAtPath<TowerLayout>(LayoutPath);

            if (EditorApplication.isPlayingOrWillChangePlaymode || !layout || layout.Validate() != null)
            {
                Debug.LogError($"[{nameof(TowerScreenSketchSetup)}] Stop play mode and check {LayoutPath} before building.");
                return;
            }

            ImportSprites();
            HudSketchLayout.ImportSprites();
            var previousScene = EditorSceneManager.GetActiveScene();
            var buildScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(buildScene);
            GameObject root = new GameObject("TowerScreen_Sketch", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.layer = LayerMask.NameToLayer("UI");

            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 80;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1672f, 941f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

                RectTransform window = Rect("Window", root.transform, 0f, 0f, 1672f, 941f);
                Stretch(window);
                Image scrim = window.gameObject.AddComponent<Image>();
                scrim.color = new Color(0f, 0f, 0f, 0.15f);
                RectTransform frame = Rect("Frame", window, 0f, 0f, 1672f, 941f);
                PlaceFrameRight(frame);

                Image card = Picture("Tower", frame, TowerFolder, "tw_card_tower", CardX, CardY, 480f * CardScale, 780f * CardScale);
                card.preserveAspect = true;
                Text[] floorLabels = BuildFloors(frame, layout.FloorCount);
                TowerSlotView[] slots = BuildSlots(frame, layout);

                Image track = Picture("ProgressTrack", frame, SketchFolder, "ws_bar_track", 670f, 795f, 393f, 30f);
                Image fill = Picture("Fill", track.transform, SketchFolder, "ws_bar_fill", 0f, 0f, 1f, 1f);
                RectTransform fillRect = fill.rectTransform;
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = new Vector2(0.375f, 1f);
                fillRect.offsetMin = new Vector2(4f, 4f);
                fillRect.offsetMax = new Vector2(-4f, -4f);
                Text progress = Label("ProgressText", frame, "0/8", 26, 670f, 830f, 393f, 40f);

                Image carryPanel = Picture("Carry", frame, SketchFolder, "ws_panel_shelf", 1128f, 315f, 200f, 300f);
                Label("Title", carryPanel.transform, "들고 있는 모듈", 20, 0f, 18f, 200f, 32f);
                Image carrySlot = Picture("Slot", carryPanel.transform, SketchFolder, "ws_slot", 38f, 62f, 124f, 124f);
                Image carryIcon = Picture("Icon", carrySlot.transform, null, null, 18f, 18f, 88f, 88f);
                carryIcon.preserveAspect = true;
                Image installImage = Picture("Install", carryPanel.transform, SketchFolder, "ws_button_active", 26f, 200f, 148f, 50f);
                installImage.raycastTarget = true;
                Button install = installImage.gameObject.AddComponent<Button>();
                install.targetGraphic = installImage;
                ColorBlock colors = install.colors;
                colors.highlightedColor = new Color(1f, 0.92f, 0.75f);
                colors.pressedColor = new Color(0.85f, 0.72f, 0.45f);
                // The blocked look comes from the sprite swap, not a tint.
                colors.disabledColor = Color.white;
                install.colors = colors;
                Text installText = Label("Text", installImage.transform, "장착", 24, 0f, 0f, 148f, 50f);
                installText.color = Dark;
                Image installKey = Keycap(carryPanel.transform, "F", 84f, 252f, 32f);
                CanvasGroup installKeyGroup = installKey.gameObject.AddComponent<CanvasGroup>();
                Text message = Label("Message", frame, string.Empty, 16, 1098f, 625f, 260f, 60f);
                Outline outline = message.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.12f, 0.09f, 0.05f, 0.85f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);

                AddCloseHint(window);

                // Keycap + TMP caption without a backing panel, like every station's prompt (2026-10-31).
                RectTransform prompt = Rect("Prompt", root.transform, 0f, 0f, 177f, 48f);
                prompt.anchorMin = prompt.anchorMax = new Vector2(0.5f, 0f);
                prompt.pivot = new Vector2(0.5f, 0f);
                // Above the workbench screen's carried module card at the bottom centre (2026-10-27).
                prompt.anchoredPosition = new Vector2(0f, 150f);
                HudSketchLayout.BuildPrompt(prompt, PromptCaption, 1f);

                TowerScreen screen = root.AddComponent<TowerScreen>();
                SerializedObject serialized = new SerializedObject(screen);
                Set(serialized, "_window", window.gameObject);
                Set(serialized, "_prompt", prompt.gameObject);
                Set(serialized, "_message", message);
                Set(serialized, "_progressFill", fillRect);
                Set(serialized, "_progressText", progress);
                Set(serialized, "_carryIcon", carryIcon);
                Set(serialized, "_installButton", install);
                Set(serialized, "_installBackground", installImage);
                Set(serialized, "_installText", installText);
                Set(serialized, "_installKeycap", installKeyGroup);
                Set(serialized, "_installReadySprite", Load(SketchFolder, "ws_button_active"));
                Set(serialized, "_installBlockedSprite", Load(SketchFolder, "ws_button"));
                SetArray(serialized, "_slots", slots);
                SetArray(serialized, "_floorLabels", floorLabels);
                SetRecipeIcons(serialized);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                ConfigureRitualButton(root);
                window.gameObject.SetActive(false);
                prompt.gameObject.SetActive(false);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[{nameof(TowerScreenSketchSetup)}] Built {PrefabPath}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(buildScene, true);
            }
        }

        [MenuItem("Lighthouse/Tower/Add Ritual Button To Sketch Prefab")]
        public static void AddRitualButton()
        {
            // A prefab-only update is safe while a main scene is open; never save that scene as a side effect.
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ConfigureRitualButton(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureRitualButton(GameObject root)
        {
            Transform frame = root.transform.Find("Window/Frame");
            Transform source = frame.Find("Carry/Install");
            Transform existing = frame.Find("StartRitual");
            GameObject buttonObject = existing ? existing.gameObject : Object.Instantiate(source.gameObject, frame, false);
            buttonObject.name = "StartRitual";
            RectTransform rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(1128f, -720f);
            rect.sizeDelta = new Vector2(200f, 60f);
            Text caption = buttonObject.GetComponentInChildren<Text>(true);
            caption.text = "의식 시작";
            caption.fontSize = 24;
            caption.resizeTextForBestFit = true;
            caption.resizeTextMinSize = 18;
            caption.resizeTextMaxSize = 24;
            caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            caption.verticalOverflow = VerticalWrapMode.Truncate;
            caption.rectTransform.anchorMin = Vector2.zero;
            caption.rectTransform.anchorMax = Vector2.one;
            caption.rectTransform.offsetMin = new Vector2(10f, 6f);
            caption.rectTransform.offsetMax = new Vector2(-10f, -6f);
            Text hint = frame.Find("RitualHint") ? frame.Find("RitualHint").GetComponent<Text>()
                : Label("RitualHint", frame, string.Empty, 18, 1128f, 790f, 200f, 64f);
            hint.text = "수리 100% 후 시작 가능\n시작하면 투표가 잠깁니다";
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            hint.verticalOverflow = VerticalWrapMode.Truncate;
            hint.resizeTextForBestFit = true;
            hint.resizeTextMinSize = 14;
            hint.resizeTextMaxSize = 18;
            var serialized = new SerializedObject(root.GetComponent<TowerScreen>());
            Set(serialized, "_ritualButton", buttonObject.GetComponent<Button>());
            Set(serialized, "_ritualBackground", buttonObject.GetComponent<Image>());
            Set(serialized, "_ritualText", caption);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Moves the saved prefab to the 2026-10-30 layout without rebuilding it, so pieces changed by hand stay: the
        // frame on the right half (the camera leaves the lighthouse on the left), and the HUD's Esc row in place of the
        // F install and Esc close hints. Also redraws the E prompt with a TMP caption (2026-10-31).
        [MenuItem("Lighthouse/Tower/Update Sketch Screen Layout")]
        public static void UpdateLayout()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"[{nameof(TowerScreenSketchSetup)}] Stop play mode before updating.");
                return;
            }

            HudSketchLayout.ImportSprites();
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                Transform window = root.transform.Find("Window");
                RectTransform frame = window ? (RectTransform)window.Find("Frame") : null;

                if (!frame)
                {
                    Debug.LogError($"[{nameof(TowerScreenSketchSetup)}] {PrefabPath} has no Window/Frame.");
                    return;
                }

                foreach (string name in new[] { "KeyF", "InstallHint", "KeyESC", "CloseHint" })
                {
                    Transform child = frame.Find(name);

                    if (child)
                    {
                        Object.DestroyImmediate(child.gameObject);
                    }
                }

                foreach (string name in new[] { "CloseKey", "CloseLabel" })
                {
                    Transform child = window.Find(name);

                    if (child)
                    {
                        Object.DestroyImmediate(child.gameObject);
                    }
                }

                PlaceFrameRight(frame);
                AddCloseHint(window);
                RectTransform prompt = (RectTransform)root.transform.Find("Prompt");

                if (prompt)
                {
                    HudSketchLayout.BuildPrompt(prompt, PromptCaption, 1f);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[{nameof(TowerScreenSketchSetup)}] Updated the layout of {PrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Lighthouse/Tower/Use Sketch Screen In Open Scene")]
        public static void UseInOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();

            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.path.StartsWith("Assets/00.Scenes/Jihun/"))
            {
                Debug.LogError($"[{nameof(TowerScreenSketchSetup)}] Open a Jihun lighthouse scene in edit mode.");
                return;
            }

            TowerNet[] towers = Object.FindObjectsByType<TowerNet>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            InteractionTarget target = towers.Length == 1 ? towers[0].GetComponent<InteractionTarget>() : null;

            if (!target || !prefab)
            {
                Debug.LogError($"[{nameof(TowerScreenSketchSetup)}] Expected one lighthouse with an interaction target and a built sketch prefab.");
                return;
            }

            TowerScreen screen = null;

            foreach (TowerScreen existing in Object.FindObjectsByType<TowerScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == prefab)
                {
                    screen = existing;
                }
            }

            if (!screen)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Add lighthouse install screen");
                screen = instance.GetComponent<TowerScreen>();
            }

            SerializedObject links = new SerializedObject(screen);
            Set(links, "_tower", towers[0]);
            Set(links, "_target", target);
            links.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = screen.gameObject;
            Debug.Log($"[{nameof(TowerScreenSketchSetup)}] Install screen connected. Save the scene to keep it.");
        }

        // Rows run top-down from the highest floor, like the sketch, each with a label and a line along the body.
        private static Text[] BuildFloors(Transform parent, int floorCount)
        {
            var labels = new Text[floorCount];

            for (int floor = 0; floor < floorCount; floor++)
            {
                float top = RowTop + (floorCount - 1 - floor) * RowHeight;
                float left = BodyLeft(top) + 12f;
                float right = CardX * 2f + 480f * CardScale - left;
                Picture($"Line{floor + 1}", parent, TowerFolder, "tw_floor_line", left, top - 3f, right - left, 6f);
                labels[floor] = Label($"Floor{floor + 1}", parent, string.Empty, 22, 700f, top + RowHeight * 0.5f - 22f, 100f, 44f, TextAnchor.MiddleRight);
            }

            return labels;
        }

        private static TowerSlotView[] BuildSlots(Transform parent, TowerLayout layout)
        {
            var slots = new TowerSlotView[layout.SlotCount];
            int column = 0;

            for (int i = 0; i < slots.Length; i++)
            {
                int floor = layout[i].Floor;
                column = i > 0 && layout[i - 1].Floor == floor ? column + 1 : 0;
                float top = RowTop + (layout.FloorCount - 1 - floor) * RowHeight + (RowHeight - SlotSize) * 0.5f;
                Image frame = Picture($"Slot{i + 1}", parent, SketchFolder, "ws_slot", SlotX + column * SlotStep, top, SlotSize, SlotSize);
                Image icon = Picture("Icon", frame.transform, null, null, 10f, 10f, SlotSize - 20f, SlotSize - 20f);
                icon.preserveAspect = true;
                Image padlock = Picture("Lock", frame.transform, TowerFolder, "tw_lock", SlotSize - 26f, SlotSize - 30f, 24f, 28f);
                RectTransform here = Rect("Here", frame.transform, SlotSize + 4f, 0f, 70f, SlotSize);
                Picture("Arrow", here, TowerFolder, "tw_arrow_here", 0f, 34f, 44f, 30f);
                Label("Text", here, "여기", 18, 14f, 6f, 56f, 26f, TextAnchor.MiddleLeft);
                TowerSlotView view = frame.gameObject.AddComponent<TowerSlotView>();
                SerializedObject serialized = new SerializedObject(view);
                Set(serialized, "_frame", frame);
                Set(serialized, "_icon", icon);
                Set(serialized, "_lock", padlock.gameObject);
                Set(serialized, "_hereMarker", here.gameObject);
                Set(serialized, "_normalSprite", Load(SketchFolder, "ws_slot"));
                Set(serialized, "_highlightSprite", Load(SketchFolder, "ws_slot_selected"));
                serialized.ApplyModifiedPropertiesWithoutUndo();
                padlock.gameObject.SetActive(false);
                here.gameObject.SetActive(false);
                slots[i] = view;
            }

            return slots;
        }

        // Left edge of the tower body at a screen height, from the trapezoid drawn in tw_card_tower (182 to 772 of 780).
        private static float BodyLeft(float screenY)
        {
            float local = Mathf.Clamp((screenY - CardY) / CardScale, 182f, 772f);
            float edge = 126f - (126f - 14f) * (local - 182f) / (772f - 182f);
            return CardX + edge * CardScale;
        }

        private static void SetRecipeIcons(SerializedObject serialized)
        {
            var recipes = new List<ModuleRecipe>();

            foreach (string guid in AssetDatabase.FindAssets("t:ModuleRecipe"))
            {
                ModuleRecipe recipe = AssetDatabase.LoadAssetAtPath<ModuleRecipe>(AssetDatabase.GUIDToAssetPath(guid));

                if (recipe && RecipeSprites.ContainsKey(recipe.name))
                {
                    recipes.Add(recipe);
                }
            }

            SerializedProperty icons = serialized.FindProperty("_recipeIcons");
            icons.arraySize = recipes.Count;

            for (int i = 0; i < recipes.Count; i++)
            {
                SerializedProperty item = icons.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("Recipe").objectReferenceValue = recipes[i];
                item.FindPropertyRelative("Icon").objectReferenceValue = Load(SketchFolder, RecipeSprites[recipes[i].name]);
            }
        }

        // The workbench sketch builder imports its own folder; only the new tower sprites need settings here.
        private static void ImportSprites()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TowerFolder.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
        }

        // The content spans 651..1328 of the sketch; anchored to the right edge it ends a margin short of it,
        // so wider screens keep it beside the lighthouse rather than drifting to the middle.
        private static void PlaceFrameRight(RectTransform frame)
        {
            const float contentRight = 1328f;
            const float frameRightMargin = 130f;
            frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(1f, 0.5f);
            frame.anchoredPosition = new Vector2(1672f - contentRight - frameRightMargin, 0f);
        }

        // The in-game HUD's Esc row, same art and place (its canvas uses these mockup pixels too), since the HUD is
        // hidden while this screen is open.
        private static void AddCloseHint(Transform window)
        {
            HudSketchLayout.KeyRow(0, HudSketchLayout.ExitKeyWidth, out Rect key, out Rect label);
            Image keyImage = Picture("CloseKey", window, HudFolder, HudSketchLayout.ExitKeySprite, 0f, 0f, key.width, key.height);
            keyImage.preserveAspect = true;
            PlaceTopRight(keyImage.rectTransform, key);
            RectTransform caption = Rect("CloseLabel", window, 0f, 0f, label.width, label.height);
            PlaceTopRight(caption, label);
            HudSketchLayout.Caption(caption, HudSketchLayout.ExitCaption, HudSketchLayout.KeyCaptionSize);
        }

        private static void PlaceTopRight(RectTransform rect, Rect place)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(place.x - 1672f, -place.y);
        }

        private static Image Keycap(Transform parent, string key, float x, float y, float width)
        {
            Image image = Picture($"Key{key}", parent, SketchFolder, "ws_key", x, y, width, width < 40f ? width : 40f);
            Label("Text", image.transform, key, key.Length > 1 ? 18 : 20, 1f, 0f, width - 2f, image.rectTransform.sizeDelta.y - 2f);
            return image;
        }

        private static Text Label(string name, Transform parent, string caption, int size, float x, float y, float width, float height, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            RectTransform rect = Rect(name, parent, x, y, width, height);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = Cream;
            text.alignment = alignment;
            text.text = caption;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Image Picture(string name, Transform parent, string folder, string sprite, float x, float y, float width, float height)
        {
            RectTransform rect = Rect(name, parent, x, y, width, height);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite == null ? null : Load(folder, sprite);
            image.type = image.sprite && image.sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Sprite Load(string folder, string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}{name}.png");

            if (!sprite)
            {
                throw new System.InvalidOperationException($"Missing sketch sprite: {folder}{name}");
            }

            return sprite;
        }

        private static void Set(SerializedObject serialized, string name, Object value)
        {
            SerializedProperty property = serialized.FindProperty(name);

            if (property == null)
            {
                throw new System.InvalidOperationException($"Missing screen field: {name}");
            }

            property.objectReferenceValue = value;
        }

        private static void SetArray(SerializedObject serialized, string name, Object[] values)
        {
            SerializedProperty property = serialized.FindProperty(name);
            property.arraySize = values.Length;

            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }
    }
}
