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
                    && importer.alphaIsTransparency && !importer.mipmapEnabled)
                {
                    continue;
                }

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
