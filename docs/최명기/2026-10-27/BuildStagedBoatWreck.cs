using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Lighthouse.Player.Boat.View;

// Execute through Unity MCP run_script. This file is outside Assets to avoid a runtime dependency.
public static class BuildStagedBoatWreck
{
    private static string _folder;
    private static string _prefabPath;

    private struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 Uv;

        public static Vertex Lerp(Vertex a, Vertex b, float t)
        {
            return new Vertex
            {
                Position = Vector3.Lerp(a.Position, b.Position, t),
                Normal = Vector3.Lerp(a.Normal, b.Normal, t).normalized,
                Uv = Vector2.Lerp(a.Uv, b.Uv, t)
            };
        }
    }

    private sealed class Region
    {
        public string Name;
        public readonly List<Func<Vector3, float>> Cuts = new List<Func<Vector3, float>>();
        public float Release;
        public float Sink;
        public float SinkDuration;
        public Vector3 Offset;
        public Vector3 Rotation;
    }

    private static float Seam(Vector3 p)
    {
        return p.z - 0.037f * Mathf.Sin(p.x * 53f + p.y * 11f)
            - 0.018f * Mathf.Sin(p.y * 79f - p.x * 17f);
    }

    public static string Main()
    {
        return BuildFor("Boat");
    }

    public static string BuildAll()
    {
        return string.Join("\n", new[] { "Boat", "captainBoat", "gardenBoat", "TankerBoat" }.Select(BuildFor));
    }

    private static string BuildFor(string boatName)
    {
        if (EditorApplication.isPlaying)
        {
            throw new InvalidOperationException("Build only in Edit mode.");
        }

        string suffix = boatName == "Boat" ? "" : "_" + boatName;
        _folder = "Assets/04.Models/Player/Wreck/Staged" + suffix;
        _prefabPath = "Assets/02.Prefabs/Player/BoatWreckEffect" + suffix + ".prefab";
        string boatPath = "Assets/02.Prefabs/Player/" + boatName + ".prefab";
        if (!AssetDatabase.IsValidFolder(_folder))
        {
            AssetDatabase.CreateFolder("Assets/04.Models/Player/Wreck", "Staged" + suffix);
        }

        GameObject boat = AssetDatabase.LoadAssetAtPath<GameObject>(boatPath);
        MeshFilter source = boat.transform.Find("Model").GetComponent<MeshFilter>();
        Mesh mesh = source.sharedMesh;
        Matrix4x4 matrix = boat.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
        Vector3[] positions = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector2[] uvs = mesh.uv;
        int[] triangles = mesh.triangles;
        var vertices = new Vertex[positions.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = new Vertex { Position = matrix.MultiplyPoint3x4(positions[i]),
                Normal = matrix.MultiplyVector(normals[i]).normalized, Uv = uvs[i] };
        }

        Material exterior = source.GetComponent<MeshRenderer>().sharedMaterial;
        Material wood = AssetDatabase.LoadAssetAtPath<Material>(_folder + "/FreshTimber.mat");
        if (!wood)
        {
            wood = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(wood, _folder + "/FreshTimber.mat");
        }
        wood.color = new Color(0.52f, 0.285f, 0.115f);
        wood.SetFloat("_Smoothness", 0.08f);
        wood.SetFloat("_Cull", 0f);
        EditorUtility.SetDirty(wood);
        var regions = new List<Region>();
        var front = new Region { Name = "Bow", Release = 0.015f, Sink = 2.0f, SinkDuration = 2.8f,
            Offset = new Vector3(0f, -0.09f, 0.025f), Rotation = new Vector3(32f, 0f, -4f) };
        front.Cuts.Add(p => Seam(p) - 0.105f);
        regions.Add(front);
        var back = new Region { Name = "Stern", Release = 0.015f, Sink = 2f, SinkDuration = 2.8f,
            Offset = new Vector3(0f, -0.015f, 0.01f), Rotation = new Vector3(-12f, 0f, 5f) };
        back.Cuts.Add(p => -Seam(p) - 0.095f);
        regions.Add(back);

        for (int side = 0; side < 3; side++)
        {
            for (int row = 0; row < 2; row++)
            {
                int column = side;
                int half = row;
                float direction = side == 0 ? -1f : side == 2 ? 1f : 0.3f;
                var region = new Region { Name = "BrokenPlanking_" + side + "_" + row,
                    Release = 0.015f,
                    Sink = 2f, SinkDuration = 2.8f,
                    Offset = new Vector3(direction * 0.035f, -0.08f, 0.045f + row * 0.02f),
                    Rotation = new Vector3(18f + side * 6f, direction * 5f, direction * 8f) };
                region.Cuts.Add(p => Seam(p) + 0.095f);
                region.Cuts.Add(p => 0.105f - Seam(p));
                if (column > 0) { region.Cuts.Add(p => p.x - (column == 1 ? -0.115f : 0.115f)); }
                if (column < 2) { region.Cuts.Add(p => (column == 0 ? -0.115f : 0.115f) - p.x); }
                region.Cuts.Add(p => half == 0 ? -Seam(p) + 0.005f : Seam(p) - 0.005f);
                regions.Add(region);
            }
        }

        string templatePath = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath)
            ? _prefabPath : "Assets/02.Prefabs/Player/BoatWreckEffect.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(templatePath);
        try
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            var pieces = new List<BoatWreckPiece>();
            Vector3 bowPivot = Vector3.zero;
            foreach (Region region in regions)
            {
                Mesh chunk = Fracture(vertices, triangles, region);
                chunk = SaveMesh(chunk, _folder + "/" + region.Name + ".asset");
                Vector3 pivot = chunk.bounds.center;
                if (region.Name == "Bow") { bowPivot = pivot; }
                Vector3[] chunkVertices = chunk.vertices;
                for (int i = 0; i < chunkVertices.Length; i++) { chunkVertices[i] -= pivot; }
                chunk.vertices = chunkVertices;
                chunk.RecalculateBounds();
                EditorUtility.SetDirty(chunk);
                var holder = new GameObject(region.Name);
                holder.transform.SetParent(root.transform, false);
                holder.transform.localPosition = pivot;
                holder.AddComponent<MeshFilter>().sharedMesh = chunk;
                holder.AddComponent<MeshRenderer>().sharedMaterials = new[] { exterior, wood };
                BoatWreckPiece piece = holder.AddComponent<BoatWreckPiece>();
                Configure(piece, region.Release, region.Name == "Bow" || region.Name == "Stern" ? 0.22f : 0.16f, region.Offset, region.Rotation,
                    region.Sink, region.SinkDuration, 1.5f, 0.018f, false, null);
                pieces.Add(piece);
                if (region.Name == "Bow" || region.Name == "Stern")
                {
                    GameObject details = AddSplinters(holder.transform, pivot, region.Name == "Bow", wood);
                    var pieceData = new SerializedObject(piece);
                    pieceData.FindProperty("_fractureDetails").objectReferenceValue = details;
                    pieceData.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            Mesh shard = CreateShard();
            shard = SaveMesh(shard, _folder + "/TimberShard.asset");
            for (int i = 0; i < 14; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var holder = new GameObject("FlyingTimber_" + i);
                holder.transform.SetParent(root.transform, false);
                holder.transform.localPosition = new Vector3(side * (0.14f + (i % 3) * 0.045f),
                    0.16f + (i % 4) * 0.017f, (i % 5 - 2) * 0.04f);
                var visual = new GameObject("Timber");
                visual.transform.SetParent(holder.transform, false);
                visual.transform.localScale = new Vector3(0.012f + i % 3 * 0.004f,
                    0.009f + i % 2 * 0.005f, 0.055f + i % 4 * 0.025f);
                visual.AddComponent<MeshFilter>().sharedMesh = shard;
                visual.AddComponent<MeshRenderer>().sharedMaterial = wood;
                BoatWreckPiece piece = holder.AddComponent<BoatWreckPiece>();
                Configure(piece, 0.015f, 0.2f,
                    new Vector3(side * (0.055f + i % 4 * 0.012f), -0.1f, 0.04f + i % 5 * 0.015f),
                    new Vector3((i % 3 - 1) * 8f, i * 29f, side * 7f),
                    5.2f + i % 3 * 0.3f, 3.2f, 1.15f, 0.025f, true, visual);
                var floatingData = new SerializedObject(piece);
                floatingData.FindProperty("_stayAfloat").boolValue = true;
                floatingData.FindProperty("_phase").floatValue = i * 1.7f;
                floatingData.ApplyModifiedPropertiesWithoutUndo();
                visual.SetActive(false);
                pieces.Add(piece);
            }

            Material foam = AssetDatabase.LoadAssetAtPath<Material>(_folder + "/Foam.mat");
            if (!foam)
            {
                foam = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(foam, _folder + "/Foam.mat");
            }
            foam.color = new Color(0.78f, 0.93f, 0.94f);
            foam.SetFloat("_Cull", 0f);
            EditorUtility.SetDirty(foam);
            Mesh arc = SaveMesh(CreateFoamArc(), _folder + "/FoamArc.asset");
            for (int i = 0; i < 12; i++)
            {
                var holder = new GameObject("FoamRipple_" + i);
                holder.transform.SetParent(root.transform, false);
                holder.transform.localPosition = new Vector3(0f, 0.035f, 0f);
                holder.transform.localRotation = Quaternion.Euler(0f, i * 30f, 0f);
                var visual = new GameObject("Foam");
                visual.transform.SetParent(holder.transform, false);
                visual.AddComponent<MeshFilter>().sharedMesh = arc;
                visual.AddComponent<MeshRenderer>().sharedMaterial = foam;
                BoatWreckPiece piece = holder.AddComponent<BoatWreckPiece>();
                Vector3 direction = Quaternion.Euler(0f, i * 30f, 0f) * Vector3.forward;
                Configure(piece, 0.015f, 0.65f, direction * 0.10f, Vector3.zero,
                    0.9f + i % 3 * 0.08f, 1.3f, 0.065f, 0f, true, visual);
                visual.SetActive(false);
                pieces.Add(piece);
            }

            var serialized = new SerializedObject(root.GetComponent<BoatWreckEffect>());
            serialized.FindProperty("_frontHalf").objectReferenceValue = null;
            serialized.FindProperty("_backHalf").objectReferenceValue = null;
            SerializedProperty array = serialized.FindProperty("_pieces");
            array.arraySize = pieces.Count;
            for (int i = 0; i < pieces.Count; i++) { array.GetArrayElementAtIndex(i).objectReferenceValue = pieces[i]; }
            Mesh[] damageMeshes;
            Mesh[] damagedBows;
            CreateDamageModels(vertices, triangles, front, bowPivot, matrix, out damageMeshes, out damagedBows);
            serialized.FindProperty("_bowMesh").objectReferenceValue = root.transform.Find("Bow").GetComponent<MeshFilter>();
            SerializedProperty bowArray = serialized.FindProperty("_damagedBowMeshes");
            bowArray.arraySize = damagedBows.Length;
            for (int i = 0; i < damagedBows.Length; i++) { bowArray.GetArrayElementAtIndex(i).objectReferenceValue = damagedBows[i]; }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, _prefabPath);
            GameObject boatRoot = PrefabUtility.LoadPrefabContents(boatPath);
            try
            {
                BoatHullDamage damage = boatRoot.GetComponent<BoatHullDamage>();
                if (!damage) { damage = boatRoot.AddComponent<BoatHullDamage>(); }
                var damageData = new SerializedObject(damage);
                damageData.FindProperty("_model").objectReferenceValue = boatRoot.transform.Find("Model").GetComponent<MeshFilter>();
                damageData.FindProperty("_renderer").objectReferenceValue = boatRoot.transform.Find("Model").GetComponent<MeshRenderer>();
                SerializedProperty models = damageData.FindProperty("_damageMeshes");
                models.arraySize = damageMeshes.Length;
                for (int i = 0; i < damageMeshes.Length; i++) { models.GetArrayElementAtIndex(i).objectReferenceValue = damageMeshes[i]; }
                damageData.ApplyModifiedPropertiesWithoutUndo();
                var controller = new SerializedObject(boatRoot.GetComponent<BoatController>());
                controller.FindProperty("_wreckEffectPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath).GetComponent<BoatWreckEffect>();
                controller.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(boatRoot, boatPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(boatRoot); }
            AssetDatabase.SaveAssets();
            return boatName + ": saved dented nose stages and simultaneous wreck.";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CreateDamageModels(Vertex[] source, int[] triangles, Region front,
        Vector3 bowPivot, Matrix4x4 modelToRoot, out Mesh[] models, out Mesh[] bows)
    {
        models = new Mesh[3];
        bows = new Mesh[3];
        for (int stage = 0; stage < 3; stage++)
        {
            float amount = (stage + 1) / 3f;
            Vertex[] deformed = source.Select(v => DeformVertex(v, amount)).ToArray();
            var whole = new Region { Name = "HullDamage_" + (stage + 1) };
            // Retain every exterior triangle; damage compresses the skin rather than opening the hollow hull.
            Mesh damage = Fracture(deformed, triangles, whole);
            Vector3[] points = damage.vertices;
            Vector3[] normals = damage.normals;
            Matrix4x4 rootToModel = modelToRoot.inverse;
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = rootToModel.MultiplyPoint3x4(points[i]);
                normals[i] = rootToModel.MultiplyVector(normals[i]).normalized;
            }
            damage.vertices = points;
            damage.normals = normals;
            damage.subMeshCount = 1;
            damage.RecalculateTangents();
            damage.RecalculateBounds();
            models[stage] = SaveMesh(damage, _folder + "/HullDamage" + (stage + 1) + ".asset");

            var bow = new Region { Name = "BowDamage_" + (stage + 1) };
            bow.Cuts.AddRange(front.Cuts);
            Mesh damagedBow = Fracture(source, triangles, bow);
            Vector3[] bowPoints = damagedBow.vertices;
            Vector3[] bowNormals = damagedBow.normals;
            for (int i = 0; i < bowPoints.Length; i++)
            {
                Vertex deformedVertex = DeformVertex(new Vertex { Position = bowPoints[i], Normal = bowNormals[i] }, amount);
                bowPoints[i] = deformedVertex.Position - bowPivot;
                bowNormals[i] = deformedVertex.Normal;
            }
            damagedBow.vertices = bowPoints;
            damagedBow.normals = bowNormals;
            damagedBow.RecalculateTangents();
            damagedBow.RecalculateBounds();
            bows[stage] = SaveMesh(damagedBow, _folder + "/BowDamage" + (stage + 1) + ".asset");
        }
    }

    private static Vector3 DentNose(Vector3 point, float amount)
    {
        float weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.18f, 0.46f, point.z)) * amount;
        return new Vector3(point.x * (1f - 0.14f * weight),
            point.y - 0.04f * weight + Mathf.Sin(point.z * 110f + point.x * 60f) * 0.012f * weight,
            point.z - 0.11f * weight + Mathf.Sin(point.x * 90f) * 0.012f * weight);
    }

    private static Vertex DeformVertex(Vertex vertex, float amount)
    {
        if (vertex.Position.z <= 0.18f)
        {
            return vertex;
        }

        Vector3 axis = Mathf.Abs(vertex.Normal.y) < 0.9f ? Vector3.up : Vector3.right;
        Vector3 tangent = Vector3.Cross(vertex.Normal, axis).normalized;
        Vector3 bitangent = Vector3.Cross(vertex.Normal, tangent).normalized;
        const float Sample = 0.0001f;
        Vector3 deformedTangent = DentNose(vertex.Position + tangent * Sample, amount)
            - DentNose(vertex.Position - tangent * Sample, amount);
        Vector3 deformedBitangent = DentNose(vertex.Position + bitangent * Sample, amount)
            - DentNose(vertex.Position - bitangent * Sample, amount);
        vertex.Position = DentNose(vertex.Position, amount);
        vertex.Normal = Vector3.Cross(deformedTangent.normalized, deformedBitangent.normalized).normalized;
        return vertex;
    }

    private static void Configure(BoatWreckPiece piece, float release, float duration, Vector3 offset,
        Vector3 rotation, float sink, float sinkDuration, float depth, float lift, bool hidden, GameObject visual)
    {
        var data = new SerializedObject(piece);
        data.FindProperty("_releaseTime").floatValue = release;
        data.FindProperty("_releaseDuration").floatValue = duration;
        data.FindProperty("_impulseRelease").boolValue = true;
        data.FindProperty("_displacement").vector3Value = offset;
        data.FindProperty("_rotation").vector3Value = rotation;
        data.FindProperty("_sinkTime").floatValue = sink;
        data.FindProperty("_sinkDuration").floatValue = sinkDuration;
        data.FindProperty("_sinkDepth").floatValue = depth;
        data.FindProperty("_lift").floatValue = lift;
        data.FindProperty("_phase").floatValue = release * 9f;
        data.FindProperty("_hideUntilRelease").boolValue = hidden;
        data.FindProperty("_visibleObject").objectReferenceValue = visual;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Mesh Fracture(Vertex[] source, int[] indices, Region region)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var exterior = new List<int>();
        var interior = new List<int>();
        var edges = region.Cuts.Select(c => new List<Vector3>()).ToArray();
        for (int i = 0; i < indices.Length; i += 3)
        {
            var polygon = new List<Vertex> { source[indices[i]], source[indices[i + 1]], source[indices[i + 2]] };
            for (int cut = 0; cut < region.Cuts.Count && polygon.Count >= 3; cut++)
            {
                polygon = Clip(polygon, region.Cuts[cut], edges[cut]);
            }
            for (int corner = 1; corner + 1 < polygon.Count; corner++)
            {
                AddTriangle(polygon[0], polygon[corner], polygon[corner + 1], positions, normals, uvs, exterior);
            }
        }

        foreach (List<Vector3> boundary in edges)
        {
            // Later cuts also trim cap segments; this prevents caps spanning a removed section.
            var kept = new List<Vector3>();
            for (int i = 0; i + 1 < boundary.Count; i += 2)
            {
                Vector3 a = boundary[i];
                Vector3 b = boundary[i + 1];
                bool valid = true;
                foreach (Func<Vector3, float> cut in region.Cuts)
                {
                    float da = cut(a);
                    float db = cut(b);
                    if (da < -0.0001f && db < -0.0001f) { valid = false; break; }
                    if (da < -0.0001f) { a = Vector3.Lerp(a, b, da / (da - db)); }
                    else if (db < -0.0001f) { b = Vector3.Lerp(a, b, da / (da - db)); }
                }
                if (valid) { kept.Add(a); kept.Add(b); }
            }
            if (kept.Count == 0) { continue; }
            Vector3 center = Vector3.zero;
            foreach (Vector3 p in kept) { center += p; }
            center /= kept.Count;
            for (int i = 0; i + 1 < kept.Count; i += 2)
            {
                Vector3 a = kept[i];
                Vector3 b = kept[i + 1];
                // A thin inward rim gives torn boards thickness without giant cap fans crossing the cannon or rail gaps.
                Vector3 innerA = a + (center - a).normalized * Mathf.Min(0.018f, Vector3.Distance(center, a) * 0.2f);
                Vector3 innerB = b + (center - b).normalized * Mathf.Min(0.018f, Vector3.Distance(center, b) * 0.2f);
                Vector3 normal = Vector3.Cross(b - a, innerA - a).normalized;
                Vertex va = new Vertex { Position = a, Normal = normal, Uv = new Vector2(a.x, a.y) };
                Vertex vb = new Vertex { Position = b, Normal = normal, Uv = new Vector2(b.x, b.y) };
                Vertex vc = new Vertex { Position = innerA, Normal = normal, Uv = new Vector2(innerA.x, innerA.y) };
                Vertex vd = new Vertex { Position = innerB, Normal = normal, Uv = new Vector2(innerB.x, innerB.y) };
                AddTriangle(va, vb, vc, positions, normals, uvs, interior);
                AddTriangle(vb, vd, vc, positions, normals, uvs, interior);
            }
        }

        var result = new Mesh { name = region.Name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        result.SetVertices(positions);
        result.SetNormals(normals);
        result.SetUVs(0, uvs);
        result.subMeshCount = 2;
        result.SetTriangles(exterior, 0);
        result.SetTriangles(interior, 1);
        result.RecalculateBounds();
        return result;
    }

    private static List<Vertex> Clip(List<Vertex> polygon, Func<Vector3, float> cut, List<Vector3> edges)
    {
        var result = new List<Vertex>();
        var intersections = new List<Vector3>();
        for (int i = 0; i < polygon.Count; i++)
        {
            Vertex a = polygon[i];
            Vertex b = polygon[(i + 1) % polygon.Count];
            float da = cut(a.Position);
            float db = cut(b.Position);
            if (da >= 0f) { result.Add(a); }
            if ((da >= 0f) != (db >= 0f))
            {
                Vertex point = Vertex.Lerp(a, b, da / (da - db));
                result.Add(point);
                intersections.Add(point.Position);
            }
        }
        if (intersections.Count == 2) { edges.AddRange(intersections); }
        return result;
    }

    private static void AddTriangle(Vertex a, Vertex b, Vertex c, List<Vector3> positions,
        List<Vector3> normals, List<Vector2> uvs, List<int> indices)
    {
        foreach (Vertex vertex in new[] { a, b, c })
        {
            indices.Add(positions.Count);
            positions.Add(vertex.Position);
            normals.Add(vertex.Normal);
            uvs.Add(vertex.Uv);
        }
    }

    private static Mesh SaveMesh(Mesh mesh, string path)
    {
        mesh.name = System.IO.Path.GetFileNameWithoutExtension(path);
        Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing)
        {
            // Explicit mesh setters refresh graphics buffers as well as serialized CPU data.
            existing.Clear();
            existing.indexFormat = mesh.indexFormat;
            existing.name = mesh.name;
            existing.vertices = mesh.vertices;
            existing.normals = mesh.normals;
            existing.uv = mesh.uv;
            existing.tangents = mesh.tangents;
            existing.subMeshCount = mesh.subMeshCount;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                existing.SetTriangles(mesh.GetTriangles(i), i);
            }
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            return existing;
        }
        else { AssetDatabase.CreateAsset(mesh, path); }
        return mesh;
    }

    private static Mesh CreateShard()
    {
        var mesh = new Mesh { name = "SplinteredTimber" };
        mesh.vertices = new[] { new Vector3(-1,-1,-1), new Vector3(1,-1,-1), new Vector3(1,1,-1),
            new Vector3(-1,1,-1), new Vector3(-0.4f,-0.5f,0.65f), new Vector3(0.45f,-0.4f,1.15f),
            new Vector3(0.3f,0.4f,0.8f), new Vector3(-0.55f,0.35f,1.4f) };
        mesh.triangles = new[] {0,2,1,0,3,2,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7,4,5,6,4,6,7};
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateFoamArc()
    {
        var vertices = new List<Vector3>();
        var indices = new List<int>();
        for (int i = 0; i <= 8; i++)
        {
            float angle = (-12f + i * 3f) * Mathf.Deg2Rad;
            Vector3 direction = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            float radius = 0.32f + Mathf.Sin(i * 2.1f) * 0.008f;
            vertices.Add(direction * radius);
            vertices.Add(direction * (radius + 0.008f + Mathf.Sin(i * 1.4f) * 0.004f));
            if (i == 0) { continue; }
            int index = i * 2;
            indices.AddRange(new[] { index - 2, index, index - 1, index - 1, index, index + 1 });
        }
        var result = new Mesh { name = "BrokenFoamArc" };
        result.SetVertices(vertices);
        result.SetTriangles(indices, 0);
        result.RecalculateNormals();
        result.RecalculateBounds();
        return result;
    }

    private static GameObject AddSplinters(Transform parent, Vector3 pivot, bool front, Material wood)
    {
        string path = _folder + "/TimberShard.asset";
        Mesh shard = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!shard) { shard = SaveMesh(CreateShard(), path); }
        var details = new GameObject("ExposedTimber");
        details.transform.SetParent(parent, false);
        for (int i = 0; i < 11; i++)
        {
            float x = -0.27f + i * 0.054f;
            float y = 0.125f + (i % 3) * 0.026f;
            float seam = 0.037f * Mathf.Sin(x * 53f + y * 11f) + 0.018f * Mathf.Sin(y * 79f - x * 17f);
            var splinter = new GameObject("TornTimber_" + i);
            splinter.transform.SetParent(details.transform, false);
            splinter.transform.localPosition = new Vector3(x, y, (front ? 0.11f : -0.10f) + seam) - pivot;
            splinter.transform.localRotation = Quaternion.Euler((i % 3 - 1) * 12f, front ? 180f : 0f, (i % 4 - 1) * 8f);
            splinter.transform.localScale = new Vector3(0.012f + i % 3 * 0.004f, 0.008f, 0.035f + i % 4 * 0.013f);
            splinter.AddComponent<MeshFilter>().sharedMesh = shard;
            splinter.AddComponent<MeshRenderer>().sharedMaterial = wood;
        }
        details.SetActive(false);
        return details;
    }
}
