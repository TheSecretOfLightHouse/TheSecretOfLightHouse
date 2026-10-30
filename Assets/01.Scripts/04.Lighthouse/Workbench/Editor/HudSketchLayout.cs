using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Lighthouse.Workbench.Editor
{
    // Shared placement for the HUDs built from the 2026-10-29 in-game HUD mockup. Positions and sizes are the mockup's
    // pixels (1672x941), scaled onto the 1920x1080 canvas. Each piece keeps its distance from the screen edge or centre
    // named by its anchor, so a wider screen pushes the groups apart instead of stretching them. The parent of a placed
    // piece must cover the whole canvas. Sprites are the pieces cut from the mockup's two parts sheets.
    internal static class HudSketchLayout
    {
        public const string Folder = "Assets/03.Images/InGameHud/";
        // The Esc row leads the HUD's key hints; screens that hide the HUD draw the same row in the same place.
        public const string ExitKeySprite = "hud_key_esc";
        public const string ExitCaption = "나가기";
        public const float ExitKeyWidth = 47f;
        // Caption sizes in mockup pixels (2026-10-31): a key hint row, and a station's E prompt with its larger key.
        public const float KeyCaptionSize = 22f;
        public const float PromptCaptionSize = 28f;
        private const string CaptionFontPath = "Assets/09.Fonts/NotoSansKR/NotoSansKR-Bold SDF.asset";
        private const string CaptionMaterialPath = "Assets/09.Fonts/NotoSansKR/NotoSansKR-Bold SDF Outline.mat";
        public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        public static readonly Vector2 Top = new Vector2(0.5f, 1f);
        public static readonly Vector2 TopRight = new Vector2(1f, 1f);
        public static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        public static readonly Vector2 Bottom = new Vector2(0.5f, 0f);
        public static readonly Color Cream = new Color(0.96f, 0.89f, 0.74f);
        public static readonly Color Gold = new Color(0.98f, 0.73f, 0.15f);
        public static readonly Color Hit = new Color(1f, 0.35f, 0.28f);
        // The card frame is hollow, so the lens sits on this.
        public static readonly Color CardBacking = new Color(0.16f, 0.11f, 0.06f, 0.95f);
        private static readonly Color Shadow = new Color(0.1f, 0.07f, 0.04f, 0.85f);
        private const float CanvasWidth = 1920f;
        private const float CanvasHeight = 1080f;
        private const float Scale = CanvasWidth / 1672f;

        public static void ImportSprites()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);

                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                    && importer.alphaIsTransparency && importer.mipmapEnabled
                    && importer.filterMode == FilterMode.Trilinear)
                {
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                // Mipmaps keep the drawn-in lettering readable when a small window shrinks these large pictures.
                importer.mipmapEnabled = true;
                importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Trilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
        }

        // One top-right key hint row in mockup pixels: a key cap and its caption, 1.12x the mockup's size. Every key is
        // centred in one shared column and every caption starts at one shared left edge, so the rows line up whatever
        // the key's width. The caption box is as tall as the key, so its text sits on the key's middle.
        public static void KeyRow(int row, float keyWidth, out Rect key, out Rect label)
        {
            const float size = 1.12f;
            const float right = 1648f;
            // Tucked into the corner, as far from the top as the captions are from the right (2026-10-30). The final
            // battle strip's health side reaches under the first rows while it shows.
            const float top = 24f;
            float labelX = right - 62f * size;
            float columnWidth = 50f * size;
            float columnX = labelX - 14f * size - columnWidth;
            float y = top + row * 46f * size;
            float width = keyWidth * size;
            key = new Rect(columnX + (columnWidth - width) * 0.5f, y, width, 36f * size);
            label = new Rect(labelX, y, right + 24f - labelX, 36f * size);
        }

        // Every key hint caption and station prompt shares the HUD's TMP look (NotoSansKR Bold with its outline
        // material, cream), one line from the left edge, centred on its box's height.
        public static TextMeshProUGUI Caption(RectTransform rect, string caption, float fontSize)
        {
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = LoadAsset<TMP_FontAsset>(CaptionFontPath);
            text.fontSharedMaterial = LoadAsset<Material>(CaptionMaterialPath);
            text.fontSize = fontSize;
            text.color = Cream;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.text = caption;
            return text;
        }

        // A key hint caption on a canvas laid out by this class, placed and sized in mockup pixels.
        public static TextMeshProUGUI KeyCaption(string name, Transform parent, string caption, Vector2 anchor, Rect place)
        {
            return Caption(Rect(name, parent, anchor, place.x, place.y, place.width, place.height), caption, KeyCaptionSize * Scale);
        }

        // A station's E prompt above the bottom centre: the E key cap and its caption, in mockup pixels times unit
        // (canvas units per mockup pixel), so prompts on canvases of different reference sizes look the same. The
        // prompt is as wide as key plus caption, so it stays centred. Replaces the key and label already there.
        public static void BuildPrompt(RectTransform prompt, string caption, float unit)
        {
            foreach (string name in new[] { "Key", "Label" })
            {
                Transform old = prompt.Find(name);

                if (old)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }

            RectTransform key = NewRect("Key", prompt);
            Corner(key, 0f, 1f * unit, 47f * unit, 46f * unit);
            Image keyImage = AddSprite(key, "hud_key_e");
            keyImage.preserveAspect = true;
            RectTransform label = NewRect("Label", prompt);
            Corner(label, 56f * unit, 1f * unit, 0f, 46f * unit);
            TextMeshProUGUI text = Caption(label, caption, PromptCaptionSize * unit);
            // A dynamic font measures characters its atlas lacks as nothing, and text on an inactive object is not set
            // up to measure, so the glyphs go in first and the prompt shows while measuring.
            text.font.TryAddCharacters(caption);
            bool wasActive = prompt.gameObject.activeSelf;
            prompt.gameObject.SetActive(true);
            float width = Mathf.Ceil(text.GetPreferredValues(caption).x);
            prompt.gameObject.SetActive(wasActive);
            label.sizeDelta = new Vector2(width, label.sizeDelta.y);
            prompt.sizeDelta = new Vector2(56f * unit + width, 48f * unit);
        }

        private static void Corner(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static T LoadAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (!asset)
            {
                throw new System.InvalidOperationException($"Missing caption asset: {path}");
            }

            return asset;
        }

        // A layer that covers the whole canvas, for groups that show and hide or move together.
        public static RectTransform Layer(string name, Transform parent)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchor, float x, float y, float width, float height)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x * Scale - anchor.x * CanvasWidth, (1f - anchor.y) * CanvasHeight - y * Scale);
            rect.sizeDelta = new Vector2(width * Scale, height * Scale);
            return rect;
        }

        public static Image Picture(string name, Transform parent, string sprite, Vector2 anchor, float x, float y, float width, float height)
        {
            Image image = AddSprite(Rect(name, parent, anchor, x, y, width, height), sprite);
            image.preserveAspect = true;
            return image;
        }

        public static Image Backing(string name, Transform parent, Color color, Vector2 anchor, float x, float y, float width, float height)
        {
            Image image = Rect(name, parent, anchor, x, y, width, height).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Text Label(string name, Transform parent, string caption, float size, Color color, Vector2 anchor, float x, float y, float width, float height, TextAnchor alignment)
        {
            RectTransform rect = Rect(name, parent, anchor, x, y, width, height);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = Mathf.RoundToInt(size * Scale);
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = alignment;
            text.text = caption;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            // Over open sea and storms, so every number gets a dark edge like the cut labels.
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = Shadow;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        // Track, fill and optional frame, back to front. The fill is cut from the left by fillAmount. The inset is the
        // frame sprite's hollow (plus a little under its ring) as fractions of the frame, so the pieces meet at any size.
        public static Image Bar(string name, Transform parent, Vector2 anchor, float x, float y, float width, float height,
            string track, string fill, string frame, Rect inset)
        {
            RectTransform bar = Rect(name, parent, anchor, x, y, width, height);
            Vector2 size = bar.sizeDelta;
            Inner("Track", bar, track, size, inset);
            Image fillImage = Inner("Fill", bar, fill, size, inset);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 1f;

            if (frame != null)
            {
                Inner("Frame", bar, frame, size, new Rect(0f, 0f, 1f, 1f));
            }

            return fillImage;
        }

        private static Image Inner(string name, RectTransform parent, string sprite, Vector2 size, Rect inset)
        {
            RectTransform rect = NewRect(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(size.x * inset.x, -size.y * inset.y);
            rect.sizeDelta = new Vector2(size.x * inset.width, size.y * inset.height);
            return AddSprite(rect, sprite);
        }

        private static Image AddSprite(RectTransform rect, string sprite)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = Load(sprite);
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static Sprite Load(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}{name}.png");

            if (!sprite)
            {
                throw new System.InvalidOperationException($"Missing HUD sprite: {Folder}{name}");
            }

            return sprite;
        }
    }
}
