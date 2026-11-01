using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Lighthouse.Player.Boat.View;

public static class VerifyWreckShadows
{
    public static object Main()
    {
        string[] paths = AssetDatabase.FindAssets("BoatWreckEffect t:Prefab")
            .Select(AssetDatabase.GUIDToAssetPath).ToArray();
        int checkedRenderers = 0;
        int visibilityChecks = 0;
        foreach (string path in paths)
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Any(r => r.shadowCastingMode != ShadowCastingMode.Off))
            {
                throw new Exception("Wreck still casts shadows: " + path);
            }
            if (root.GetComponentsInChildren<BoatWreckPiece>(true).Length != 34)
            {
                throw new Exception("Missing wreck pieces: " + path);
            }
            checkedRenderers += renderers.Length;
            GameObject instance = UnityEngine.Object.Instantiate(root);
            try
            {
                instance.GetComponent<BoatWreckEffect>().Initialize(0f);
                foreach (BoatWreckPiece piece in instance.GetComponentsInChildren<BoatWreckPiece>(true))
                {
                    var data = new SerializedObject(piece);
                    bool afloat = data.FindProperty("_stayAfloat").boolValue;
                    float end = data.FindProperty("_sinkTime").floatValue
                        + Mathf.Max(0.01f, data.FindProperty("_sinkDuration").floatValue);
                    foreach (float time in new[] { end - 0.001f, end + 0.001f, 30f, 0f })
                    {
                        piece.ApplyPose(time);
                        if (piece.gameObject.activeSelf != (afloat || time < end))
                        {
                            throw new Exception("Incorrect visibility: " + path + "/" + piece.name + " at " + time);
                        }
                        visibilityChecks++;
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        Scene scene = EditorSceneManager.NewPreviewScene();
        RenderTexture previous = RenderTexture.active;
        var target = new RenderTexture(640, 480, 24);
        var sheet = new Texture2D(1280, 480, TextureFormat.RGB24, false);
        Material water = null;
        string output = Path.GetFullPath("docs/최명기/2026-10-27/난파-그림자-비교.png");
        try
        {
            var cameraObject = new GameObject("WreckShadowPreview");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.scene = scene;
            camera.cameraType = CameraType.Preview;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.45f, 0.48f);
            camera.transform.position = new Vector3(9f, 13f, -14f);
            camera.transform.LookAt(Vector3.zero);
            camera.targetTexture = target;
            var lightObject = new GameObject("WreckShadowSun");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            SceneManager.MoveGameObjectToScene(plane, scene);
            plane.transform.localScale = Vector3.one * 10f;
            water = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            water.color = new Color(0.04f, 0.45f, 0.48f);
            plane.GetComponent<Renderer>().sharedMaterial = water;
            plane.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[0]);
            for (int i = 0; i < 2; i++)
            {
                GameObject instance = UnityEngine.Object.Instantiate(prefab);
                SceneManager.MoveGameObjectToScene(instance, scene);
                instance.transform.localScale = Vector3.one * 3f;
                instance.GetComponent<BoatWreckEffect>().Initialize(6.2f);
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.shadowCastingMode = i == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                }
                camera.Render();
                RenderTexture.active = target;
                var tile = new Texture2D(640, 480, TextureFormat.RGB24, false);
                tile.ReadPixels(new Rect(0, 0, 640, 480), 0, 0);
                tile.Apply();
                sheet.SetPixels(i * 640, 0, 640, 480, tile.GetPixels());
                UnityEngine.Object.DestroyImmediate(tile);
                UnityEngine.Object.DestroyImmediate(instance);
            }
            sheet.Apply();
            File.WriteAllBytes(output, sheet.EncodeToPNG());
            return new { prefabs = paths.Length, checkedRenderers, visibilityChecks, output, comparison = "Left: shadows on, right: shadows off. Isolated flat water, not gameplay." };
        }
        finally
        {
            RenderTexture.active = previous;
            EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(sheet);
            if (water) { UnityEngine.Object.DestroyImmediate(water); }
        }
    }
}
