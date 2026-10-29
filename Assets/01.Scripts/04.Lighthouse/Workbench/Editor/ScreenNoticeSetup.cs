using Lighthouse.Common.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.Editor
{
    // Builds the shared notice strip (00.Common/UI ScreenNotice) on a 1920x1080 reference. It sits under the final
    // battle HUD's strip (16..196 from the top) so they never overlap. Kept in this editor assembly so no new asmdef is
    // needed; the bar pieces and default icon are the final battle HUD's sprites, so build that HUD once first.
    // Rebuilding overwrites the prefab, so later look changes made by hand belong in the inspector, not here.
    public static class ScreenNoticeSetup
    {
        private const string PrefabPath = "Assets/02.Prefabs/UI/Common/ScreenNotice.prefab";
        private const string Folder = "Assets/03.Images/FinalBattleHud/";
        private const float BarWidth = 300f;
        private const float BarHeight = 30f;
        // Track and fill sit inside the frame by the sprites' own ratio (456x46 inside 512x80).
        private const float InnerWidth = BarWidth * 456f / 512f;
        private const float InnerHeight = BarHeight * 46f / 80f;
        private static readonly Color Cream = new Color(0.96f, 0.89f, 0.74f);
        private static readonly Color Gold = new Color(0.98f, 0.73f, 0.15f);
        private static readonly Color Shadow = new Color(0.1f, 0.07f, 0.04f, 0.85f);
        private static readonly Color Backing = new Color(0.04f, 0.06f, 0.09f, 0.72f);

        [MenuItem("Lighthouse/UI/Build Screen Notice Prefab")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"[{nameof(ScreenNoticeSetup)}] Stop play mode before building.");
                return;
            }

            var previousScene = EditorSceneManager.GetActiveScene();
            var buildScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(buildScene);
            GameObject root = new GameObject("ScreenNotice", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.layer = LayerMask.NameToLayer("UI");

            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // Over the station screens (80) so a player at a screen still sees it; under the summon radial menu (100).
                canvas.sortingOrder = 90;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                RectTransform strip = Rect("Strip", root.transform, 0f, 0f, 640f, 96f);
                strip.anchorMin = strip.anchorMax = strip.pivot = new Vector2(0.5f, 1f);
                strip.anchoredPosition = new Vector2(0f, -206f);
                CanvasGroup group = strip.gameObject.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.interactable = false;
                Image backing = strip.gameObject.AddComponent<Image>();
                backing.color = Backing;
                backing.raycastTarget = false;
                Image icon = Picture("Icon", strip, "fb_icon_lighthouse", 22f, 12f, 54f, 72f);
                icon.preserveAspect = true;
                Text text = Label("Text", strip, "알림", 26, Cream, 92f, 10f, 520f, 38f, TextAnchor.MiddleLeft);
                RectTransform gauge = Rect("Gauge", strip, 92f, 54f, BarWidth, BarHeight);
                Image fill = Bar(gauge);
                Text gain = Label("Gain", strip, "+0%", 24, Gold, 404f, 50f, 140f, 36f, TextAnchor.MiddleLeft);

                ScreenNotice notice = root.AddComponent<ScreenNotice>();
                SerializedObject serialized = new SerializedObject(notice);
                Set(serialized, "_group", group);
                Set(serialized, "_backing", backing);
                Set(serialized, "_icon", icon);
                Set(serialized, "_text", text);
                Set(serialized, "_gauge", gauge.gameObject);
                Set(serialized, "_gaugeFill", fill);
                Set(serialized, "_gain", gain);
                Set(serialized, "_defaultIcon", icon.sprite);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[{nameof(ScreenNoticeSetup)}] Built {PrefabPath}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(buildScene, true);
            }
        }

        // One strip per scene: Show goes to the last one enabled.
        [MenuItem("Lighthouse/UI/Use Screen Notice In Open Scene")]
        public static void UseInOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (EditorApplication.isPlayingOrWillChangePlaymode || !prefab)
            {
                Debug.LogError($"[{nameof(ScreenNoticeSetup)}] Build the prefab first, in edit mode.");
                return;
            }

            ScreenNotice[] existing = Object.FindObjectsByType<ScreenNotice>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (existing.Length > 0)
            {
                Selection.activeGameObject = existing[0].gameObject;
                Debug.Log($"[{nameof(ScreenNoticeSetup)}] The scene already has a screen notice.");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Add screen notice");
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = instance;
            Debug.Log($"[{nameof(ScreenNoticeSetup)}] Screen notice added. Save the scene to keep it.");
        }

        // Track, fill and frame, back to front. The fill is cut from the left by fillAmount.
        private static Image Bar(RectTransform bar)
        {
            float insetX = (BarWidth - InnerWidth) * 0.5f;
            float insetY = (BarHeight - InnerHeight) * 0.5f;
            Picture("Track", bar, "fb_bar_track", insetX, insetY, InnerWidth, InnerHeight);
            Image fill = Picture("Fill", bar, "fb_bar_fill_yellow", insetX, insetY, InnerWidth, InnerHeight);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            Picture("Frame", bar, "fb_bar_frame", 0f, 0f, BarWidth, BarHeight);
            return fill;
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
            // Over open sea and fog, so every line gets a dark edge.
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = Shadow;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        private static Image Picture(string name, Transform parent, string sprite, float x, float y, float width, float height)
        {
            RectTransform rect = Rect(name, parent, x, y, width, height);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = Load(sprite);
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

        private static Sprite Load(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}{name}.png");

            if (!sprite)
            {
                throw new System.InvalidOperationException($"Missing sprite {Folder}{name}. Build the final battle HUD prefab first.");
            }

            return sprite;
        }

        private static void Set(SerializedObject serialized, string name, Object value)
        {
            SerializedProperty property = serialized.FindProperty(name);

            if (property == null)
            {
                throw new System.InvalidOperationException($"Missing screen notice field: {name}");
            }

            property.objectReferenceValue = value;
        }
    }
}
