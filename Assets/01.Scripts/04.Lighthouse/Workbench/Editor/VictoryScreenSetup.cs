using Lighthouse.Common.UI;
using Lighthouse.Tower.Net;
using Lighthouse.Tower.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.Editor
{
    // Builds the match result screen from the 2026-10-28 victory sketch on a 1920x1080 reference. Sprites are the
    // separated transparent pieces in VictoryScreen/ (lines replaced 2026-10-29), each fitted to a height and stacked
    // down the centre:
    // symbol, title, line, confirm button. Both sides' pieces are built and the view turns on the winner's.
    public static class VictoryScreenSetup
    {
        private const string PrefabPath = "Assets/02.Prefabs/UI/Tower/VictoryScreen.prefab";
        private const string Folder = "Assets/03.Images/VictoryScreen/";
        // Centre heights and display heights. The pieces are drawn at different pixel scales, so each is fitted to
        // its height and keeps its own aspect.
        private const float SymbolY = 250f;
        private const float SymbolHeight = 220f;
        private const float TitleY = 60f;
        private const float TitleHeight = 130f;
        private const float SubtitleY = -50f;
        private const float SubtitleHeight = 56f;
        private const float ButtonY = -165f;
        private const float ButtonHeight = 104f;
        private const float LabelHeight = 60f;
        private const float ReturnY = -260f;
        private static readonly Color Backdrop = new Color(0.02f, 0.03f, 0.05f, 0.6f);
        private static readonly Color Faded = new Color(0.86f, 0.79f, 0.65f);

        [MenuItem("Lighthouse/Tower/Build Victory Screen Prefab")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"[{nameof(VictoryScreenSetup)}] Stop play mode before building.");
                return;
            }

            ImportSprites();
            var previousScene = EditorSceneManager.GetActiveScene();
            var buildScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(buildScene);
            GameObject root = new GameObject("VictoryScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.layer = LayerMask.NameToLayer("UI");

            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // Over everything else, including the summon radial menu (100), so the confirm button can be pressed.
                canvas.sortingOrder = 110;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                RectTransform screen = Rect("Screen", root.transform, Vector2.zero, Vector2.zero);
                screen.anchorMin = Vector2.zero;
                screen.anchorMax = Vector2.one;
                screen.offsetMin = screen.offsetMax = Vector2.zero;
                CanvasGroup group = screen.gameObject.AddComponent<CanvasGroup>();
                // Blocks clicks into the world while it is up.
                Image backdrop = screen.gameObject.AddComponent<Image>();
                backdrop.color = Backdrop;

                RectTransform content = Rect("Content", screen, Vector2.zero, new Vector2(1000f, 760f));

                GameObject citizensSymbol = Picture("CitizensSymbol", content, "vs_civilian_symbol", SymbolY, SymbolHeight);
                GameObject citizensTitle = Picture("CitizensTitle", content, "vs_civilian_title", TitleY, TitleHeight);
                GameObject citizensSubtitle = Picture("CitizensSubtitle", content, "vs_civilian_subtitle", SubtitleY, SubtitleHeight);
                GameObject mafiaSymbol = Picture("MafiaSymbol", content, "vs_mafia_symbol", SymbolY, SymbolHeight);
                GameObject mafiaTitle = Picture("MafiaTitle", content, "vs_mafia_title", TitleY, TitleHeight);
                GameObject mafiaSubtitle = Picture("MafiaSubtitle", content, "vs_mafia_subtitle", SubtitleY, SubtitleHeight);

                // The label is its own child so the word keeps its size whatever the frame does.
                GameObject frame = Picture("ConfirmButton", content, "vs_button_frame", ButtonY, ButtonHeight);
                Image frameImage = frame.GetComponent<Image>();
                frameImage.raycastTarget = true;
                Button button = frame.AddComponent<Button>();
                button.targetGraphic = frameImage;
                ColorBlock colors = button.colors;
                colors.highlightedColor = new Color(1f, 0.93f, 0.78f);
                colors.pressedColor = new Color(0.8f, 0.74f, 0.62f);
                button.colors = colors;
                frame.AddComponent<UiButtonFeedback>();
                Picture("Label", frame.transform, "vs_button_label", 0f, LabelHeight);

                RectTransform returnRect = Rect("ReturnCountdown", content, new Vector2(0f, ReturnY), new Vector2(800f, 40f));
                Text returnText = returnRect.gameObject.AddComponent<Text>();
                returnText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                returnText.fontSize = 22;
                returnText.color = Faded;
                returnText.alignment = TextAnchor.MiddleCenter;
                returnText.text = "확인을 누르면 선원 대기실로 돌아갑니다";
                returnText.raycastTarget = false;
                returnText.horizontalOverflow = HorizontalWrapMode.Overflow;

                VictoryScreen view = root.AddComponent<VictoryScreen>();
                SerializedObject serialized = new SerializedObject(view);
                Set(serialized, "_root", group);
                Set(serialized, "_content", content);
                Set(serialized, "_confirmButton", button);
                Set(serialized, "_returnText", returnText);
                Set(serialized, "_citizensSymbol", citizensSymbol);
                Set(serialized, "_citizensTitle", citizensTitle);
                Set(serialized, "_citizensSubtitle", citizensSubtitle);
                Set(serialized, "_mafiaSymbol", mafiaSymbol);
                Set(serialized, "_mafiaTitle", mafiaTitle);
                Set(serialized, "_mafiaSubtitle", mafiaSubtitle);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                screen.gameObject.SetActive(false);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[{nameof(VictoryScreenSetup)}] Built {PrefabPath}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(buildScene, true);
            }
        }

        [MenuItem("Lighthouse/Tower/Use Victory Screen In Open Scene")]
        public static void UseInOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            TowerMatchNet[] matches = Object.FindObjectsByType<TowerMatchNet>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (EditorApplication.isPlayingOrWillChangePlaymode || matches.Length != 1 || !prefab)
            {
                Debug.LogError($"[{nameof(VictoryScreenSetup)}] Open a scene with one lighthouse match in edit mode and build the prefab first.");
                return;
            }

            VictoryScreen screen = null;

            foreach (VictoryScreen existing in Object.FindObjectsByType<VictoryScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == prefab)
                {
                    screen = existing;
                }
            }

            if (!screen)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Add victory screen");
                screen = instance.GetComponent<VictoryScreen>();
            }

            SerializedObject links = new SerializedObject(screen);
            Set(links, "_match", matches[0]);
            links.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = screen.gameObject;
            Debug.Log($"[{nameof(VictoryScreenSetup)}] Victory screen connected. Save the scene to keep it.");
        }

        private static void ImportSprites()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder.TrimEnd('/') }))
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

        // Centred on x at the given height from the parent's centre, fitted to the height at the sprite's own aspect.
        private static GameObject Picture(string name, Transform parent, string sprite, float y, float height)
        {
            Sprite loaded = Load(sprite);
            Vector2 size = new Vector2(loaded.rect.width * height / loaded.rect.height, height);
            RectTransform rect = Rect(name, parent, new Vector2(0f, y), size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = loaded;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return rect.gameObject;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Sprite Load(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}{name}.png");

            if (!sprite)
            {
                throw new System.InvalidOperationException($"Missing victory screen sprite: {Folder}{name}");
            }

            return sprite;
        }

        private static void Set(SerializedObject serialized, string name, Object value)
        {
            SerializedProperty property = serialized.FindProperty(name);

            if (property == null)
            {
                throw new System.InvalidOperationException($"Missing victory screen field: {name}");
            }

            property.objectReferenceValue = value;
        }
    }
}
