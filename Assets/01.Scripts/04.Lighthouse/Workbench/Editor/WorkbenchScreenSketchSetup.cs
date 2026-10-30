using System.Collections.Generic;
using Lighthouse.Dev.Tower;
using Lighthouse.Interaction.Targets.Net;
using Lighthouse.Workbench.Net;
using Lighthouse.Workbench.Rules;
using Lighthouse.Workbench.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.Editor
{
    public static class WorkbenchScreenSketchSetup
    {
        private const string PrefabPath = "Assets/02.Prefabs/UI/Workbench/WorkbenchScreen_Sketch.prefab";
        private const string SourcePath = "Assets/02.Prefabs/UI/Workbench/WorkbenchScreen_Wood.prefab";
        private const string SpriteFolder = "Assets/03.Images/WorkbenchUiSketch/";
        public const string PromptCaption = "작업대 사용";
        private const float FramePixelsPerUnit = 160f;
        private static readonly Color Cream = new Color(0.98f, 0.92f, 0.79f);
        private static readonly Color Muted = new Color(0.72f, 0.67f, 0.56f);
        private static readonly Color Gold = new Color(1f, 0.78f, 0.27f);
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

        [MenuItem("Lighthouse/Workbench/Build Sketch Screen Prefab")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"[{nameof(WorkbenchScreenSketchSetup)}] Stop play mode before building the screen.");
                return;
            }

            ImportSprites();
            var previousScene = EditorSceneManager.GetActiveScene();
            var buildScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(buildScene);
            GameObject root = new GameObject("WorkbenchScreen_Sketch", typeof(RectTransform), typeof(Canvas),
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
                // Expand keeps the whole sketch area on screen at 16:10 and ultrawide alike.
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

                RectTransform window = Rect("Window", root.transform, 0f, 0f, 1672f, 941f);
                Stretch(window);
                Image scrim = window.gameObject.AddComponent<Image>();
                scrim.color = new Color(0f, 0f, 0f, 0.15f);
                RectTransform frame = Rect("Frame", window, 0f, 0f, 1672f, 941f);
                frame.anchorMin = frame.anchorMax = frame.pivot = new Vector2(0.5f, 0.5f);
                frame.anchoredPosition = Vector2.zero;

                WorkbenchRecipeCard card = BuildRecipes(frame, out Text section);
                Image progress = Picture("Progress", frame, "ws_panel_craft", 627f, 237f, 373f, 467f);
                Image hero = Picture("BigIcon", progress.transform, null, 126f, 29f, 168f, 122f);
                hero.preserveAspect = true;
                Text title = Label("Title", progress.transform, "모듈", 24, 23f, 161f, 327f, 38f);
                Text kind = Label("Kind", progress.transform, string.Empty, 14, 30f, 202f, 313f, 24f);
                kind.color = Muted;
                kind.gameObject.SetActive(false);
                Text empty = Label("Empty", progress.transform, "레시피를 선택하세요", 22, 24f, 145f, 325f, 80f);
                WorkbenchIngredientRow row = BuildIngredients(progress.transform);
                Image track = Picture("ProgressTrack", progress.transform, "ws_bar_track", 24f, 337f, 322f, 26f);
                Image fill = Picture("Fill", track.transform, "ws_bar_fill", 4f, 4f, 314f, 18f);
                Stretch((RectTransform)fill.transform, 4f);
                // The mafia's withdraw sits between deposit and craft (2026-10-27). A row layout shares the width, so on
                // citizens' screens, where it is hidden, the two buttons stand as before. Keys ride on their buttons.
                RectTransform actions = Rect("Actions", progress.transform, 24f, 378f, 322f, 48f);
                HorizontalLayoutGroup actionRow = actions.gameObject.AddComponent<HorizontalLayoutGroup>();
                actionRow.spacing = 14f;
                actionRow.childControlWidth = true;
                actionRow.childControlHeight = true;
                actionRow.childForceExpandWidth = true;
                actionRow.childForceExpandHeight = true;
                Button deposit = MakeActionButton("Deposit", actions, "투입", "ws_button_active", "E", true);
                Button withdraw = MakeActionButton("Withdraw", actions, "빼기", "ws_button", "R", false);
                Button craft = MakeActionButton("Craft", actions, "제작", "ws_button_craft", "F", true);
                // The mask from the mafia's identity screen marks the button as theirs alone.
                Image mafiaMark = Picture("MafiaMark", withdraw.transform, "ws_icon_mafia_mask", 0f, 0f, 34f, 34f);
                mafiaMark.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                mafiaMark.rectTransform.anchoredPosition = new Vector2(4f, -4f);
                withdraw.gameObject.SetActive(false);

                Image shelf = Picture("Stock", frame, "ws_panel_shelf", 1023f, 273f, 305f, 397f);
                Label("Title", shelf.transform, "선반", 24, 22f, 20.4f, 100f, 36f, TextAnchor.MiddleLeft);
                // Not in the sketch, but six slots cannot show all eight modules without paging.
                // Placed from the shelf's top-right corner as the user laid them out in JH04 (2026-10-27). The screen swaps
                // in the OS font when it runs, which is wider than the editor's and draws "‹ ›" off centre, so the arrows
                // use "〈 〉" and the page count may run past its box instead of being cut.
                Button previous = MakeButton("PreviousPage", shelf.transform, "〈", "ws_button", 0f, 0f, 26f, 24f, 16);
                PlaceFromTopRight(previous.transform, -138f, -25f);
                Text page = Label("Page", shelf.transform, "1 / 1", 20, 0f, 0f, 44f, 24f);
                page.horizontalOverflow = HorizontalWrapMode.Overflow;
                PlaceFromTopRight(page.transform, -102.2f, -26.4f);
                Button next = MakeButton("NextPage", shelf.transform, "〉", "ws_button", 0f, 0f, 26f, 24f, 16);
                PlaceFromTopRight(next.transform, -50f, -25f);
                WorkbenchItemSlot[] slots = BuildStoredSlots(shelf.transform);
                Button take = MakeButton("Take", shelf.transform, "가져가기", "ws_button_outline_active", 24f, 274f, 104f, 42f, 17);
                Button putBack = MakeButton("Return", shelf.transform, "되돌려놓기", "ws_button_outline", 153f, 274f, 128f, 42f, 17);
                Keycap(shelf.transform, "E", 61f, 319f);
                Keycap(shelf.transform, "G", 202f, 319f);
                Text carryText = Label("CarryText", shelf.transform, "인벤토리 모듈: 없음", 16, 22f, 354f, 261f, 28f);
                carryText.resizeTextForBestFit = true;
                carryText.resizeTextMinSize = 12;
                carryText.resizeTextMaxSize = 16;

                // The carried module card stays on screen while a module is carried, also with the window closed, so it
                // sits outside the window, pinned to the bottom centre of the screen like the open prompt above it
                // (2026-10-27). The sketch's empty "G" slot beside it went, as the shelf's put-back button does that.
                Image carry = Picture("CarriedModule", root.transform, "ws_card", 0f, 0f, 90f, 90f);
                RectTransform carryRect = carry.rectTransform;
                carryRect.anchorMin = carryRect.anchorMax = carryRect.pivot = new Vector2(0.5f, 0f);
                carryRect.anchoredPosition = new Vector2(0f, 43f);
                Image carryIcon = Picture("Icon", carry.transform, null, 15f, 12f, 60f, 62f);
                carryIcon.preserveAspect = true;
                Text message = Label("Message", frame, string.Empty, 16, 627f, 712f, 373f, 30f);
                message.resizeTextForBestFit = true;
                message.resizeTextMinSize = 12;
                message.resizeTextMaxSize = 16;
                Outline messageOutline = message.gameObject.AddComponent<Outline>();
                messageOutline.effectColor = new Color(0.12f, 0.09f, 0.05f, 0.85f);
                messageOutline.effectDistance = new Vector2(1.5f, -1.5f);
                // Keycap + TMP caption without a backing panel, like every station's prompt (2026-10-31).
                RectTransform prompt = Rect("Prompt", root.transform, 0f, 0f, 190f, 48f);
                prompt.anchorMin = prompt.anchorMax = new Vector2(0.5f, 0f);
                prompt.pivot = new Vector2(0.5f, 0f);
                // Above the carried module card, which sits at the bottom centre (2026-10-27).
                prompt.anchoredPosition = new Vector2(0f, 150f);
                HudSketchLayout.BuildPrompt(prompt, PromptCaption, 1f);

                WorkbenchScreen screen = root.AddComponent<WorkbenchScreen>();
                SerializedObject serialized = new SerializedObject(screen);
                Set(serialized, "_window", window.gameObject);
                Set(serialized, "_prompt", prompt.gameObject);
                Set(serialized, "_message", message);
                Set(serialized, "_cardTemplate", card);
                Set(serialized, "_rowTemplate", row);
                Set(serialized, "_progressTitle", title);
                Set(serialized, "_progressEmpty", empty.gameObject);
                Set(serialized, "_progressHint", null);
                Set(serialized, "_progressIcon", hero);
                Set(serialized, "_progressKind", kind);
                Set(serialized, "_craftButton", craft);
                Set(serialized, "_depositButton", deposit);
                Set(serialized, "_withdrawButton", withdraw);
                Set(serialized, "_takeButton", take);
                Set(serialized, "_overallFill", fill.rectTransform);
                Set(serialized, "_carryBar", carry.gameObject);
                Set(serialized, "_carryIcon", carryIcon);
                Set(serialized, "_carryText", carryText);
                Set(serialized, "_returnButton", putBack);
                Set(serialized, "_previousStockPageButton", previous);
                Set(serialized, "_nextStockPageButton", next);
                Set(serialized, "_stockPageText", page);
                Set(serialized, "_sectionHeaderTemplate", section);
                Set(serialized, "_recipeFallbackIcon", Load("ws_icon_frame"));
                serialized.FindProperty("_isSketchLayout").boolValue = true;
                serialized.FindProperty("_messageInfoColor").colorValue = Cream;
                serialized.FindProperty("_resourceSlots").arraySize = 0;
                SerializedProperty stored = serialized.FindProperty("_storedSlots");
                stored.arraySize = slots.Length;

                for (int i = 0; i < slots.Length; i++)
                {
                    stored.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
                }

                // Esc closes the screen; the sketch has no close button.
                serialized.FindProperty("_closeButtons").arraySize = 0;
                CopyResourceIcons(serialized);
                SetRecipeIcons(serialized);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                window.gameObject.SetActive(false);
                carry.gameObject.SetActive(false);
                prompt.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[{nameof(WorkbenchScreenSketchSetup)}] Built {PrefabPath}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(buildScene, true);
            }
        }

        [MenuItem("Lighthouse/Workbench/Use Sketch Screen In Open Scene")]
        public static void UseInOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();

            if (EditorApplication.isPlayingOrWillChangePlaymode || !scene.path.StartsWith("Assets/00.Scenes/Jihun/"))
            {
                Debug.LogError($"[{nameof(WorkbenchScreenSketchSetup)}] Open a Jihun workbench scene in edit mode.");
                return;
            }

            WorkbenchNet[] benches = Object.FindObjectsByType<WorkbenchNet>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (benches.Length != 1 || !prefab)
            {
                Debug.LogError($"[{nameof(WorkbenchScreenSketchSetup)}] Expected one workbench and a built sketch prefab.");
                return;
            }

            WorkbenchScreen[] screens = Object.FindObjectsByType<WorkbenchScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            WorkbenchScreen sketch = null;

            foreach (WorkbenchScreen existing in screens)
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == prefab)
                {
                    sketch = existing;
                }
                else
                {
                    Undo.RecordObject(existing.gameObject, "Replace workbench screen");
                    existing.gameObject.SetActive(false);
                }
            }

            if (!sketch)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Add sketch workbench screen");
                sketch = instance.GetComponent<WorkbenchScreen>();
            }

            SerializedObject links = new SerializedObject(sketch);
            Set(links, "_workbench", benches[0]);
            Set(links, "_target", benches[0].GetComponent<InteractionTarget>());
            links.ApplyModifiedProperties();
            sketch.gameObject.SetActive(true);

            foreach (TowerTrialPanel panel in Object.FindObjectsByType<TowerTrialPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                SerializedObject trial = new SerializedObject(panel);
                Set(trial, "_workbenchScreen", sketch.gameObject);
                trial.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = sketch.gameObject;
            Debug.Log($"[{nameof(WorkbenchScreenSketchSetup)}] Sketch screen connected. Save the scene to keep it.");
        }

        private static WorkbenchRecipeCard BuildRecipes(Transform parent, out Text section)
        {
            RectTransform viewport = Rect("Recipes", parent, 500f, 150f, 90f, 600f);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform list = Rect("List", viewport, 0f, 0f, 86f, 600f);
            VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = list.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = list;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;
            section = Label("SectionHeader", list, "등대 모듈", 16, 0f, 0f, 86f, 22f, TextAnchor.MiddleLeft);
            section.resizeTextForBestFit = true;
            section.resizeTextMinSize = 12;
            section.resizeTextMaxSize = 16;
            section.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f;
            // Sits on the open scene rather than a panel, so it needs its own edge on bright skies.
            Outline outline = section.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.09f, 0.05f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            section.gameObject.SetActive(false);
            Image cardImage = Picture("RecipeCard", list, "ws_card", 0f, 0f, 84f, 61f);
            cardImage.raycastTarget = true;
            cardImage.gameObject.AddComponent<LayoutElement>().preferredHeight = 61f;
            Button button = cardImage.gameObject.AddComponent<Button>();
            button.targetGraphic = cardImage;
            button.transition = Selectable.Transition.None;
            CanvasGroup group = cardImage.gameObject.AddComponent<CanvasGroup>();
            Image icon = Picture("Icon", cardImage.transform, "ws_icon_frame", 18f, 6f, 66f, 53f);
            icon.rectTransform.localScale = Vector3.one * 0.9f;
            icon.preserveAspect = true;
            Text name = Label("Name", cardImage.transform, string.Empty, 14, 0f, 0f, 84f, 20f);
            Text detail = Label("Detail", cardImage.transform, string.Empty, 12, 0f, 0f, 84f, 20f);
            Image badge = Picture("Badge", cardImage.transform, null, 58f, 0f, 26f, 20f);
            Text badgeText = Label("Text", badge.transform, string.Empty, 11, 0f, 0f, 26f, 20f);
            badge.color = Color.clear;
            badgeText.color = Color.clear;
            name.gameObject.SetActive(false);
            detail.gameObject.SetActive(false);
            badge.gameObject.SetActive(false);
            WorkbenchRecipeCard card = cardImage.gameObject.AddComponent<WorkbenchRecipeCard>();
            SerializedObject serialized = new SerializedObject(card);
            Set(serialized, "_button", button);
            Set(serialized, "_background", cardImage);
            Set(serialized, "_icon", icon);
            Set(serialized, "_name", name);
            Set(serialized, "_detail", detail);
            Set(serialized, "_badge", badge.gameObject);
            Set(serialized, "_badgeText", badgeText);
            Set(serialized, "_group", group);
            Set(serialized, "_normalSprite", Load("ws_card"));
            Set(serialized, "_selectedSprite", Load("ws_card_selected"));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            card.gameObject.SetActive(false);
            return card;
        }

        private static WorkbenchIngredientRow BuildIngredients(Transform parent)
        {
            RectTransform rows = Rect("Rows", parent, 26f, 205f, 322f, 122f);
            HorizontalLayoutGroup layout = rows.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            Image cell = Picture("IngredientRow", rows, "ws_slot", 0f, 0f, 100f, 120f);
            cell.gameObject.AddComponent<LayoutElement>().preferredWidth = 100f;
            Image selection = Picture("Selection", cell.transform, "ws_slot_selected", 0f, 0f, 100f, 120f);
            Stretch(selection.rectTransform);
            Image icon = Picture("Icon", cell.transform, null, 15f, 9f, 70f, 67f);
            icon.preserveAspect = true;
            Text count = Label("Count", cell.transform, "0 / 0", 23, 4f, 77f, 92f, 30f);
            Text held = Label("Held", cell.transform, string.Empty, 11, 4f, 103f, 92f, 16f);
            held.color = Muted;
            held.gameObject.SetActive(false);
            Text name = Label("Name", cell.transform, string.Empty, 12, 0f, 0f, 100f, 20f);
            name.gameObject.SetActive(false);
            Image bar = Picture("UnusedFill", cell.transform, null, 0f, 0f, 1f, 1f);
            bar.color = Color.clear;
            Text done = Label("Done", cell.transform, "✓", 14, 78f, 4f, 18f, 20f);
            done.color = Gold;
            Button select = HitArea("Select", cell.transform);
            Button one = HitArea("AddOne", cell.transform);
            Button all = HitArea("AddAll", cell.transform);
            one.gameObject.SetActive(false);
            all.gameObject.SetActive(false);
            WorkbenchIngredientRow row = cell.gameObject.AddComponent<WorkbenchIngredientRow>();
            SerializedObject serialized = new SerializedObject(row);
            Set(serialized, "_icon", icon);
            Set(serialized, "_name", name);
            Set(serialized, "_fill", bar.rectTransform);
            Set(serialized, "_count", count);
            Set(serialized, "_held", held);
            Set(serialized, "_doneMark", done.gameObject);
            Set(serialized, "_addOneButton", one);
            Set(serialized, "_addAllButton", all);
            Set(serialized, "_selectButton", select);
            Set(serialized, "_selection", selection);
            serialized.FindProperty("_isShortageTinted").boolValue = true;
            serialized.FindProperty("_shortageColor").colorValue = new Color(0.92f, 0.35f, 0.27f);
            serialized.FindProperty("_sufficientColor").colorValue = Cream;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            cell.gameObject.SetActive(false);
            return row;
        }

        private static WorkbenchItemSlot[] BuildStoredSlots(Transform parent)
        {
            var slots = new WorkbenchItemSlot[6];

            for (int i = 0; i < slots.Length; i++)
            {
                Image cell = Picture($"Slot{i + 1}", parent, "ws_slot_shelf", 17f + (i % 3) * 93f, 65f + (i / 3) * 100f, 86f, 88f);
                Image selection = Picture("Selection", cell.transform, "ws_slot_selected", 0f, 0f, 86f, 88f);
                Stretch(selection.rectTransform);
                Image icon = Picture("Icon", cell.transform, null, 12f, 12f, 62f, 62f);
                icon.preserveAspect = true;
                Text count = Label("Count", cell.transform, string.Empty, 15, 57f, 3f, 24f, 22f, TextAnchor.UpperRight);
                Text label = Label("Label", cell.transform, string.Empty, 12, 3f, 30f, 80f, 28f);
                Button select = HitArea("Select", cell.transform);
                WorkbenchItemSlot slot = cell.gameObject.AddComponent<WorkbenchItemSlot>();
                SerializedObject serialized = new SerializedObject(slot);
                Set(serialized, "_icon", icon);
                Set(serialized, "_count", count);
                Set(serialized, "_label", label);
                Set(serialized, "_actionButton", select);
                Set(serialized, "_selection", selection);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                slots[i] = slot;
            }

            return slots;
        }

        private static void CopyResourceIcons(SerializedObject destination)
        {
            WorkbenchScreen source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath).GetComponent<WorkbenchScreen>();
            SerializedObject sourceFields = new SerializedObject(source);
            destination.CopyFromSerializedProperty(sourceFields.FindProperty("_resourceIcons"));
            destination.CopyFromSerializedProperty(sourceFields.FindProperty("_towerLayout"));
            SerializedProperty icons = destination.FindProperty("_resourceIcons");

            for (int i = 0; i < icons.arraySize; i++)
            {
                SerializedProperty item = icons.GetArrayElementAtIndex(i);
                Object resource = item.FindPropertyRelative("Resource").objectReferenceValue;
                string name = resource ? resource.name.ToLowerInvariant() : string.Empty;
                string sprite = name.Contains("glass") ? "ws_icon_glass" : name.Contains("iron") ? "ws_icon_iron"
                    : name.Contains("crystal") ? "ws_icon_crystal" : name.Contains("wood") ? "ws_icon_wood"
                    : name.Contains("stone") ? "ws_icon_stone" : name.Contains("fiber") ? "ws_icon_fiber" : null;

                if (sprite != null)
                {
                    item.FindPropertyRelative("Icon").objectReferenceValue = Load(sprite);
                }
            }
        }

        private static void SetRecipeIcons(SerializedObject serialized)
        {
            FillRecipeIcons(serialized.FindProperty("_recipeIcons"));
        }

        // Shared with the in-game HUD's carried module card, so both show the same icon. The property is an array of
        // { Recipe, Icon } like WorkbenchScreen's.
        public static void FillRecipeIcons(SerializedProperty icons)
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

            icons.arraySize = recipes.Count;

            for (int i = 0; i < recipes.Count; i++)
            {
                SerializedProperty item = icons.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("Recipe").objectReferenceValue = recipes[i];
                item.FindPropertyRelative("Icon").objectReferenceValue = Load(RecipeSprites[recipes[i].name]);
            }
        }

        private static void ImportSprites()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteFolder.TrimEnd('/') }))
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
                importer.maxTextureSize = 1024;
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                float border = SpriteBorder(name);
                importer.spriteBorder = new Vector4(border, border, border, border);
                // Frames are cut at 2x; 160 keeps their sliced edges at the sketch's line weight.
                importer.spritePixelsPerUnit = border > 0f && name != "ws_button" ? FramePixelsPerUnit : 100f;
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        private static float SpriteBorder(string name)
        {
            // Old single-scale frames kept for the page arrows.
            if (name == "ws_button")
            {
                return 12f;
            }

            return name.Contains("panel") ? 34f : name.Contains("slot") || name.Contains("card") ? 24f
                : name.Contains("button") || name.Contains("banner") ? 22f : name.Contains("track") ? 20f
                : name == "ws_key" ? 18f : 0f;
        }

        private static Button MakeButton(string name, Transform parent, string caption, string sprite, float x, float y, float width, float height, int fontSize, bool isFilled = false)
        {
            Image image = Picture(name, parent, sprite, x, y, width, height);
            image.raycastTarget = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.88f, 0.6f);
            colors.pressedColor = new Color(0.8f, 0.66f, 0.35f);
            colors.disabledColor = new Color(0.5f, 0.48f, 0.43f, 0.75f);
            button.colors = colors;
            // Best fit only shrinks: the sketch keeps captions well inside their frames.
            float padding = width < 40f ? 1f : 8f;
            Text text = Label("Text", image.transform, caption, fontSize, padding, 1f, width - padding * 2f, height - 2f);
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 11;
            text.resizeTextMaxSize = fontSize;
            text.color = isFilled ? new Color(0.12f, 0.09f, 0.05f) : Cream;
            return button;
        }

        private static Button HitArea(string name, Transform parent)
        {
            RectTransform rect = Rect(name, parent, 0f, 0f, 1f, 1f);
            Stretch(rect);
            Image hit = rect.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            return button;
        }

        private static void PlaceFromTopRight(Transform item, float x, float y)
        {
            RectTransform rect = (RectTransform)item;
            rect.anchorMin = rect.anchorMax = Vector2.one;
            rect.anchoredPosition = new Vector2(x, y);
            rect.localScale = Vector3.one * 1.1f;
        }

        // A button sized by its row layout, its caption filling it and its key just under its bottom edge.
        private static Button MakeActionButton(string name, Transform parent, string caption, string sprite, string key, bool isFilled)
        {
            Button button = MakeButton(name, parent, caption, sprite, 0f, 0f, 100f, 48f, 21, isFilled);
            Stretch((RectTransform)button.transform.Find("Text"), 4f);
            Image keycap = Picture($"Key{key}", button.transform, "ws_key", 0f, 0f, 32f, 32f);
            Label("Text", keycap.transform, key, 19, 1f, 0f, 30f, 30f);
            RectTransform keyRect = keycap.rectTransform;
            keyRect.anchorMin = keyRect.anchorMax = new Vector2(0.5f, 0f);
            keyRect.pivot = new Vector2(0.5f, 1f);
            keyRect.anchoredPosition = new Vector2(0f, 6f);
            return button;
        }

        private static void Keycap(Transform parent, string key, float x, float y)
        {
            Image image = Picture($"Key{key}", parent, "ws_key", x, y, 32f, 32f);
            Label("Text", image.transform, key, 19, 1f, 0f, 30f, 30f);
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

        private static Image Picture(string name, Transform parent, string sprite, float x, float y, float width, float height)
        {
            RectTransform rect = Rect(name, parent, x, y, width, height);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite == null ? null : Load(sprite);
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

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = Vector2.one * -inset;
        }

        private static Sprite Load(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}{name}.png");

            if (!sprite)
            {
                throw new System.InvalidOperationException($"Missing sketch sprite: {name}");
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
    }
}
