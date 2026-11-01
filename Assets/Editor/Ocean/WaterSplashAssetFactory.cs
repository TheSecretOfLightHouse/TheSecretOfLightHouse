using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lighthouse.EditorTools.Ocean
{
    public static class WaterSplashAssetFactory
    {
        private const string TextureFolder = "Assets/06.Textures/World/Splash";
        private const string MaterialFolder = "Assets/07.Materials/World/Splash";
        private const string ParticleShaderName = "Universal Render Pipeline/Particles/Unlit";
        private const int SoftSize = 64;
        private const int RingSize = 128;

        public static Material BuildBodyMaterial()
        {
            return BuildMaterial("WaterSplash_Body", null, new Color(0.85f, 1f, 1f, 0.92f));
        }

        public static Material BuildSoftMaterial()
        {
            Texture2D soft = BuildTexture("WaterSplash_Soft", SoftSize, SoftAlpha);
            return BuildMaterial("WaterSplash_Soft", soft, Color.white);
        }

        public static Material BuildRingMaterial()
        {
            Texture2D ring = BuildTexture("WaterSplash_Ring", RingSize, RingAlpha);
            return BuildMaterial("WaterSplash_Ring", ring, Color.white);
        }

        private delegate float AlphaFunction(float radius);

        private static float SoftAlpha(float radius)
        {
            return Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.3f, 1f, radius));
        }

        private static float RingAlpha(float radius)
        {
            float ring = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.78f, radius))
                * Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.84f, 0.99f, radius));
            return Mathf.Max(ring, 0.1f * (1f - radius));
        }

        private static Texture2D BuildTexture(string name, int size, AlphaFunction alphaAt)
        {
            EnsureFolder(TextureFolder);
            string path = $"{TextureFolder}/{name}.png";
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float radius = new Vector2(x - half, y - half).magnitude / half;
                    float alpha = radius >= 1f ? 0f : Mathf.Clamp01(alphaAt(radius));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material BuildMaterial(string name, Texture2D texture, Color color)
        {
            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            Shader shader = Shader.Find(ParticleShaderName);
            if (shader == null)
            {
                Debug.LogError($"[{nameof(WaterSplashAssetFactory)}] Shader not found: {ParticleShaderName}");
                return null;
            }
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", 2f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", texture);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }
            AssetDatabase.CreateFolder(Path.GetDirectoryName(folder).Replace('\\', '/'), Path.GetFileName(folder));
        }
    }
}
