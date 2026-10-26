using Lighthouse.Tower.Net;
using Lighthouse.Tower.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.Editor
{
    // Builds the final battle HUD from the 2026-10-23 sketch. Positions are the sketch's pixels inside a 1500x180 strip
    // pinned to the top centre of a 1920x1080 canvas. Sprites are the separated pieces in FinalBattleHud/.
    public static class FinalBattleHudSetup
    {
        private const string PrefabPath = "Assets/02.Prefabs/UI/Tower/FinalBattleHud.prefab";
        private const string Folder = "Assets/03.Images/FinalBattleHud/";
        private const float BarWidth = 240f;
        private const float BarHeight = 38f;
        // Track and fill sit inside the frame by the sprites' own ratio (456x46 inside 512x80).
        private const float InnerWidth = BarWidth * 456f / 512f;
        private const float InnerHeight = BarHeight * 46f / 80f;
        private static readonly Color Cream = new Color(0.96f, 0.89f, 0.74f);
        private static readonly Color Faded = new Color(0.86f, 0.79f, 0.65f);
        private static readonly Color Gold = new Color(0.98f, 0.73f, 0.15f);
        private static readonly Color Hit = new Color(1f, 0.35f, 0.28f);
        private static readonly Color Shadow = new Color(0.1f, 0.07f, 0.04f, 0.85f);

        [MenuItem("Lighthouse/Tower/Build Final Battle HUD Prefab")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError($"[{nameof(FinalBattleHudSetup)}] Stop play mode before building.");
                return;
            }

            ImportSprites();
            var previousScene = EditorSceneManager.GetActiveScene();
            var buildScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(buildScene);
            GameObject root = new GameObject("FinalBattleHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.layer = LayerMask.NameToLayer("UI");

            try
            {
                Canvas canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // Under the station screens (80), which cover it while open.
                canvas.sortingOrder = 60;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                RectTransform hud = Rect("Hud", root.transform, 0f, 0f, 1500f, 180f);
                hud.anchorMin = hud.anchorMax = hud.pivot = new Vector2(0.5f, 1f);
                hud.anchoredPosition = new Vector2(0f, -16f);

                // Repair, left.
                Icon("Lighthouse", hud, "fb_icon_lighthouse", 52f, 34f, 84f, 111f);
                Icon("RayLeft", hud, "fb_accent_right", 34f, 44f, 16f, 24f);
                Icon("RayRight", hud, "fb_accent_left", 134f, 44f, 16f, 24f);
                Label("RepairTitle", hud, "등대 수리도", 26, Cream, 162f, 44f, 230f, 36f, TextAnchor.MiddleLeft);
                Image repairFill = Bar("RepairBar", hud, "fb_bar_fill_yellow", 160f, 84f);
                Text repairText = Label("RepairValue", hud, "0%", 30, Gold, 410f, 80f, 120f, 46f, TextAnchor.MiddleLeft);
                Label("RepairGoal", hud, "100% 달성 시 시민 승리", 17, Faded, 162f, 124f, 300f, 28f, TextAnchor.MiddleLeft);

                // Title, centre.
                Icon("Lantern", hud, "fb_icon_lantern", 727f, 4f, 46f, 78f);
                Icon("LanternRayLeft", hud, "fb_accent_left", 690f, 20f, 18f, 27f);
                Icon("LanternRayRight", hud, "fb_accent_right", 792f, 20f, 18f, 27f);
                Icon("AccentLeft", hud, "fb_accent_left", 586f, 86f, 26f, 39f);
                Icon("AccentRight", hud, "fb_accent_right", 888f, 86f, 26f, 39f);
                Label("Title", hud, "최후 전투", 46, Cream, 600f, 78f, 300f, 58f, TextAnchor.MiddleCenter);
                Label("Subtitle", hud, "등대를 지켜 수리를 완성하세요", 20, Faded, 520f, 140f, 460f, 32f, TextAnchor.MiddleCenter);

                // Health, right.
                Icon("Shield", hud, "fb_icon_shield", 1028f, 40f, 66f, 79f);
                Label("HealthTitle", hud, "등대 내구도", 26, Cream, 1112f, 44f, 230f, 36f, TextAnchor.MiddleLeft);
                Image healthFill = Bar("HealthBar", hud, "fb_bar_fill_hp_red", 1110f, 84f);
                Text healthText = Label("HealthValue", hud, "0 / 0", 24, Cream, 1360f, 80f, 140f, 46f, TextAnchor.MiddleLeft);
                Label("HealthGoal", hud, "0이 되면 마피아 승리", 17, Faded, 1112f, 124f, 300f, 28f, TextAnchor.MiddleLeft);
                Text damageText = Label("HealthDamage", hud, "-0", 28, Hit, 1360f, 34f, 140f, 40f, TextAnchor.MiddleLeft);

                FinalBattleHud view = root.AddComponent<FinalBattleHud>();
                SerializedObject serialized = new SerializedObject(view);
                Set(serialized, "_root", hud.gameObject);
                Set(serialized, "_repairFill", repairFill);
                Set(serialized, "_repairText", repairText);
                Set(serialized, "_healthFill", healthFill);
                Set(serialized, "_healthText", healthText);
                Set(serialized, "_damageText", damageText);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                hud.gameObject.SetActive(false);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[{nameof(FinalBattleHudSetup)}] Built {PrefabPath}.");
            }
            finally
            {
                Object.DestroyImmediate(root);
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(buildScene, true);
            }
        }

        [MenuItem("Lighthouse/Tower/Use Final Battle HUD In Open Scene")]
        public static void UseInOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            TowerMatchNet[] matches = Object.FindObjectsByType<TowerMatchNet>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            if (EditorApplication.isPlayingOrWillChangePlaymode || matches.Length != 1 || !prefab)
            {
                Debug.LogError($"[{nameof(FinalBattleHudSetup)}] Open a scene with one lighthouse match in edit mode and build the prefab first.");
                return;
            }

            FinalBattleHud hud = null;

            foreach (FinalBattleHud existing in Object.FindObjectsByType<FinalBattleHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(existing.gameObject) == prefab)
                {
                    hud = existing;
                }
            }

            if (!hud)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(instance, "Add final battle HUD");
                hud = instance.GetComponent<FinalBattleHud>();
            }

            SerializedObject links = new SerializedObject(hud);
            Set(links, "_match", matches[0]);
            links.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = hud.gameObject;
            Debug.Log($"[{nameof(FinalBattleHudSetup)}] Final battle HUD connected. Save the scene to keep it.");
        }

        // Track, fill and frame, back to front. The fill is cut from the left by fillAmount.
        private static Image Bar(string name, Transform parent, string fillSprite, float x, float y)
        {
            RectTransform bar = Rect(name, parent, x, y, BarWidth, BarHeight);
            float insetX = (BarWidth - InnerWidth) * 0.5f;
            float insetY = (BarHeight - InnerHeight) * 0.5f;
            Picture("Track", bar, "fb_bar_track", insetX, insetY, InnerWidth, InnerHeight);
            Image fill = Picture("Fill", bar, fillSprite, insetX, insetY, InnerWidth, InnerHeight);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            Picture("Frame", bar, "fb_bar_frame", 0f, 0f, BarWidth, BarHeight);
            return fill;
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

        private static Image Icon(string name, Transform parent, string sprite, float x, float y, float width, float height)
        {
            Image image = Picture(name, parent, sprite, x, y, width, height);
            image.preserveAspect = true;
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
                throw new System.InvalidOperationException($"Missing final battle HUD sprite: {Folder}{name}");
            }

            return sprite;
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
