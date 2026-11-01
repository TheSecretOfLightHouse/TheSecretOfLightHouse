using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RepairWreckMeshFrames
{
    public static object Main()
    {
        int changedMeshes = 0;
        int repairedNormals = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/04.Models/Player/Wreck" }))
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guid));
            Vector3[] positions = mesh.vertices;
            Vector3[] normals = mesh.normals;
            if (normals.Length != positions.Length) throw new Exception("Missing normals: " + mesh.name);
            int repaired = 0;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                int[] triangles = mesh.GetTriangles(submesh);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 face = Vector3.Cross(positions[triangles[i+1]] - positions[triangles[i]],
                        positions[triangles[i+2]] - positions[triangles[i]]);
                    float length = Mathf.Sqrt(face.sqrMagnitude);
                    Vector3 fallback = length > 1e-15f ? face / length : Vector3.up;
                    for (int corner = 0; corner < 3; corner++)
                    {
                        int index = triangles[i + corner];
                        Vector3 normal = normals[index];
                        if (!float.IsFinite(normal.x) || !float.IsFinite(normal.y) || !float.IsFinite(normal.z)
                            || normal.sqrMagnitude < 1e-8f)
                        {
                            normals[index] = fallback;
                            repaired++;
                        }
                    }
                }
            }
            bool needsTangents = mesh.uv.Length == mesh.vertexCount && mesh.tangents.Length != mesh.vertexCount;
            if (repaired == 0 && !needsTangents) continue;
            mesh.normals = normals;
            if (mesh.uv.Length == mesh.vertexCount) mesh.RecalculateTangents();
            if (mesh.normals.Any(n => !float.IsFinite(n.x) || !float.IsFinite(n.y) || !float.IsFinite(n.z)
                || n.sqrMagnitude < 1e-8f)) throw new Exception("Invalid normal remains: " + mesh.name);
            EditorUtility.SetDirty(mesh);
            AssetDatabase.SaveAssetIfDirty(mesh);
            changedMeshes++;
            repairedNormals += repaired;
        }
        return new { changedMeshes, repairedNormals, positionsAndTrianglesUnchanged = true };
    }
}
