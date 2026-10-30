using Lighthouse.Interaction.GhostShipHints.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.Editor
{
    // Builds the ghost ship ledger (2026-10-30 mockup) from the parts cut out of the user's atlas, on a 1920x1080
    // reference, and hands it to the ghost ship prefab. The board is nine-sliced so it grows with the rows a layout
    // group stacks: header, then one divider + day row per day. The E prompt copies the workbench's (keycap + label art
    // at the bottom centre). Kept in this editor assembly so no new asmdef is needed.
    // Rebuilding overwrites the prefab, so later look changes made by hand belong in the inspector, not here.
    public static class GhostShipLedgerSetup
    {
        private const string PrefabPath = "Assets/02.Prefabs/UI/GhostShip/GhostShipLedger.prefab";
        private const string ShipPrefabPath = "Assets/02.Prefabs/World/GhostShip/GhostShip.prefab";
        private const string Folder = "Assets/03.Images/GhostShipLedger/";
        private const string HudFolder = "Assets/03.Images/InGameHud/";
        private const int DayCount = 5;
        private const float BoardWidth = 720f;
        private const float ContentWidth = 560f;
        // Torn edge and frame of the board art, kept unstretched (source pixels, art is 1041x780).
        private const float BoardBorder = 130f;
        private const float BoardArtWidth = 1041f;
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

                GameObject window = BuildWindow(root.transform, out RectTransform rows, out GameObject rowTemplate, out GameObject emptyRow);

                // Same spot and parts as the workbench's open prompt; the two never share a screen (sea vs island).
                RectTransform prompt = Rect("Prompt", root.transform, 0f, 0f, 190f, 48f);
                prompt.anchorMin = prompt.anchorMax = new Vector2(0.5f, 0f);
                prompt.pivot = new Vector2(0.5f, 0f);
                prompt.anchoredPosition = new Vector2(0f, 150f);
                Picture("Key", prompt, HudFolder, "hud_key_e", 0f, 1f, 47f, 46f).preserveAspect = true;
                Picture("Label", prompt, HudFolder, "hud_label_check_ghostship", 56f, 8.5f, 133f, 31f).preserveAspect = true;

                Sprite[] days = new Sprite[DayCount];

                for (int i = 0; i < DayCount; i++)
                {
                    days[i] = Load(Folder, $"ghl_day_{i + 1}");
                }

                GhostShipLedgerScreen screen = root.AddComponent<GhostShipLedgerScreen>();
                SerializedObject serialized = new SerializedObject(screen);
                Set(serialized, "_prompt", prompt.gameObject);
                Set(serialized, "_window", window);
                Set(serialized, "_rows", rows);
                Set(serialized, "_rowTemplate", rowTemplate);
                Set(serialized, "_emptyRow", emptyRow);
                SerializedProperty daySprites = serialized.FindProperty("_daySprites");
                daySprites.arraySize = DayCount;

                for (int i = 0; i < DayCount; i++)
                {
                    daySprites.GetArrayElementAtIndex(i).objectReferenceValue = days[i];
                }

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

        private static GameObject BuildWindow(Transform parent, out RectTransform rows, out GameObject rowTemplate, out GameObject emptyRow)
        {
            RectTransform window = Rect("Window", parent, 0f, 0f, BoardWidth, 400f);
            window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
            window.anchoredPosition = new Vector2(0f, 40f);
            Image board = window.gameObject.AddComponent<Image>();
            board.sprite = Load(Folder, "ghl_board");
            board.type = Image.Type.Sliced;
            // Draws the sliced frame at the board's on-screen scale instead of the art's full pixel size.
            board.pixelsPerUnitMultiplier = BoardArtWidth / BoardWidth;
            board.raycastTarget = false;
            VerticalLayoutGroup layout = window.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(80, 80, 58, 70);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = window.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Stacked(Picture("Title", window, Folder, "ghl_title", 0f, 0f, 0f, 0f), 334f, 73f);
            Stacked(Picture("Knot", window, Folder, "ghl_divider_knot", 0f, 0f, 0f, 0f), ContentWidth, ContentWidth * 75f / 825f);
            Stacked(Picture("Subtitle", window, Folder, "ghl_subtitle", 0f, 0f, 0f, 0f), 278f, 40f);

            rows = Rect("Rows", window, 0f, 0f, ContentWidth, 0f);
            VerticalLayoutGroup rowLayout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
            rowLayout.childAlignment = TextAnchor.UpperCenter;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            Stacked(rows, ContentWidth, -1f);

            emptyRow = Row(rows, "EmptyRow", 80f).gameObject;
            Picture("Empty", emptyRow.transform, Folder, "ghl_empty", 151f, 34f, 258f, 38f).preserveAspect = true;

            RectTransform template = Row(rows, "RowTemplate", 72f);
            Picture("Day", template, Folder, "ghl_day_1", 14f, 30f, 74f, 35f).preserveAspect = true;
            Label("DayText", template, "6일차:", 26, Cream, 14f, 28f, 90f, 38f, TextAnchor.MiddleLeft);
            Text resources = Label("Resources", template, "목재 3, 철 2", 24, Cream, 106f, 26f, ContentWidth - 112f, 42f, TextAnchor.MiddleLeft);
            resources.resizeTextForBestFit = true;
            resources.resizeTextMinSize = 14;
            resources.resizeTextMaxSize = 24;
            resources.horizontalOverflow = HorizontalWrapMode.Wrap;
            rowTemplate = template.gameObject;
            emptyRow.SetActive(false);
            rowTemplate.SetActive(false);
            window.gameObject.SetActive(false);
            return window.gameObject;
        }

        // A divider across the top, then the row's own parts under it.
        private static RectTransform Row(Transform parent, string name, float height)
        {
            RectTransform row = Rect(name, parent, 0f, 0f, ContentWidth, height);
            Stacked(row, ContentWidth, height);
            Picture("Line", row, Folder, "ghl_divider_line", 0f, 4f, ContentWidth, ContentWidth * 30f / 802f);
            return row;
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
            element.preferredWidth = width;

            if (height >= 0f)
            {
                element.preferredHeight = height;
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
                Vector4 border = path.EndsWith("ghl_board.png") ? Vector4.one * BoardBorder : Vector4.zero;
                ImportSprite(path, border);
            }

            ImportSprite($"{HudFolder}hud_label_check_ghostship.png", Vector4.zero);
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
