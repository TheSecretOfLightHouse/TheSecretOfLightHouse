using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Lighthouse.Player.Boat.View;

public static class PreviewStagedBoatWreck
{
    public static string Main()
    {
        return Render(false);
    }

    public static string Burst()
    {
        return Render(true);
    }

    private static string Render(bool burst)
    {
        string output = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../docs/최명기/2026-10-27/" + (burst ? "난파-순간-미리보기.png" : "난파-단계-미리보기.png")));
        Scene scene = EditorSceneManager.NewPreviewScene();
        var sheet = new Texture2D(1500, 840, TextureFormat.RGB24, false);
        var rt = new RenderTexture(500, 420, 24);
        RenderTexture previous = RenderTexture.active;
        Material water = null;
        try
        {
            var cameraObject = new GameObject("WreckPreviewCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.cameraType = CameraType.Preview;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.19f, 0.23f, 0.27f);
            camera.orthographic = true;
            camera.orthographicSize = 1.7f;
            camera.transform.position = new Vector3(3.3f, 2.8f, 3.8f);
            camera.transform.LookAt(new Vector3(0f, 0.3f, 0f));
            camera.targetTexture = rt;

            var lightObject = new GameObject("WreckPreviewLight");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2.2f;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            var fillObject = new GameObject("Fill");
            SceneManager.MoveGameObjectToScene(fillObject, scene);
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.8f;
            fill.transform.rotation = Quaternion.Euler(35f, 145f, 0f);

            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            SceneManager.MoveGameObjectToScene(plane, scene);
            plane.transform.position = new Vector3(0f, 0.075f, 0f);
            water = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            water.color = new Color(0.08f, 0.25f, 0.29f);
            water.SetFloat("_Smoothness", 0.65f);
            plane.GetComponent<MeshRenderer>().sharedMaterial = water;

            float[] times = burst ? new[] { 0f, 0.04f, 0.08f, 0.12f, 0.22f, 6.2f }
                : new[] { 0f, 0f, 0f, 0f, 0.4f, 6.2f };
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02.Prefabs/Player/BoatWreckEffect.prefab");
            GameObject boat = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02.Prefabs/Player/Boat.prefab");
            var damageData = new SerializedObject(boat.GetComponent<BoatHullDamage>());
            for (int i = 0; i < times.Length; i++)
            {
                GameObject instance;
                if (!burst && i < 4)
                {
                    instance = new GameObject("DamagePreview");
                    instance.transform.localScale = boat.transform.localScale;
                    var visual = new GameObject("Model");
                    visual.transform.SetParent(instance.transform, false);
                    Transform source = boat.transform.Find("Model");
                    visual.transform.localPosition = source.localPosition;
                    visual.transform.localRotation = source.localRotation;
                    visual.transform.localScale = source.localScale;
                    visual.AddComponent<MeshFilter>().sharedMesh = i == 0 ? source.GetComponent<MeshFilter>().sharedMesh
                        : (Mesh)damageData.FindProperty("_damageMeshes").GetArrayElementAtIndex(i - 1).objectReferenceValue;
                    Material exterior = source.GetComponent<MeshRenderer>().sharedMaterial;
                    visual.AddComponent<MeshRenderer>().sharedMaterials = new[] { exterior };
                }
                else
                {
                    instance = UnityEngine.Object.Instantiate(prefab);
                }
                SceneManager.MoveGameObjectToScene(instance, scene);
                if (burst || i >= 4)
                {
                    instance.GetComponent<BoatWreckEffect>().SetHullDamageStage(3);
                    instance.GetComponent<BoatWreckEffect>().Initialize(times[i]);
                }
                camera.Render();
                RenderTexture.active = rt;
                var tile = new Texture2D(500, 420, TextureFormat.RGB24, false);
                tile.ReadPixels(new Rect(0, 0, 500, 420), 0, 0);
                tile.Apply();
                sheet.SetPixels((i % 3) * 500, (1 - i / 3) * 420, 500, 420, tile.GetPixels());
                UnityEngine.Object.DestroyImmediate(tile);
                UnityEngine.Object.DestroyImmediate(instance);
            }
            sheet.Apply();
            File.WriteAllBytes(output, sheet.EncodeToPNG());
            return output;
        }
        finally
        {
            RenderTexture.active = previous;
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(sheet);
            UnityEngine.Object.DestroyImmediate(rt);
            if (water) { UnityEngine.Object.DestroyImmediate(water); }
        }
    }
}
