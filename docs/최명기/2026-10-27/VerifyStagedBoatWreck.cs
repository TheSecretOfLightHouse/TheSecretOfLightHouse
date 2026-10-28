using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Lighthouse.Player.Boat.View;

public static class VerifyStagedBoatWreck
{
    public static object Main()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02.Prefabs/Player/BoatWreckEffect.prefab");
        MethodInfo pose = typeof(BoatWreckEffect).GetMethod("ApplyPose", BindingFlags.Instance | BindingFlags.NonPublic);
        float[] times = { 0f, 0.05f, 0.1f, 0.2f, 0.3f, 0.55f, 1.35f, 2.6f, 4.2f, 6.2f, 9f, 30f };
        float maxDifference = 0f;
        foreach (float time in times)
        {
            GameObject sequential = UnityEngine.Object.Instantiate(prefab);
            GameObject late = UnityEngine.Object.Instantiate(prefab);
            try
            {
                sequential.transform.rotation = late.transform.rotation = Quaternion.Euler(12f, 43f, -8f);
                BoatWreckEffect effect = sequential.GetComponent<BoatWreckEffect>();
                effect.Initialize(0f);
                for (float frame = 0f; frame < time; frame += 1f / 60f)
                {
                    pose.Invoke(effect, new object[] { frame });
                }
                pose.Invoke(effect, new object[] { time });
                late.GetComponent<BoatWreckEffect>().Initialize(time);
                BoatWreckPiece[] a = sequential.GetComponentsInChildren<BoatWreckPiece>(true);
                BoatWreckPiece[] b = late.GetComponentsInChildren<BoatWreckPiece>(true);
                if (a.Length != 34 || a.Length != b.Length) { throw new Exception("Missing pieces."); }
                for (int i = 0; i < a.Length; i++)
                {
                    float difference = Vector3.Distance(a[i].transform.position, b[i].transform.position);
                    maxDifference = Mathf.Max(maxDifference, difference);
                    if (difference > 0.00001f || Quaternion.Angle(a[i].transform.rotation, b[i].transform.rotation) > 0.01f)
                    {
                        throw new Exception("Late observer pose differs: " + a[i].name);
                    }
                    if (float.IsNaN(a[i].transform.position.y)) { throw new Exception("Invalid position."); }
                }
                Transform bow = sequential.transform.Find("Bow");
                if (time >= 0.05f)
                {
                    foreach (BoatWreckPiece piece in a.Take(22))
                    {
                        var data = new SerializedObject(piece);
                        if (data.FindProperty("_releaseTime").floatValue > 0.016f
                            || data.FindProperty("_releaseDuration").floatValue > 0.23f
                            || !data.FindProperty("_impulseRelease").boolValue)
                        {
                            throw new Exception("Simultaneous impulse timing or curve mismatch: " + piece.name);
                        }
                    }
                    if (a.Where(p => p.name.StartsWith("FlyingTimber_")).Any(p => !p.transform.GetChild(0).gameObject.activeSelf))
                    {
                        throw new Exception("Timber did not appear together.");
                    }
                }
                if (time >= 9f)
                {
                    if (bow.position.y > -1f || sequential.transform.Find("Stern").position.y > -1f)
                    {
                        throw new Exception("Hull did not sink.");
                    }
                    foreach (BoatWreckPiece timber in a.Where(p => p.name.StartsWith("FlyingTimber_")))
                    {
                        if (timber.transform.position.y < 0.05f || timber.transform.position.y > 0.13f)
                        {
                            throw new Exception("Timber left waterline: " + timber.name);
                        }
                    }
                    if (!float.IsPositiveInfinity(effect.Duration)) { throw new Exception("Floating timber expires."); }
                }
                Vector3 before = bow.position;
                pose.Invoke(effect, new object[] { time });
                effect.Initialize(0f);
                if (Vector3.Distance(before, bow.position) > 0.00001f) { throw new Exception("Repeated initialization drift."); }
                int missing = sequential.GetComponentsInChildren<Transform>(true)
                    .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                if (missing != 0) { throw new Exception("Missing script."); }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sequential);
                UnityEngine.Object.DestroyImmediate(late);
            }
        }

        foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!filter.sharedMesh || filter.sharedMesh.vertexCount == 0) { throw new Exception("Empty mesh."); }
            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (!renderer || renderer.sharedMaterials.Any(m => !m || m.shader.name == "Hidden/InternalErrorShader"))
            {
                throw new Exception("Missing material.");
            }
        }

        GameObject boat = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02.Prefabs/Player/Boat.prefab");
        Mesh source = boat.transform.Find("Model").GetComponent<MeshFilter>().sharedMesh;
        double originalArea = Area(source);
        double fracturedArea = prefab.GetComponentsInChildren<BoatWreckPiece>(true).Take(8)
            .Sum(p => Area(p.GetComponent<MeshFilter>().sharedMesh));
        double ratio = fracturedArea / originalArea;
        if (Math.Abs(1d - ratio) > 0.01d) { throw new Exception("Exterior surface area changed by more than 1%: " + ratio); }
        return new { stages = times.Length, pieces = 34, maxDifference,
            exteriorAreaRatio = ratio, remainsUntilSceneUnload = float.IsPositiveInfinity(prefab.GetComponent<BoatWreckEffect>().Duration),
            missingScripts = 0, missingMeshesOrMaterials = 0, playModeTested = false };
    }

    private static double Area(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.GetTriangles(0);
        double area = 0d;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            area += Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]],
                vertices[triangles[i + 2]] - vertices[triangles[i]]).magnitude * 0.5d;
        }
        return area;
    }
}
