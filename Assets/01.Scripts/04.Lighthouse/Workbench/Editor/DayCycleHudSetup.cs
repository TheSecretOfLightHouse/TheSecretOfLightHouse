using Lighthouse.UI.Hud;
using Lighthouse.World.Weather.Rules;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.Editor
{
    // Builds the top status bar from the 2026-11-01 mockup: weather | wind heading | day | 낮/밤 | time left. Places are
    // fractions of the bar measured on the mockup; the pieces are cut from the supplied parts and icon sheets.
    public static class DayCycleHudSetup
    {
        private const string Folder = "Assets/03.Images/StatusBar/";
        private const string PrefabPath = "Assets/02.Prefabs/UI/Hud/InGameHud.prefab";

        // Bar size on the 1920x1080 reference canvas.
        private const float Width = 640f;
        private const float Height = 128f;
        // Match the vertical centre of the repair HUD's mockup bounds (y = 28..146).
        private const float RepairCenterY = 87f * 1920f / 1672f;
        private const float Scale = 0.76f * 1.14f;

        // Source pixel sizes of the sliced pieces and their rounded caps.
        private const float FrameHeight = 364f;
        private const float FrameCap = 190f;
        private const float TrackHeight = 85f;
        private const float TrackCap = 42f;

        private const float IconSize = 56f;
        private const float CaptionSize = 18f;
        private const float IconY = 0.40f;
        private const float CaptionY = 0.76f;

        private static readonly float[] DividerX = { 0.205f, 0.418f, 0.612f, 0.758f };
        private static readonly string[] WeatherIcons = { "clear", "cloudy", "rain", "storm", "whirlpool" };
        private static readonly string[] WindIcons = { "n", "ne", "e", "se", "s", "sw", "w", "nw" };

        [MenuItem("Lighthouse/UI/Update Day Cycle HUD")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Sprite frameSprite = Import("status_frame.png", new Vector4(FrameCap, 0f, FrameCap, 0f));
            Sprite dividerSprite = Import("status_divider.png", Vector4.zero);
            Sprite trackSprite = Import("status_track.png", new Vector4(TrackCap, 0f, TrackCap, 0f));
            Sprite fillSprite = Import("status_fill.png", new Vector4(TrackCap, 0f, TrackCap, 0f));
            Sprite nightSprite = Import("phase_night.png", Vector4.zero);
            Sprite[] weatherSprites = ImportAll("weather_", WeatherIcons);
            Sprite[] windSprites = ImportAll("wind_", WindIcons);
            GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Transform hud = prefab.transform.Find("Hud");
                // The bar replaces the 2026-10-29 day badge and the separate weather block.
                foreach (string previous in new[] { "DayCycle", "WeatherWind", "StatusBar" })
                {
                    Transform old = hud.Find(previous);
                    if (old)
                    {
                        Object.DestroyImmediate(old.gameObject);
                    }
                }

                RectTransform root = Rect("StatusBar", hud);
                root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
                root.pivot = new Vector2(0.5f, 0.5f);
                root.anchoredPosition = new Vector2(0f, -RepairCenterY);
                root.sizeDelta = new Vector2(Width, Height);
                root.localScale = new Vector3(Scale, Scale, 1f);
                DayCycleHud day = root.gameObject.AddComponent<DayCycleHud>();
                WeatherWindHud weather = root.gameObject.AddComponent<WeatherWindHud>();

                RectTransform display = Rect("Display", root);
                Fit(display);
                Image frame = Picture("Frame", display, frameSprite);
                Fit(frame.rectTransform);
                frame.type = Image.Type.Sliced;
                frame.pixelsPerUnitMultiplier = FrameHeight / Height;
                for (int i = 0; i < DividerX.Length; i++)
                {
                    Place(Picture($"Divider{i + 1}", display, dividerSprite).rectTransform, DividerX[i], 0.5f, 6f, 80f);
                }

                RectTransform weatherGroup = Rect("Weather", display);
                Fit(weatherGroup);
                Image weatherIcon = Icon("WeatherIcon", weatherGroup, 0.115f);
                TMP_Text weatherText = Caption("WeatherLabel", weatherGroup, 0.115f, CaptionY, CaptionSize);
                Image windIcon = Icon("WindIcon", weatherGroup, 0.31f);
                TMP_Text windText = Caption("WindLabel", weatherGroup, 0.31f, CaptionY, CaptionSize);

                TMP_Text dayText = Caption("DayLabel", display, 0.515f, 0.5f, 36f);
                Image phaseIcon = Icon("PhaseIcon", display, 0.683f);
                Place(phaseIcon.rectTransform, 0.683f, IconY, 50f, 50f);
                TMP_Text phaseText = Caption("PhaseLabel", display, 0.683f, CaptionY, CaptionSize);
                TMP_Text remainText = Caption("RemainLabel", display, 0.862f, 0.22f, 15f);
                TMP_Text timeText = Caption("TimeLabel", display, 0.862f, 0.48f, 32f);

                const float trackLeft = 0.785f;
                const float trackRight = 0.94f;
                const float trackHigh = 13f;
                float trackWidth = (trackRight - trackLeft) * Width;
                float trackScale = trackHigh / TrackHeight;
                Image track = Picture("Track", display, trackSprite);
                Place(track.rectTransform, (trackLeft + trackRight) * 0.5f, CaptionY, trackWidth, trackHigh);
                track.type = Image.Type.Sliced;
                track.pixelsPerUnitMultiplier = 1f / trackScale;
                Image fill = Picture("Fill", display, fillSprite);
                RectTransform fillRect = fill.rectTransform;
                fillRect.pivot = new Vector2(0f, 0.5f);
                fillRect.anchoredPosition = new Vector2(trackLeft * Width, -CaptionY * Height);
                fillRect.sizeDelta = new Vector2(trackWidth, trackHigh);
                fill.type = Image.Type.Sliced;
                fill.pixelsPerUnitMultiplier = 1f / trackScale;

                SerializedObject dayView = new SerializedObject(day);
                Set(dayView, "_display", display.gameObject);
                Set(dayView, "_phaseIcon", phaseIcon);
                Set(dayView, "_fill", fill);
                Set(dayView, "_dayText", dayText);
                Set(dayView, "_phaseText", phaseText);
                Set(dayView, "_remainText", remainText);
                Set(dayView, "_timeText", timeText);
                // No separate day icon was supplied; the clear weather sun stands for the day.
                Set(dayView, "_dayIcon", weatherSprites[(int)WeatherType.Calm]);
                Set(dayView, "_nightIcon", nightSprite);
                dayView.FindProperty("_fillMinWidth").floatValue = 2f * TrackCap * trackScale;
                dayView.FindProperty("_fillMaxWidth").floatValue = trackWidth;
                dayView.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject weatherView = new SerializedObject(weather);
                Set(weatherView, "_display", weatherGroup.gameObject);
                Set(weatherView, "_weatherIcon", weatherIcon);
                Set(weatherView, "_weatherText", weatherText);
                Set(weatherView, "_windIcon", windIcon);
                Set(weatherView, "_windText", windText);
                SetArray(weatherView, "_weatherSprites", weatherSprites);
                SetArray(weatherView, "_windSprites", windSprites);
                weatherView.ApplyModifiedPropertiesWithoutUndo();

                // Fill the mockup's values in, so the prefab reads like the mockup while the game is not running.
                day.Render(3, true, 0.45f, 72.0);
                weather.RefreshWeather(WeatherType.Overcast);
                weather.RefreshWind(new WindDisplay(6f, 90f, CompassPoint.East, WindStrength.Moderate));
                PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        private static Sprite[] ImportAll(string prefix, string[] names)
        {
            Sprite[] sprites = new Sprite[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                sprites[i] = Import(prefix + names[i] + ".png", Vector4.zero);
            }

            return sprites;
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
            // The pieces are drawn several times smaller than their pixels, so mipmaps keep the edges clean.
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Image Icon(string name, Transform parent, float x)
        {
            Image icon = Picture(name, parent, null);
            icon.preserveAspect = true;
            Place(icon.rectTransform, x, IconY, IconSize, IconSize);
            return icon;
        }

        private static TMP_Text Caption(string name, Transform parent, float x, float y, float size)
        {
            RectTransform rect = Rect(name, parent);
            Place(rect, x, y, 120f, size * 1.4f);
            TextMeshProUGUI text = HudSketchLayout.Caption(rect, string.Empty, size);
            text.alignment = TextAlignmentOptions.Center;
            return text;
        }

        // Centres a piece on a point given as fractions of the bar, measured from its top-left corner.
        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x * Width, -y * Height);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Fit(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            GameObject item = new GameObject(name, typeof(RectTransform));
            item.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = (RectTransform)item.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            return rect;
        }

        private static Image Picture(string name, Transform parent, Sprite sprite)
        {
            Image image = Rect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static void Set(SerializedObject serialized, string name, Object value)
        {
            serialized.FindProperty(name).objectReferenceValue = value;
        }

        private static void SetArray(SerializedObject serialized, string name, Sprite[] values)
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
