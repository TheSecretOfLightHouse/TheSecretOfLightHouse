using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Lighthouse.Player.Boat.View;

public static class InspectWreckRendering
{
    public static object Main()
    {
        bool live = Application.isPlaying;
        Scene scene = live ? SceneManager.GetActiveScene() : EditorSceneManager.NewPreviewScene();
        Vector3 origin = live ? new Vector3(0f, -0.25f, -60f) : Vector3.zero;
        var rt = new RenderTexture(640, 480, 24);
        var sheet = new Texture2D(2560, 1440, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        string output = Path.GetFullPath("docs/최명기/2026-10-27/난파-원인-분리" + (live ? "-실제바다" : "") + ".png");
        GameObject co = null;
        GameObject lo = null;
        GameObject plane = null;
        try
        {
            co = new GameObject("InspectionCamera");
            SceneManager.MoveGameObjectToScene(co, scene);
            var camera = co.AddComponent<Camera>();
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.4f, 0.43f);
            camera.enabled = false;
            camera.transform.position = origin + new Vector3(3f, 5f, -6f);
            camera.transform.LookAt(origin + new Vector3(0f, -1f, 0f));
            camera.targetTexture = rt;
            camera.nearClipPlane = 0.1f;
            lo = new GameObject("InspectionSun");
            SceneManager.MoveGameObjectToScene(lo, scene);
            var light = lo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            lo.SetActive(!live);
            plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            SceneManager.MoveGameObjectToScene(plane, scene);
            plane.transform.localScale = Vector3.one * 40f;
            plane.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/07.Materials/World/OceanWater_Clear.mat");
            plane.SetActive(!live);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02.Prefabs/Player/BoatWreckEffect.prefab");
            float[] times = { 0.3f, 3.4f, 4.5f };
            for (int row = 0; row < 3; row++)
            {
                for (int mode = 0; mode < 4; mode++)
                {
                    var instance = UnityEngine.Object.Instantiate(prefab);
                    SceneManager.MoveGameObjectToScene(instance, scene);
                    instance.transform.localScale = Vector3.one * 3f;
                    instance.transform.position = origin;
                    instance.GetComponent<BoatWreckEffect>().Initialize(times[row]);
                    foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                    {
                        bool foam = renderer.name == "Foam";
                        bool splinter = renderer.name.StartsWith("TornTimber_");
                        bool flying = renderer.name == "Timber";
                        if (mode == 1 && (foam || splinter || flying)) renderer.enabled = false;
                        if (mode == 2 && !splinter) renderer.enabled = false;
                        if (mode == 3 && !foam) renderer.enabled = false;
                    }
                    var request = new RenderPipeline.StandardRequest { destination = rt };
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderTexture.active = rt;
                    var tile = new Texture2D(640, 480, TextureFormat.RGB24, false);
                    tile.ReadPixels(new Rect(0, 0, 640, 480), 0, 0);
                    tile.Apply();
                    sheet.SetPixels(mode * 640, (2-row) * 480, 640, 480, tile.GetPixels());
                    UnityEngine.Object.DestroyImmediate(tile);
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
            sheet.Apply();
            File.WriteAllBytes(output, sheet.EncodeToPNG());
            return new { output, columns = "all, hull, splinters, foam", rows = times };
        }
        finally
        {
            RenderTexture.active = previous;
            if (!live) EditorSceneManager.ClosePreviewScene(scene);
            else
            {
                UnityEngine.Object.DestroyImmediate(co);
                UnityEngine.Object.DestroyImmediate(lo);
                UnityEngine.Object.DestroyImmediate(plane);
            }
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(sheet);
        }
    }
}
