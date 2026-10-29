using UnityEngine;

namespace Lighthouse.World.Islands.Editor
{
    internal static class IslandGroundMaterialBuilder
    {
        private const string ShaderName = "Lighthouse/World/IslandTerrainBlend";
        private const float MinTileSize = 0.01f;

        public static Material Create(TerrainData data)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader);
            TerrainLayer[] layers = data.terrainLayers;
            Vector4 tiles = new Vector4(2f, 2f, 2f, 2f);
            Vector4 smoothness = Vector4.zero;
            int count = Mathf.Min(layers.Length, TerrainSplatSampler.MaxLayers);
            for (int i = 0; i < count; i++)
            {
                if (layers[i] == null)
                {
                    continue;
                }

                material.SetTexture($"_Tex{i}", layers[i].diffuseTexture);
                tiles[i] = Mathf.Max(MinTileSize, layers[i].tileSize.x);
                smoothness[i] = layers[i].smoothness;
            }

            material.SetVector("_TileSizes", tiles);
            material.SetVector("_Smoothness", smoothness);
            return material;
        }
    }
}
