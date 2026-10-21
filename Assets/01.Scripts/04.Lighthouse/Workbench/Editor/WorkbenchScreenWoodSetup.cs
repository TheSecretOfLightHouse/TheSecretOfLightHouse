using System.Collections.Generic;
using Lighthouse.Dev.Tower;
using Lighthouse.Workbench.Rules;
using Lighthouse.Workbench.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.Editor
{
    // Wood and parchment version of the workbench screen (2026-10-21 mockup). The navy prefab stays as it is; this
    // copies it once to WorkbenchScreen_Wood.prefab and restyles the copy with sprites cut from the mockup
    // (Assets/03.Images/WorkbenchUiWood). Running it again restyles the same copy, so its GUID and scene links hold.
    public static class WorkbenchScreenWoodSetup
    {
        private const string SourcePath = "Assets/02.Prefabs/UI/Workbench/WorkbenchScreen.prefab";
        private const string WoodPath = "Assets/02.Prefabs/UI/Workbench/WorkbenchScreen_Wood.prefab";
        private const string SpriteFolder = "Assets/03.Images/WorkbenchUiWood/";
        private const string BoatRecipeFolder = "Assets/02.Prefabs/Lighthouse/Data/Boat/";
        private const float FrameHeight = 960f;
        private const int ResourceCount = 6;

        private static readonly Color Ink = new Color(0.2f, 0.14f, 0.09f);
        private static readonly Color InkSoft = new Color(0.42f, 0.33f, 0.23f);
        private static readonly Color Cream = new Color(0.93f, 0.88f, 0.78f);
        private static readonly Color CreamDim = new Color(0.72f, 0.67f, 0.58f);
        private static readonly Color Gold = new Color(0.93f, 0.8f, 0.58f);

        // Nine-slice borders (left, bottom, right, top) of the cut sprites, as the cutting script made them.
        private static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
        {
            { "ww_window", new Vector4(20, 20, 20, 20) },
            { "ww_panel", new Vector4(16, 16, 16, 16) },
            { "ww_parchment", new Vector4(36, 36, 36, 36) },
            { "ww_card", new Vector4(8, 8, 8, 8) },
            { "ww_card_selected", new Vector4(8, 8, 8, 8) },
            { "ww_slot", new Vector4(12, 12, 12, 12) },
            { "ww_carrybar", new Vector4(12, 12, 12, 12) },
            { "ww_message", new Vector4(14, 14, 14, 14) },
            { "ww_button", new Vector4(10, 10, 10, 10) },
            { "ww_bar_fill", new Vector4(8, 6, 8, 6) },
            { "ww_bar_track", new Vector4(8, 6, 8, 6) },
            { "ww_tab", new Vector4(16, 8, 16, 8) },
            { "ww_pill", new Vector4(14, 13, 14, 13) },
            { "ww_iconbox", new Vector4(10, 10, 10, 10) }
        };

        // Boat recipe asset -> its icon cut from the mockup.
        private static readonly string[,] BoatIcons =
        {
            { "ModuleRecipe_BoatSearchlight", "ww_icon_boat_searchlight" },
            { "ModuleRecipe_BoatDash", "ww_icon_boat_dash" },
            { "ModuleRecipe_BoatObstacleAlarm", "ww_icon_boat_alarm" },
            { "ModuleRecipe_BoatStabilizer", "ww_icon_boat_stabilizer" }
        };

        [MenuItem("Lighthouse/Workbench/Build Wood Screen Prefab")]
        public static void Build()
        {
            ImportSprites();
            AssignBoatIcons();

            if (!AssetDatabase.LoadAssetAtPath<GameObject>(WoodPath) && !AssetDatabase.CopyAsset(SourcePath, WoodPath))
            {
                Debug.LogError($"[{nameof(WorkbenchScreenWoodSetup)}] Could not copy {SourcePath}.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(WoodPath);

            try
            {
                Restyle(root);
                PrefabUtility.SaveAsPrefabAsset(root, WoodPath);
                Debug.Log($"[{nameof(WorkbenchScreenWoodSetup)}] {WoodPath} built.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Puts the wood screen in the open scene next to the navy one, which is turned off but kept.
        [MenuItem("Lighthouse/Workbench/Use Wood Screen In Open Scene")]
        public static void UseInOpenScene()
        {
            GameObject woodPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WoodPath);

            if (!woodPrefab)
            {
                Debug.LogError($"[{nameof(WorkbenchScreenWoodSetup)}] Build the wood screen prefab first.");
                return;
            }

            WorkbenchScreen navy = null;
            WorkbenchScreen wood = null;

            foreach (WorkbenchScreen screen in Object.FindObjectsByType<WorkbenchScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(screen.gameObject) == woodPrefab)
                {
                    wood = screen;
                }
                else
                {
                    navy = screen;
                }
            }

            if (!navy)
            {
                Debug.LogError($"[{nameof(WorkbenchScreenWoodSetup)}] No workbench screen in the open scene to copy the links from.");
                return;
            }

            if (!wood)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(woodPrefab, navy.gameObject.scene);
                Undo.RegisterCreatedObjectUndo(instance, "Add wood workbench screen");
                instance.transform.SetParent(navy.transform.parent, false);
                instance.transform.SetSiblingIndex(navy.transform.GetSiblingIndex() + 1);
                wood = instance.GetComponent<WorkbenchScreen>();
            }

            SerializedObject navyLinks = new SerializedObject(navy);
            SerializedObject woodLinks = new SerializedObject(wood);
            woodLinks.FindProperty("_workbench").objectReferenceValue = navyLinks.FindProperty("_workbench").objectReferenceValue;
            woodLinks.FindProperty("_target").objectReferenceValue = navyLinks.FindProperty("_target").objectReferenceValue;
            woodLinks.ApplyModifiedProperties();
            wood.gameObject.SetActive(navy.gameObject.activeSelf);
            Undo.RecordObject(navy.gameObject, "Turn off navy workbench screen");
            navy.gameObject.SetActive(false);

            // The trial panel shows whichever screen it holds in screen mode.
            foreach (TowerTrialPanel panel in Object.FindObjectsByType<TowerTrialPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                SerializedObject serialized = new SerializedObject(panel);
                serialized.FindProperty("_workbenchScreen").objectReferenceValue = wood.gameObject;
                serialized.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(wood.gameObject.scene);
            Selection.activeObject = wood.gameObject;
            Debug.Log($"[{nameof(WorkbenchScreenWoodSetup)}] Wood screen in use, navy screen off. Save the scene to keep it.");
        }

        private static void ImportSprites()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteFolder.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.spriteBorder = Borders.TryGetValue(name, out Vector4 border) ? border : Vector4.zero;
                importer.SaveAndReimport();
            }
        }

        private static void AssignBoatIcons()
        {
            for (int i = 0; i < BoatIcons.GetLength(0); i++)
            {
                ModuleRecipe recipe = AssetDatabase.LoadAssetAtPath<ModuleRecipe>($"{BoatRecipeFolder}{BoatIcons[i, 0]}.asset");

                if (!recipe)
                {
                    continue;
                }

                SerializedObject serialized = new SerializedObject(recipe);
                serialized.FindProperty("_icon").objectReferenceValue = Load(BoatIcons[i, 1]);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.SaveAssets();
        }

        private static void Restyle(GameObject root)
        {
            WorkbenchScreen screen = root.GetComponent<WorkbenchScreen>();
            Transform frame = root.transform.Find("Window/Frame");
            SetImage(frame, "ww_window");
            Rect(frame).sizeDelta = new Vector2(Rect(frame).sizeDelta.x, FrameHeight);
            SetImage(root.transform.Find("Prompt"), "ww_message");

            RestyleTitle(frame);
            RestyleRecipes(frame.Find("Recipes"), screen);
            RestyleProgress(frame.Find("Progress"), screen);
            RestyleStock(frame.Find("Stock"));
            RestyleMessage(frame.Find("MessageBar"));

            // The mockup closes with the corner X only.
            Hide(frame.Find("Close"));
            Transform closeX = frame.Find("CloseX");
            SetImage(closeX, "ww_close", false);
            Hide(closeX.Find("Label"));
            Rect(closeX).sizeDelta = new Vector2(62f, 62f);

            SerializedObject serialized = new SerializedObject(screen);
            serialized.FindProperty("_isBadgeTinted").boolValue = true;
            serialized.FindProperty("_messageInfoColor").colorValue = Cream;
            serialized.FindProperty("_lighthouseSectionIcon").objectReferenceValue = Load("ww_glyph_lighthouse");
            serialized.FindProperty("_boatSectionIcon").objectReferenceValue = Load("ww_glyph_boat");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // A plank with the title painted on, hanging over the top left of the frame.
        private static void RestyleTitle(Transform frame)
        {
            Hide(frame.Find("TitleIcon"));
            Hide(frame.Find("Title"));
            Hide(frame.Find("Subtitle"));
            Hide(frame.Find("TitleLine"));

            if (frame.Find("TitlePlank"))
            {
                return;
            }

            RectTransform plank = CreateRect("TitlePlank", frame);
            plank.anchorMin = new Vector2(0f, 1f);
            plank.anchorMax = new Vector2(0f, 1f);
            plank.pivot = new Vector2(0f, 1f);
            plank.sizeDelta = new Vector2(368f, 100f);
            plank.anchoredPosition = new Vector2(8f, 52f);
            Image image = plank.gameObject.AddComponent<Image>();
            image.sprite = Load("ww_title");
            image.raycastTarget = false;
        }

        private static void RestyleRecipes(Transform recipes, WorkbenchScreen screen)
        {
            SetImage(recipes, "ww_panel");
            SetTopPanel(recipes, 24f, 430f, 26f);
            Hide(recipes.Find("Header"));
            RectTransform viewport = Rect(recipes.Find("Viewport"));
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(14f, 14f);
            viewport.offsetMax = new Vector2(-14f, -16f);
            Transform list = viewport.Find("List");

            RectTransform card = Rect(list.Find("RecipeCard"));
            SetImage(card, "ww_card");
            Image iconBox = SetImage(card.Find("IconBox"), "ww_iconbox");
            iconBox.color = new Color(0f, 0f, 0f, 0.18f);
            Rect(card.Find("IconBox")).sizeDelta = new Vector2(78f, 78f);
            Transform badge = card.Find("Badge");
            SetImage(badge, "ww_pill");
            RectTransform badgeRect = Rect(badge);
            badgeRect.pivot = Vector2.one;
            badgeRect.sizeDelta = new Vector2(104f, 28f);
            badgeRect.anchoredPosition = new Vector2(-10f, -10f);

            foreach (Text text in badge.GetComponentsInChildren<Text>(true))
            {
                text.color = Color.white;
                text.fontSize = 16;
            }

            SerializedObject cardSettings = new SerializedObject(card.GetComponent<WorkbenchRecipeCard>());
            cardSettings.FindProperty("_normalSprite").objectReferenceValue = Load("ww_card");
            cardSettings.FindProperty("_selectedSprite").objectReferenceValue = Load("ww_card_selected");
            cardSettings.FindProperty("_normalTextColor").colorValue = Cream;
            cardSettings.FindProperty("_normalDetailColor").colorValue = CreamDim;
            cardSettings.FindProperty("_selectedTextColor").colorValue = Ink;
            cardSettings.FindProperty("_selectedDetailColor").colorValue = InkSoft;
            cardSettings.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject screenLinks = new SerializedObject(screen);
            screenLinks.FindProperty("_sectionHeaderView").objectReferenceValue = BuildSectionHeader(list);
            screenLinks.ApplyModifiedPropertiesWithoutUndo();
        }

        // A short parchment plate with an icon and the title, left aligned in a full-width row.
        private static WorkbenchSectionHeader BuildSectionHeader(Transform list)
        {
            Transform existing = list.Find("SectionPlate");

            if (existing)
            {
                return existing.GetComponent<WorkbenchSectionHeader>();
            }

            Text textSource = list.Find("SectionHeader").GetComponent<Text>();
            RectTransform row = CreateRect("SectionPlate", list);
            row.sizeDelta = new Vector2(0f, 46f);
            RectTransform plate = CreateRect("Plate", row);
            plate.anchorMin = new Vector2(0f, 0.5f);
            plate.anchorMax = new Vector2(0f, 0.5f);
            plate.pivot = new Vector2(0f, 0.5f);
            plate.sizeDelta = new Vector2(190f, 40f);
            plate.anchoredPosition = new Vector2(4f, 0f);
            Image plateImage = plate.gameObject.AddComponent<Image>();
            plateImage.sprite = Load("ww_tab");
            plateImage.type = Image.Type.Sliced;

            RectTransform icon = CreateRect("Icon", plate);
            icon.anchorMin = new Vector2(0f, 0.5f);
            icon.anchorMax = new Vector2(0f, 0.5f);
            icon.sizeDelta = new Vector2(30f, 28f);
            icon.anchoredPosition = new Vector2(30f, 0f);
            Image iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.preserveAspect = true;

            Text label = Object.Instantiate(textSource, plate, false);
            label.name = "Label";
            label.gameObject.SetActive(true);
            label.color = Ink;
            label.fontSize = 22;
            label.alignment = TextAnchor.MiddleLeft;
            RectTransform labelRect = Rect(label.transform);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(52f, 0f);
            labelRect.offsetMax = new Vector2(-10f, 0f);

            WorkbenchSectionHeader view = row.gameObject.AddComponent<WorkbenchSectionHeader>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("_label").objectReferenceValue = label;
            serialized.FindProperty("_icon").objectReferenceValue = iconImage;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            row.gameObject.SetActive(false);
            return view;
        }

        private static void RestyleProgress(Transform progress, WorkbenchScreen screen)
        {
            SetImage(progress, "ww_parchment");
            SetTopPanel(progress, 466f, 560f, 112f);

            // Big icon on the left, the name and where it goes on the right.
            Text title = progress.Find("Header").GetComponent<Text>();
            title.color = Ink;
            title.fontSize = 34;
            RectTransform titleRect = Rect(title.transform);
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(186f, -96f);
            titleRect.offsetMax = new Vector2(-24f, -40f);

            Transform kindTransform = progress.Find("Kind");
            Text kind = kindTransform ? kindTransform.GetComponent<Text>() : Object.Instantiate(title, progress, false);
            kind.name = "Kind";
            kind.color = InkSoft;
            kind.fontSize = 20;
            RectTransform kindRect = Rect(kind.transform);
            kindRect.anchorMin = new Vector2(0f, 1f);
            kindRect.anchorMax = Vector2.one;
            kindRect.offsetMin = new Vector2(186f, -136f);
            kindRect.offsetMax = new Vector2(-24f, -100f);

            Transform iconTransform = progress.Find("BigIcon");
            RectTransform icon = iconTransform ? Rect(iconTransform) : CreateRect("BigIcon", progress);

            if (!iconTransform)
            {
                icon.gameObject.AddComponent<Image>().preserveAspect = true;
            }

            icon.anchorMin = new Vector2(0f, 1f);
            icon.anchorMax = new Vector2(0f, 1f);
            icon.pivot = new Vector2(0f, 1f);
            icon.sizeDelta = new Vector2(140f, 140f);
            icon.anchoredPosition = new Vector2(30f, -24f);

            RectTransform rows = Rect(progress.Find("Rows"));
            rows.anchorMin = Vector2.zero;
            rows.anchorMax = Vector2.one;
            rows.offsetMin = new Vector2(28f, 170f);
            rows.offsetMax = new Vector2(-28f, -184f);

            Text hint = progress.Find("Hint").GetComponent<Text>();
            hint.color = InkSoft;
            RectTransform hintRect = Rect(hint.transform);
            hintRect.anchorMin = Vector2.zero;
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.sizeDelta = new Vector2(-48f, 34f);
            hintRect.anchoredPosition = new Vector2(0f, 120f);
            progress.Find("Empty").GetComponent<Text>().color = InkSoft;

            Transform craft = progress.Find("Craft");
            SetButton(craft, 30);
            Rect(craft).sizeDelta = new Vector2(300f, 72f);
            Rect(craft).anchoredPosition = new Vector2(0f, 30f);

            Transform row = progress.Find("Rows/IngredientRow");
            row.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            row.Find("IconBox").GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            SetImage(row.Find("BarTrack"), "ww_bar_track");

            foreach (Image image in row.Find("BarTrack").GetComponentsInChildren<Image>(true))
            {
                if (image.transform != row.Find("BarTrack"))
                {
                    image.sprite = Load("ww_bar_fill");
                    image.type = Image.Type.Sliced;
                }
            }

            foreach (string name in new[] { "Name", "Count", "Held" })
            {
                row.Find(name).GetComponent<Text>().color = name == "Held" ? InkSoft : Ink;
            }

            SetButton(row.Find("AddOne"), 20);
            SetButton(row.Find("AddAll"), 17);

            SerializedObject serialized = new SerializedObject(screen);
            serialized.FindProperty("_progressIcon").objectReferenceValue = icon.GetComponent<Image>();
            serialized.FindProperty("_progressKind").objectReferenceValue = kind;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // Three wide cells per row with names, so every position below moves down.
        private static void RestyleStock(Transform stock)
        {
            SetImage(stock, "ww_panel");
            SetTopPanel(stock, 1038f, 378f, 26f);
            Text resourceHeader = stock.GetChild(0).GetComponent<Text>();
            Text storedHeader = stock.GetChild(2).GetComponent<Text>();
            resourceHeader.color = Gold;
            storedHeader.color = Gold;
            resourceHeader.fontSize = 24;
            storedHeader.fontSize = 24;

            RectTransform resourceGrid = Rect(stock.Find("ResourceGrid"));
            ResizeGrid(resourceGrid, 104f, 100f, 2);
            Text nameSource = resourceHeader;

            // Six base resources; the grid was built with eight cells.
            for (int i = 0; i < resourceGrid.childCount; i++)
            {
                Transform cell = resourceGrid.GetChild(i);
                SetImage(cell, "ww_slot");
                AddSlotName(cell, nameSource, TextAnchor.UpperLeft);
                cell.gameObject.SetActive(i < ResourceCount);
            }

            SetY(stock.Find("CarryBar"), -284f);
            SetY(stock.Find("CarryNote"), -340f);
            SetY(storedHeader.transform, -362f);
            RectTransform storedGrid = Rect(stock.Find("StoredGrid"));
            SetY(storedGrid, -404f);
            ResizeGrid(storedGrid, 104f, 120f, 3);

            for (int i = 0; i < storedGrid.childCount; i++)
            {
                Transform cell = storedGrid.GetChild(i);
                SetImage(cell, "ww_slot");
                SetButton(cell.Find("Take"), 14);
                cell.Find("Name").GetComponent<Text>().color = Cream;
            }

            Transform bar = stock.Find("CarryBar");
            SetImage(bar, "ww_carrybar");
            bar.Find("Text").GetComponent<Text>().color = Cream;
            SetButton(bar.Find("Return"), 16);
            stock.Find("CarryNote").GetComponent<Text>().color = CreamDim;

            foreach (Text count in stock.GetComponentsInChildren<Text>(true))
            {
                if (count.name == "Count")
                {
                    count.color = Cream;
                }
            }
        }

        private static void RestyleMessage(Transform message)
        {
            SetImage(message, "ww_message");
            RectTransform rect = Rect(message);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = new Vector2(560f, 68f);
            rect.anchoredPosition = new Vector2(466f, 26f);
            Image info = SetImage(message.Find("InfoIcon"), "ww_iconbox");
            info.color = new Color(0.33f, 0.6f, 0.4f);
        }

        // Name on top of a resource cell, the icon a little lower so both fit.
        private static void AddSlotName(Transform cell, Text source, TextAnchor alignment)
        {
            WorkbenchItemSlot slot = cell.GetComponent<WorkbenchItemSlot>();
            SerializedObject serialized = new SerializedObject(slot);

            if (serialized.FindProperty("_name").objectReferenceValue)
            {
                return;
            }

            Text name = Object.Instantiate(source, cell, false);
            name.name = "Name";
            name.text = string.Empty;
            name.fontSize = 16;
            name.color = Cream;
            name.alignment = alignment;
            name.raycastTarget = false;
            RectTransform rect = Rect(name.transform);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-16f, 24f);
            rect.anchoredPosition = new Vector2(0f, -6f);
            RectTransform icon = Rect(cell.Find("Icon"));
            icon.sizeDelta = new Vector2(52f, 52f);
            icon.anchoredPosition = new Vector2(0f, -6f);
            serialized.FindProperty("_name").objectReferenceValue = name;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ResizeGrid(RectTransform grid, float width, float height, int rows)
        {
            GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(width, height);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            grid.sizeDelta = new Vector2(grid.sizeDelta.x, height * rows + layout.spacing.y * (rows - 1));
        }

        // Panels hang from the top of the frame at the same x and width as before, down to bottomMargin.
        private static void SetTopPanel(Transform panel, float x, float width, float bottomMargin)
        {
            RectTransform rect = Rect(panel);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(x, bottomMargin);
            rect.offsetMax = new Vector2(x + width, -112f);
        }

        private static void SetButton(Transform button, int fontSize)
        {
            SetImage(button, "ww_button");

            foreach (Text text in button.GetComponentsInChildren<Text>(true))
            {
                text.color = Ink;
                text.fontSize = fontSize;
            }
        }

        private static Image SetImage(Transform target, string sprite, bool isSliced = true)
        {
            Image image = target.GetComponent<Image>();
            image.sprite = Load(sprite);
            image.type = isSliced ? Image.Type.Sliced : Image.Type.Simple;
            image.color = Color.white;
            return image;
        }

        private static Sprite Load(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}{name}.png");

            if (!sprite)
            {
                Debug.LogWarning($"[{nameof(WorkbenchScreenWoodSetup)}] Missing sprite {name}.");
            }

            return sprite;
        }

        private static void Hide(Transform target)
        {
            if (target)
            {
                target.gameObject.SetActive(false);
            }
        }

        private static void SetY(Transform target, float y)
        {
            RectTransform rect = Rect(target);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
        }

        private static RectTransform Rect(Transform target)
        {
            return (RectTransform)target;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            return rect;
        }
    }
}
