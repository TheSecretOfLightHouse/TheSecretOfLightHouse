using System;
using UnityEngine;

namespace Lighthouse.UI.SharedMap.View
{
    // A round raster brush avoids angular joins and keeps mesh size independent of drawing length.
    public sealed class SharedMapBrush : IDisposable
    {
        public const int Resolution = 1024;
        private readonly Color32[] _pixels = new Color32[Resolution * Resolution];
        private readonly Texture2D _texture;

        public Texture2D Texture => _texture;

        public SharedMapBrush()
        {
            _texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
            {
                name = "SharedMapPaint", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
        }

        public void Clear()
        {
            Array.Clear(_pixels, 0, _pixels.Length);
            Color grid = new Color(0.35f, 0.24f, 0.12f, 0.035f);
            for (int i = 1; i < 10; i++)
            {
                float offset = i / 10f;
                Paint(new Vector2(offset, 0f), new Vector2(offset, 1f), 0.55f, grid);
                Paint(new Vector2(0f, offset), new Vector2(1f, offset), 0.55f, grid);
            }
        }

        public void Paint(Vector2 from, Vector2 to, float radius, Color color)
        {
            Vector2 a = from * (Resolution - 1);
            Vector2 b = to * (Resolution - 1);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / Mathf.Max(0.5f, radius * 0.45f)));
            for (int i = 0; i <= steps; i++)
            {
                Stamp(Vector2.Lerp(a, b, i / (float)steps), radius, color);
            }
        }

        public void Upload()
        {
            _texture.SetPixels32(_pixels);
            _texture.Apply(false, false);
        }

        public void Dispose()
        {
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(_texture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(_texture);
            }
        }

        private void Stamp(Vector2 centre, float radius, Color ink)
        {
            int left = Mathf.Max(0, Mathf.FloorToInt(centre.x - radius - 1f));
            int right = Mathf.Min(Resolution - 1, Mathf.CeilToInt(centre.x + radius + 1f));
            int bottom = Mathf.Max(0, Mathf.FloorToInt(centre.y - radius - 1f));
            int top = Mathf.Min(Resolution - 1, Mathf.CeilToInt(centre.y + radius + 1f));
            for (int y = bottom; y <= top; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), centre);
                    float alpha = Mathf.Clamp01(radius + 0.5f - distance) * ink.a;
                    if (alpha <= 0f)
                    {
                        continue;
                    }
                    int index = y * Resolution + x;
                    Color existing = _pixels[index];
                    float combined = alpha + existing.a * (1f - alpha);
                    Color mixed = (ink * alpha + existing * (existing.a * (1f - alpha))) / combined;
                    mixed.a = combined;
                    _pixels[index] = mixed;
                }
            }
        }
    }
}
