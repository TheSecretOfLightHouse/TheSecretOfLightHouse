using Lighthouse.Tower.Net;
using Lighthouse.Tower.View;
using Lighthouse.Workbench.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.Editor
{
    // Builds the lighthouse match effects canvas (UI effects plan 2026-10-26) on a 1920x1080 reference: centre title,
    // ritual countdown, edge flash, end fade and the carried-module tag. Notice lines go through the shared
    // ScreenNotice strip (ScreenNoticeSetup). The tag's placeholder icon is a final battle HUD sprite, so build that
    // HUD first once to import them.
    public static class TowerMatchEffectsSetup
    {
        private const string PrefabPath = "Assets/02.Prefabs/UI/Tower/TowerMatchEffects.prefab";
        private const string Folder = "Assets/03.Images/FinalBattleHud/";
        private static readonly Color Cream = new Color(0.96f, 0.89f, 0.74f);
        private static readonly Color Shadow = new Color(0.1f, 0.07f, 0.04f, 0.85f);
        private static readonly Color Backing = new Color(0.04f, 0.06f, 0.09f, 0.72f);

        [MenuItem("Lighthouse/Tower/Build Match Effects Prefab")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"[{nameof(TowerMatchEffectsSetup)}] Stop play mode before building.");
                return;
            }

            var previousScene = EditorSceneManager.GetActiveScene();
            var buildScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(buildScene);
            GameObject root = new GameObject("TowerMatchEffects", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.layer = LayerMask.NameToLayer("UI");

            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // Over the station screens (80) so the installer sees the notice with the screen open; under the
                // summon radial menu (100).
                canvas.sortingOrder = 90;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                AudioSource audio = root.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 0f;

                // Back to front: darkening, edge, then the lines.
                Image fade = Stretch("Fade", root.transform);
                fade.color = new Color(0f, 0f, 0f, 0f);
                Image edge = Stretch("Edge", root.transform);
                edge.color = new Color(1f, 1f, 1f, 0f);

                // The carried-module tag, bottom left; only the carrier's machine ever fills it.
                RectTransform carried = Rect("CarriedModule", root.transform, 0f, 0f, 380f, 64f);
                carried.anchorMin = carried.anchorMax = carried.pivot = new Vector2(0f, 0f);
                carried.anchoredPosition = new Vector2(24f, 24f);
                Image carriedBacking = carried.gameObject.AddComponent<Image>();
                carriedBacking.color = Backing;
                carriedBacking.raycastTarget = false;
                Image carriedIcon = Picture("Icon", carried, "fb_icon_lighthouse", 10f, 6f, 52f, 52f);
                carriedIcon.preserveAspect = true;
                Text carriedText = Label("Text", carried, "들고 있는 부품: -", 22, Cream, 72f, 13f, 300f, 38f, TextAnchor.MiddleLeft);
                CarriedModuleIndicator indicator = root.AddComponent<CarriedModuleIndicator>();
                SerializedObject indicatorFields = new SerializedObject(indicator);
                Set(indicatorFields, "_root", carried.gameObject);
                Set(indicatorFields, "_icon", carriedIcon);
                Set(indicatorFields, "_text", carriedText);
                indicatorFields.ApplyModifiedPropertiesWithoutUndo();

                Text countdown = Label("Countdown", root.transform, "점화 의식 60초", 40, Cream, 0f, 0f, 520f, 64f, TextAnchor.MiddleCenter);
                RectTransform countdownRect = countdown.rectTransform;
                countdownRect.anchorMin = countdownRect.anchorMax = countdownRect.pivot = new Vector2(0.5f, 1f);
                countdownRect.anchoredPosition = new Vector2(0f, -30f);

                RectTransform title = Rect("Title", root.transform, 0f, 0f, 1600f, 140f);
                title.anchorMin = title.anchorMax = title.pivot = new Vector2(0.5f, 0.5f);
                title.anchoredPosition = new Vector2(0f, 140f);
                CanvasGroup titleGroup = title.gameObject.AddComponent<CanvasGroup>();
                titleGroup.blocksRaycasts = false;
                titleGroup.interactable = false;
                Text titleText = Label("Text", title, "심해의 괴물이 깨어났다", 64, Cream, 0f, 0f, 1600f, 140f, TextAnchor.MiddleCenter);
                titleText.GetComponent<Outline>().effectDistance = new Vector2(2.5f, -2.5f);

                TowerMatchEffects view = root.AddComponent<TowerMatchEffects>();
                SerializedObject serialized = new SerializedObject(view);
                Set(serialized, "_title", titleGroup);
                Set(serialized, "_titleText", titleText);
                Set(serialized, "_countdownText", countdown);
                Set(serialized, "_edge", edge);
                Set(serialized, "_fade", fade);
                Set(serialized, "_audio", audio);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[{nameof(TowerMatchEffectsSetup)}] Built {PrefabPath}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(buildScene, true);
            }
        }

        [MenuItem("Lighthouse/Tower/Use Match Effects In Open Scene")]
        public static void UseInOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            TowerMatchNet[] matches = Object.FindObjectsByType<TowerMatchNet>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (EditorApplication.isPlayingOrWillChangePlaymode || matches.Length != 1 || !prefab)
            {
                Debug.LogError($"[{nameof(TowerMatchEffectsSetup)}] Open a scene with one lighthouse match in edit mode and build the prefab first.");
                return;
            }

            TowerMatchEffects effects = null;

            foreach (TowerMatchEffects existing in Object.FindObjectsByType<TowerMatchEffects>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == prefab)
                {
                    effects = existing;
                }
            }

            if (!effects)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Add lighthouse match effects");
                effects = instance.GetComponent<TowerMatchEffects>();
            }

            SerializedObject links = new SerializedObject(effects);
            Set(links, "_match", matches[0]);
            links.ApplyModifiedProperties();
            CarriedModuleIndicator indicator = effects.GetComponent<CarriedModuleIndicator>();

            if (indicator)
            {
                SerializedObject indicatorLinks = new SerializedObject(indicator);
                Set(indicatorLinks, "_workbench", matches[0].GetComponent<TowerNet>().Workbench);
                indicatorLinks.ApplyModifiedProperties();
            }

            // The notices go through the shared strip, so the scene needs one too.
            ScreenNoticeSetup.UseInOpenScene();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = effects.gameObject;
            Debug.Log($"[{nameof(TowerMatchEffectsSetup)}] Match effects connected. Save the scene to keep it.");
        }

        private static Image Stretch(string name, Transform parent)
        {
            RectTransform rect = Rect(name, parent, 0f, 0f, 0f, 0f);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            Image image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
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
                throw new System.InvalidOperationException($"Missing effects field: {name}");
            }

            property.objectReferenceValue = value;
        }
    }
}
