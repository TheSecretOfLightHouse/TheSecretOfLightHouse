using Lighthouse.UI.Hud;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.Editor
{
    public static class DayCycleHudSetup
    {
        private const string Folder = "Assets/03.Images/DayCycleHud/";
        private const string PrefabPath = "Assets/02.Prefabs/UI/Hud/InGameHud.prefab";

        // Source badge pixels (592x620) shown at this size on the 1920x1080 reference canvas.
        private const float Scale = 0.3f;
        private const float FillCap = 34f;

        [MenuItem("Lighthouse/UI/Update Day Cycle HUD")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Sprite dayBadge = Import("day_badge.png", Vector4.zero);
            Sprite nightBadge = Import("night_badge.png", Vector4.zero);
            Sprite dayFill = Import("day_fill.png", new Vector4(FillCap, 0f, FillCap, 0f));
            Sprite nightFill = Import("night_fill.png", new Vector4(FillCap, 0f, FillCap, 0f));
            GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform previous = prefab.transform.Find("Hud/DayCycle");
                if (previous)
                {
                    Object.DestroyImmediate(previous.gameObject);
                }

                Vector2 size = new Vector2(592f, 620f) * Scale;
                RectTransform root = Rect("DayCycle", prefab.transform.Find("Hud"), 0f, 0f, size.x, size.y);
                root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
                root.pivot = new Vector2(0.5f, 1f);
                root.anchoredPosition = new Vector2(0f, -8f);
                DayCycleHud view = root.gameObject.AddComponent<DayCycleHud>();
                RectTransform display = Rect("Display", root, 0f, 0f, size.x, size.y);

                Image badge = Picture("Badge", display, dayBadge, 0f, 0f, size.x, size.y);
                Image fill = Picture("Fill", display, dayFill, 51f * Scale, -545f * Scale, 2f * FillCap * Scale, 37f * Scale);
                fill.type = Image.Type.Sliced;
                fill.pixelsPerUnitMultiplier = 1f / Scale;
                Text dayText = Label("DayLabel", display, 412f, 100);
                Text phaseText = Label("PhaseLabel", display, 503f, 56);

                SerializedObject serialized = new SerializedObject(view);
                Set(serialized, "_display", display.gameObject);
                Set(serialized, "_badge", badge);
                Set(serialized, "_fill", fill);
                Set(serialized, "_dayText", dayText);
                Set(serialized, "_phaseText", phaseText);
                Set(serialized, "_dayBadge", dayBadge);
                Set(serialized, "_nightBadge", nightBadge);
                Set(serialized, "_dayFill", dayFill);
                Set(serialized, "_nightFill", nightFill);
                serialized.FindProperty("_fillMinWidth").floatValue = 2f * FillCap * Scale;
                serialized.FindProperty("_fillMaxWidth").floatValue = 488f * Scale;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                view.Render(1, false, 0.4f);
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        private static Sprite Import(string name, Vector4 border)
        {
            string path = Folder + name;
            AssetDatabase.ImportAsset(path);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // centreY is measured in source badge pixels from the top, matching the mockup lettering.
        private static Text Label(string name, Transform parent, float centreY, int sourceSize)
        {
            float height = sourceSize * 1.3f * Scale;
            Text text = Rect(name, parent, 0f, -centreY * Scale + height * 0.5f, 592f * Scale, height).gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Mathf.RoundToInt(sourceSize * Scale);
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = HudSketchLayout.Cream;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            GameObject item = new GameObject(name, typeof(RectTransform));
            item.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = (RectTransform)item.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static Image Picture(string name, Transform parent, Sprite sprite, float x, float y, float width, float height)
        {
            Image image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static void Set(SerializedObject serialized, string name, Object value)
        {
            serialized.FindProperty(name).objectReferenceValue = value;
        }
    }
}
