using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Lighthouse.Player.Boat.View;

public static class VerifyWreckLighting
{
    public static object Main()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var rt = new RenderTexture(640, 480, 24, RenderTextureFormat.ARGBFloat);
        var sheet = new Texture2D(1280, 480, TextureFormat.RGBAFloat, false);
        var previous = RenderTexture.active;
        var results = new List<object>();
        string output = Path.GetFullPath("docs/최명기/2026-10-27/난파-법선-비교.png");
        try
        {
            var co = new GameObject("WreckLightingCamera");
            SceneManager.MoveGameObjectToScene(co, scene);
            var camera = co.AddComponent<Camera>();
            camera.scene = scene;
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.gray;
            camera.allowHDR = true;
            camera.transform.position = new Vector3(3f, 2.5f, -4f);
            camera.transform.LookAt(Vector3.zero);
            camera.targetTexture = rt;
            var lo = new GameObject("WreckLightingSun");
            SceneManager.MoveGameObjectToScene(lo, scene);
            var light = lo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 4f;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            for (int mode = 0; mode < 2; mode++)
            {
                var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02.Prefabs/Player/BoatWreckEffect.prefab"));
                SceneManager.MoveGameObjectToScene(instance, scene);
                instance.transform.localScale = Vector3.one * 3f;
                instance.GetComponent<BoatWreckEffect>().Initialize(0.3f);
                var clones = new List<Mesh>();
                if (mode == 0)
                {
                    foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (filter.sharedMesh.subMeshCount < 2) continue;
                        var mesh = UnityEngine.Object.Instantiate(filter.sharedMesh);
                        clones.Add(mesh);
                        var normals = mesh.normals;
                        var positions = mesh.vertices;
                        var triangles = mesh.GetTriangles(1);
                        for (int i = 0; i < triangles.Length; i += 3)
                        {
                            Vector3 normal = Vector3.Cross(positions[triangles[i+1]] - positions[triangles[i]], positions[triangles[i+2]] - positions[triangles[i]]).normalized;
                            for (int j = 0; j < 3; j++) normals[triangles[i+j]] = normal;
                        }
                        mesh.normals = normals;
                        mesh.tangents = new Vector4[0];
                        filter.sharedMesh = mesh;
                    }
                }
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = rt });
                RenderTexture.active = rt;
                var tile = new Texture2D(640, 480, TextureFormat.RGBAFloat, false);
                tile.ReadPixels(new Rect(0, 0, 640, 480), 0, 0);
                tile.Apply();
                Color[] pixels = tile.GetPixels();
                int nonfinite = pixels.Count(p => !float.IsFinite(p.r) || !float.IsFinite(p.g) || !float.IsFinite(p.b));
                int black = pixels.Count(p => p.r < 0.002f && p.g < 0.002f && p.b < 0.002f);
                results.Add(new { mode, nonfinite, black });
                sheet.SetPixels(mode*640, 0, 640, 480, pixels);
                UnityEngine.Object.DestroyImmediate(tile);
                UnityEngine.Object.DestroyImmediate(instance);
                foreach (Mesh mesh in clones) UnityEngine.Object.DestroyImmediate(mesh);
            }
            sheet.Apply();
            File.WriteAllBytes(output, sheet.EncodeToPNG());
            return new { results, output, baseline = "Reconstructed old rim normals/tangent omission, not a recorded original frame" };
        }
        finally
        {
            RenderTexture.active = previous;
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(sheet);
        }
    }
}
