using UnityEngine;

namespace Lighthouse.World.Islands.Editor
{
    internal sealed class TerrainSplatSampler
    {
        public const int MaxLayers = 4;

        private readonly float[,,] _maps;
        private readonly int _width;
        private readonly int _height;
        private readonly int _layers;

        public TerrainSplatSampler(TerrainData data)
        {
            _width = data.alphamapWidth;
            _height = data.alphamapHeight;
            _layers = Mathf.Min(data.alphamapLayers, MaxLayers);
            _maps = data.GetAlphamaps(0, 0, _width, _height);
        }

        public Color32 Sample(float u, float v)
        {
            float x = Mathf.Clamp(u * _width - 0.5f, 0f, _width - 1f);
            float y = Mathf.Clamp(v * _height - 0.5f, 0f, _height - 1f);
            int x0 = (int)x;
            int y0 = (int)y;
            int x1 = Mathf.Min(x0 + 1, _width - 1);
            int y1 = Mathf.Min(y0 + 1, _height - 1);
            float fx = x - x0;
            float fy = y - y0;

            Color weights = Color.clear;
            for (int layer = 0; layer < _layers; layer++)
            {
                float top = Mathf.Lerp(_maps[y0, x0, layer], _maps[y0, x1, layer], fx);
                float bottom = Mathf.Lerp(_maps[y1, x0, layer], _maps[y1, x1, layer], fx);
                weights[layer] = Mathf.Lerp(top, bottom, fy);
            }

            return weights;
        }
    }
}
