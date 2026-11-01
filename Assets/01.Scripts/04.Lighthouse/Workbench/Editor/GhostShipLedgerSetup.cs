using Lighthouse.Interaction.GhostShipHints.View;
using Lighthouse.Workbench.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.Editor
{
    // Builds the ghost ship ledger (2026-10-31 mockup) from the parts cut out of the user's atlas, on a 1920x1080
    // reference, and hands it to the ghost ship prefab. The board art carries the title, column titles and footer line,
    // and is nine-sliced so only its plain middle stretches with the rows a layout group stacks under the header.
    // Resource icons are copied from the sketch workbench screen so both screens show the same art. The E prompt copies
    // the workbench's (keycap + label art at the bottom centre). Kept in this editor assembly so no new asmdef is needed.
    // Rebuilding overwrites the prefab, so later look changes made by hand belong in the inspector, not here.
    public static class GhostShipLedgerSetup
    {
        private const string PrefabPath = "Assets/02.Prefabs/UI/GhostShip/GhostShipLedger.prefab";
        private const string ShipPrefabPath = "Assets/02.Prefabs/World/GhostShip/GhostShip.prefab";
        private const string WorkbenchPrefabPath = "Assets/02.Prefabs/UI/Workbench/WorkbenchScreen_Sketch.prefab";
        private const string Folder = "Assets/03.Images/GhostShipLedger/";
        private const int DayCount = 5;
        public const string PromptCaption = "유령선 확인";
        // This canvas is 1920 wide where the station screens are 1672, so the prompt grows to look the same size.
        public const float PromptUnit = 1920f / 1672f;
        private const float BoardWidth = 940f;
        // Board art is 847x721. The top border keeps the printed header down to the column-title line and the bottom
        // border keeps the footer line, so only the empty middle stretches (source pixels).
        private const float BoardArtWidth = 847f;
        private const float BoardTop = 320f;
        private const float BoardBottom = 72f;
        private const float BoardSide = 40f;
        private const float Scale = BoardWidth / BoardArtWidth;
        private const float ContentWidth = BoardWidth - 100f;
        // The board prints "사라진 자원" this far right of the content's left edge; the resources line up under it.
        private const float DayColumnWidth = 272f;
        private const int ItemColumns = 3;
        private static readonly Color Cream = new Color(0.96f, 0.9f, 0.76f);
        private static readonly Color Shadow = new Color(0.1f, 0.07f, 0.04f, 0.9f);

        [MenuItem("Lighthouse/UI/Build Ghost Ship Ledger Prefab")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"[{nameof(GhostShipLedgerSetup)}] Stop play mode before building.");
                return;
            }

            ImportSprites();
            var previousScene = EditorSceneManager.GetActiveScene();
            var buildScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(buildScene);
            GameObject root = new GameObject("GhostShipLedger", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.layer = LayerMask.NameToLayer("UI");

            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // The station screens' order: under the notice strip (90), so "already read" still shows over it.
                canvas.sortingOrder = 80;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                GameObject window = BuildWindow(root.transform, out RectTransform rows, out GameObject rowTemplate, out GameObject emptyRow, out Button close);

                // Same spot and parts as the workbench's open prompt; the two never share a screen (sea vs island).
                RectTransform prompt = Rect("Prompt", root.transform, 0f, 0f, 190f, 48f);
                prompt.anchorMin = prompt.anchorMax = new Vector2(0.5f, 0f);
                prompt.pivot = new Vector2(0.5f, 0f);
                prompt.anchoredPosition = new Vector2(0f, 150f);
                HudSketchLayout.BuildPrompt(prompt, PromptCaption, PromptUnit);

                GhostShipLedgerScreen screen = root.AddComponent<GhostShipLedgerScreen>();
                SerializedObject serialized = new SerializedObject(screen);
                Set(serialized, "_prompt", prompt.gameObject);
                Set(serialized, "_window", window);
                Set(serialized, "_rows", rows);
                Set(serialized, "_rowTemplate", rowTemplate);
                Set(serialized, "_emptyRow", emptyRow);
                Set(serialized, "_closeButton", close);
                SetDaySprites(serialized, "_daySprites", "ghl_day_");
                SetDaySprites(serialized, "_dimDaySprites", "ghl_day_dim_");
                CopyResourceIcons(serialized);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssignToShip(prefab.GetComponent<GhostShipLedgerScreen>());
                Debug.Log($"[{nameof(GhostShipLedgerSetup)}] Built {PrefabPath} and assigned it to the ghost ship.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(buildScene, true);
            }
        }

        private static GameObject BuildWindow(Transform parent, out RectTransform rows, out GameObject rowTemplate, out GameObject emptyRow, out Button close)
        {
            RectTransform window = Rect("Window", parent, 0f, 0f, BoardWidth, 600f);
            window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
            window.anchoredPosition = Vector2.zero;
            Image board = window.gameObject.AddComponent<Image>();
            board.sprite = Load(Folder, "ghl_board");
            board.type = Image.Type.Sliced;
            // Draws the sliced frame at the board's on-screen scale instead of the art's full pixel size.
            board.pixelsPerUnitMultiplier = 1f / Scale;
            VerticalLayoutGroup layout = window.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(50, 50, Mathf.RoundToInt(BoardTop * Scale) + 6, Mathf.RoundToInt(BoardBottom * Scale) + 14);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = window.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            rows = Rect("Rows", window, 0f, 0f, ContentWidth, 0f);
            VerticalLayoutGroup rowLayout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.UpperCenter;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;

            RectTransform empty = Rect("EmptyRow", rows, 0f, 0f, ContentWidth, 150f);
            Stacked(empty, -1f, 150f);
            Image emptyArt = Picture("Empty", empty, Folder, "ghl_empty", 0f, 0f, 350f, 44f);
            emptyArt.preserveAspect = true;
            emptyArt.rectTransform.anchorMin = emptyArt.rectTransform.anchorMax = emptyArt.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            emptyArt.rectTransform.anchoredPosition = Vector2.zero;
            emptyRow = empty.gameObject;

            rowTemplate = BuildRow(rows);
            close = BuildClose(window);
            BuildEscHint(window);
            emptyRow.SetActive(false);
            rowTemplate.SetActive(false);
            window.gameObject.SetActive(false);
            return window.gameObject;
        }

        // Divider on top (hidden on the first row, where the board prints its own), then day badge | resource grid.
        private static GameObject BuildRow(Transform parent)
        {
            RectTransform row = Rect("RowTemplate", parent, 0f, 0f, ContentWidth, 92f);
            row.gameObject.AddComponent<LayoutElement>().minHeight = 92f;
            HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 16, 12);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Image line = Picture("Line", row, Folder, "ghl_divider_line", 0f, 0f, 0f, 12f);
            line.rectTransform.anchorMin = new Vector2(0f, 1f);
            line.rectTransform.anchorMax = Vector2.one;
            line.rectTransform.pivot = new Vector2(0.5f, 1f);
            line.rectTransform.anchoredPosition = Vector2.zero;
            line.rectTransform.sizeDelta = new Vector2(0f, 12f);
            // The mockup's row lines are fainter than the header's.
            line.color = new Color(1f, 1f, 1f, 0.7f);
            line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            RectTransform dayCell = Rect("DayCell", row, 0f, 0f, DayColumnWidth, 64f);
            Stacked(dayCell, DayColumnWidth, 64f);
            Image day = Picture("Day", dayCell, Folder, "ghl_day_1", 0f, 0f, 124f, 63f);
            day.preserveAspect = true;
            LeftMiddle(day.rectTransform, 26f);
            Text dayText = Label("DayText", dayCell, "6일차", 28, Cream, 0f, 0f, 160f, 44f, TextAnchor.MiddleLeft);
            LeftMiddle(dayText.rectTransform, 34f);

            float itemsWidth = ContentWidth - DayColumnWidth;
            float itemWidth = itemsWidth / ItemColumns;
            RectTransform items = Rect("Items", row, 0f, 0f, itemsWidth, 58f);
            GridLayoutGroup grid = items.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(itemWidth, 58f);
            grid.spacing = new Vector2(0f, 6f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = ItemColumns;
            grid.childAlignment = TextAnchor.MiddleLeft;
            items.gameObject.AddComponent<LayoutElement>().preferredWidth = itemsWidth;

            RectTransform item = Rect("ItemTemplate", items, 0f, 0f, itemWidth, 58f);
            RectTransform iconRect = Rect("Icon", item, 0f, 0f, 52f, 52f);
            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            LeftMiddle(iconRect, 0f);
            Text label = Label("Label", item, "목재 × 12", 28, Cream, 0f, 0f, itemWidth - 66f, 46f, TextAnchor.MiddleLeft);
            LeftMiddle(label.rectTransform, 60f);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 16;
            label.resizeTextMaxSize = 28;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            item.gameObject.SetActive(false);

            Stacked(Picture("Nothing", row, Folder, "ghl_nothing", 0f, 0f, 72f, 47f), 72f, 47f);
            return row.gameObject;
        }

        // The mockup's X sits inside the frame's top-right corner and closes like Esc.
        private static Button BuildClose(Transform window)
        {
            Image image = Picture("Close", window, Folder, "ghl_close", 0f, 0f, 46f, 48f);
            image.raycastTarget = true;
            image.preserveAspect = true;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            image.rectTransform.anchoredPosition = new Vector2(-54f, -52f);
            image.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 0.85f, 0.6f);
            colors.pressedColor = new Color(0.75f, 0.65f, 0.5f);
            button.colors = colors;
            return button;
        }

        // Esc keycap + "닫기" on the footer line, right side, as in the mockup. A hint only; Esc itself is read in code.
        private static void BuildEscHint(Transform window)
        {
            RectTransform hint = Rect("EscHint", window, 0f, 0f, 98f, 42f);
            hint.anchorMin = hint.anchorMax = new Vector2(1f, 0f);
            hint.pivot = new Vector2(1f, 0.5f);
            hint.anchoredPosition = new Vector2(-58f, 54f);
            hint.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Picture("Esc", hint, Folder, "ghl_esc", 0f, 0f, 46f, 42f).preserveAspect = true;
            Picture("Label", hint, Folder, "ghl_close_label", 54f, 5f, 44f, 42f).preserveAspect = true;
        }

        private static void LeftMiddle(RectTransform rect, float x)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
        }

        private static void Stacked(Graphic graphic, float width, float height)
        {
            if (graphic is Image image)
            {
                image.preserveAspect = true;
            }

            Stacked(graphic.rectTransform, width, height);
        }

        private static void Stacked(RectTransform rect, float width, float height)
        {
            LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();

            if (width >= 0f)
            {
                element.preferredWidth = width;
            }

            if (height >= 0f)
            {
                element.preferredHeight = height;
            }
        }

        private static void SetDaySprites(SerializedObject serialized, string field, string prefix)
        {
            SerializedProperty sprites = serialized.FindProperty(field);
            sprites.arraySize = DayCount;

            for (int i = 0; i < DayCount; i++)
            {
                sprites.GetArrayElementAtIndex(i).objectReferenceValue = Load(Folder, $"{prefix}{i + 1}");
            }
        }

        private static void CopyResourceIcons(SerializedObject destination)
        {
            WorkbenchScreen workbench = AssetDatabase.LoadAssetAtPath<GameObject>(WorkbenchPrefabPath).GetComponent<WorkbenchScreen>();
            SerializedProperty source = new SerializedObject(workbench).FindProperty("_resourceIcons");
            SerializedProperty icons = destination.FindProperty("_resourceIcons");
            icons.arraySize = source.arraySize;

            for (int i = 0; i < source.arraySize; i++)
            {
                SerializedProperty from = source.GetArrayElementAtIndex(i);
                SerializedProperty to = icons.GetArrayElementAtIndex(i);
                to.FindPropertyRelative("Resource").objectReferenceValue = from.FindPropertyRelative("Resource").objectReferenceValue;
                to.FindPropertyRelative("Icon").objectReferenceValue = from.FindPropertyRelative("Icon").objectReferenceValue;
            }
        }

        private static void AssignToShip(GhostShipLedgerScreen screen)
        {
            GameObject ship = PrefabUtility.LoadPrefabContents(ShipPrefabPath);

            try
            {
                GhostShipHintPopup popup = ship.GetComponent<GhostShipHintPopup>();
                SerializedObject serialized = new SerializedObject(popup);
                Set(serialized, "_screenPrefab", screen);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(ship, ShipPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(ship);
            }
        }

        private static void ImportSprites()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Vector4 border = path.EndsWith("ghl_board.png") ? new Vector4(BoardSide, BoardBottom, BoardSide, BoardTop) : Vector4.zero;
                ImportSprite(path, border);
            }

            HudSketchLayout.ImportSprites();
        }

        private static void ImportSprite(string path, Vector4 border)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static Text Label(string name, Transform parent, string caption, int size, Color color, float x, float y, float width, float height, TextAnchor alignment)
        {
            RectTransform rect = Rect(name, parent, x, y, width, height);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = alignment;
            text.text = caption;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = Shadow;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        private static Image Picture(string name, Transform parent, string folder, string sprite, float x, float y, float width, float height)
        {
            RectTransform rect = Rect(name, parent, x, y, width, height);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = Load(folder, sprite);
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

        private static Sprite Load(string folder, string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{folder}{name}.png");

            if (!sprite)
            {
                throw new System.InvalidOperationException($"Missing sprite {folder}{name}.png.");
            }

            return sprite;
        }

        private static void Set(SerializedObject serialized, string name, Object value)
        {
            SerializedProperty property = serialized.FindProperty(name);

            if (property == null)
            {
                throw new System.InvalidOperationException($"Missing field: {name}");
            }

            property.objectReferenceValue = value;
        }
    }
}
