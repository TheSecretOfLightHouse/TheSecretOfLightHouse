using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Lighthouse.World.Ocean.Rules;

public static class BakeF3MainIsland
{
    private const float Cell = 0.125f;
    private const float BeachReach = 0.625f;
    private const float PathDecay = 18f;
    private const int SmoothPasses = 24;
    private const int DirectionCount = 8;
    private const int Size = 384;
    private const float UnknownDepth = 4f;
    private static readonly Vector2 Origin = new Vector2(-24f, -24f);
    private static readonly int[] Dx = { 1, -1, 0, 0 };
    private static readonly int[] Dz = { 0, 0, 1, -1 };
    private static int _size = Size;
    private const string Folder = "Assets/06.Textures/World/F3Main";

    public static object Main(string prefabPath)
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Stopped Editor required.");
        EnsureFolder(Folder);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) throw new InvalidOperationException(prefabPath);
        string name = prefab.name;
        string output = Folder + "/" + name;
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        root.transform.position=Vector3.zero; root.transform.rotation=Quaternion.identity;
        Color[] geometry;
        Color32[] coverage;
        try
        {
            var terrains = root.GetComponentsInChildren<Terrain>(true).Where(t=>t.gameObject.activeInHierarchy).ToList();
            if (terrains.Count == 0) throw new InvalidOperationException("Terrain required.");
            var solids = root.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled && !c.isTrigger && !(c is TerrainCollider) && c.gameObject.activeInHierarchy && c.bounds.min.y <= 0f).ToList();
            Bounds[] solidBounds = solids.Select(c=>c.bounds).ToArray();
            foreach(var terrain in terrains)
            {
                var p=terrain.transform.position;
                var s=terrain.terrainData.size;
                if(p.x<Origin.x+2 || p.z<Origin.y+2 || p.x+s.x>Origin.x+Size*Cell-2 || p.z+s.z>Origin.y+Size*Cell-2)
                    throw new InvalidOperationException("Terrain exceeds tile padding: " + prefabPath);
            }
            geometry = new Color[Size * Size];
            coverage = new Color32[geometry.Length];
            float reach = 0.25f;
            int holes=0,groundHits=0,solidHits=0;
        for (int id = 0; id < geometry.Length; id++)
        {
            Vector2 point = Point(id);
            float height = float.NegativeInfinity;
            foreach (var terrain in terrains)
            {
                Vector3 local = terrain.transform.InverseTransformPoint(new Vector3(point.x, 0, point.y));
                TerrainData terrainData = terrain.terrainData;
                float u = local.x / terrainData.size.x, v = local.z / terrainData.size.z;
                if (u < 0 || v < 0 || u >= 1 || v >= 1)
                {
                    continue;
                }
                if (terrainData.IsHole(Mathf.Min((int)(u * terrainData.holesResolution), terrainData.holesResolution - 1), Mathf.Min((int)(v * terrainData.holesResolution), terrainData.holesResolution - 1)))
                {
                    holes++;
                    continue;
                }
                float actual = terrain.transform.position.y + terrainData.GetInterpolatedHeight(u, v);
                height = Mathf.Max(height, actual);
            }
            bool hasGround = !float.IsNegativeInfinity(height);
            if (hasGround)
            {
                groundHits++;
            }
            var ray = new Ray(new Vector3(point.x, 30, point.y), Vector3.down);
            for (int solidIndex = 0; solidIndex < solids.Count; solidIndex++)
            {
                Collider solid = solids[solidIndex];
                Bounds bounds = solidBounds[solidIndex];
                if (bounds.Contains(new Vector3(point.x, bounds.center.y, point.y)) && solid.Raycast(ray, out RaycastHit hit, 60f))
                {
                    height = Mathf.Max(height, hit.point.y);
                    solidHits++;
                }
            }
            bool hasBottom = !float.IsNegativeInfinity(height);
            float depth = hasBottom ? -height : UnknownDepth;
            // A is a newly derived vertical reach bound, never an old lifted mesh contour.
            geometry[id] = new Color(depth, 0, 0, hasBottom ? reach - height : reach + UnknownDepth);
            coverage[id] = new Color32(hasBottom ? (byte)255 : (byte)0, hasGround ? (byte)255 : (byte)0, 0, 255);

        }

            var region = new int[geometry.Length];
            var queue = new Queue<int>();
        int regions = 0;
        for (int id = 0; id < geometry.Length; id++)
        {
            if (geometry[id].r <= 0 || region[id] != 0)
            {
                continue;
            }
            regions++;
            region[id] = regions;
            queue.Enqueue(id);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                for (int n = 0; n < 4; n++)
                {
                    int next = Neighbor(current, n);
                    if (next >= 0 && region[next] == 0 && geometry[next].r > 0)
                    {
                        region[next] = regions;
                        queue.Enqueue(next);
                    }
                }
            }
        }
        var external = new bool[regions + 1];
        for (int id = 0; id < geometry.Length; id++)
        {
            if (id % Size == 0 || id % Size == Size - 1 || id / Size == 0 || id / Size == Size - 1)
            {
                external[region[id]] = true;
            }
        }
        external[0] = false;

            for (int id=0; id<geometry.Length; id++)
            {
                geometry[id].b=region[id];
                geometry[id].g=external[region[id]] ? 1f : 0f;
            }
            var distance = new int[geometry.Length];
            Array.Fill(distance, int.MaxValue);
            for(int id=0;id<geometry.Length;id++)
            {
                if(geometry[id].g>0f) {distance[id]=0;queue.Enqueue(id);}
            }
            while(queue.Count>0)
            {
                int id=queue.Dequeue();
                for(int n=0;n<4;n++)
                {
                    int next=Neighbor(id,n);
                    if(next<0 || region[next]!=0 || geometry[next].a<=0 || distance[id]*Cell>=BeachReach || distance[next]<=distance[id]+1) continue;
                    distance[next]=distance[id]+1;
                    geometry[next].g=1f;
                    geometry[next].b=geometry[id].b;
                    queue.Enqueue(next);
                }
            }

        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var geometryTexture = SaveTexture(output + "_Geometry.asset",geometry);
        var coverageTexture=new Texture2D(Size,Size,TextureFormat.RGBA32,false,true) {name=name+"_Coverage",filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        coverageTexture.SetPixels32(coverage);coverageTexture.Apply(false,false);
        CreateOrReplace(coverageTexture,output+"_Coverage.asset");
        coverageTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(output+"_Coverage.asset");
        string bytesPath=output+"_Directions.bytes";
        using(var writer=new BinaryWriter(File.Create(bytesPath)))
        {
            writer.Write(F3DirectionalTransmission.Magic);writer.Write(Size);writer.Write(DirectionCount);writer.Write(0f);
            for(int direction=0;direction<DirectionCount;direction++)
            {
                Color[] field=Bake(geometry,F3DirectionalTransmission.DirectionOf(direction*360f/DirectionCount),out int missing,out int mismatch);
                if(missing!=0 || mismatch!=0) throw new InvalidOperationException("Directional references do not cover geometry.");
                var encoded=new Texture2D(Size,Size,TextureFormat.RGBAHalf,false,true);
                encoded.SetPixels(field);encoded.Apply(false,false);
                writer.Write(encoded.GetRawTextureData<byte>().ToArray());
                UnityEngine.Object.DestroyImmediate(encoded);
            }
        }
        AssetDatabase.ImportAsset(bytesPath,ImportAssetOptions.ForceSynchronousImport);
        var data=AssetDatabase.LoadAssetAtPath<F3IslandData>(output+".asset");
        if(data==null) {data=ScriptableObject.CreateInstance<F3IslandData>();AssetDatabase.CreateAsset(data,output+".asset");}
        data.Initialize(prefab,geometryTexture,coverageTexture,AssetDatabase.LoadAssetAtPath<TextAsset>(bytesPath),Origin,Cell);
        EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
        return new {path=output+".asset",valid=data.IsValid,size=Size,directions=DirectionCount,bytes=new FileInfo(bytesPath).Length};
    }
    private static void EnsureFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        string parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
    }
    private static Texture2D SaveTexture(string path,Color[] pixels)
    {
        var texture=new Texture2D(Size,Size,TextureFormat.RGBAFloat,false,true) {name=Path.GetFileNameWithoutExtension(path),filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
        texture.SetPixels(pixels);texture.Apply(false,false);CreateOrReplace(texture,path);return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    private static void CreateOrReplace(Texture2D texture,string path)
    {
        var existing=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(existing==null)AssetDatabase.CreateAsset(texture,path);
        else {EditorUtility.CopySerialized(texture,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(texture);}
    }
    private static Color[] Bake(Color[] geometry, Vector2 incoming, out int missing, out int sourceMismatch)
    {
        int count = geometry.Length;
        var field = new Color[count];
        var source = new int[count];
        var distance = new int[count];
        var pathEnergy = new float[count];
        var isClear = new bool[count];
        var rayEnergy = new float[count];
        var queue = new Queue<int>();
        Array.Fill(source, -1);
        Array.Fill(distance, int.MaxValue);
        Parallel.For(0, count, id =>
        {
            if (geometry[id].g <= 0 || geometry[id].r <= 0)
            {
                return;
            }
            Vector2 cursor = new Vector2(id % _size + 0.5f, id / _size + 0.5f);
            bool clear = true;
            float energy = 1f;
            for (int step = 0; step < _size * 3; step++)
            {
                cursor -= incoming * 0.5f;
                int x = Mathf.FloorToInt(cursor.x), z = Mathf.FloorToInt(cursor.y);
                if (x < 0 || z < 0 || x >= _size || z >= _size)
                {
                    break;
                }
                if (geometry[z * _size + x].r <= 0)
                {
                    clear = false;
                    break;
                }
                energy = Mathf.Min(energy, Limit(geometry[z * _size + x].r));
            }
            isClear[id] = clear;
            rayEnergy[id] = energy;
        });
        for (int id = 0; id < count; id++)
        {
            if (!isClear[id])
            {
                continue;
            }
            source[id] = id;
            distance[id] = 0;
            pathEnergy[id] = Mathf.Min(rayEnergy[id], Limit(geometry[id].r));
            queue.Enqueue(id);
        }
        while (queue.Count > 0)
        {
            int id = queue.Dequeue();
            for (int n = 0; n < 4; n++)
            {
                int next = Neighbor(id, n);
                if (next < 0 || geometry[next].g <= 0 || geometry[next].r <= 0 || distance[next] <= distance[id] + 1)
                {
                    continue;
                }
                source[next] = source[id];
                distance[next] = distance[id] + 1;
                pathEnergy[next] = Mathf.Min(pathEnergy[id], Limit(geometry[next].r));
                queue.Enqueue(next);
            }
        }
        var beachDistance = new int[count];
        Array.Fill(beachDistance, int.MaxValue);
        for (int id = 0; id < count; id++)
        {
            if (geometry[id].g > 0 && geometry[id].r > 0)
            {
                beachDistance[id] = 0;
                queue.Enqueue(id);
            }
        }
        while (queue.Count > 0)
        {
            int id = queue.Dequeue();
            for (int n = 0; n < 4; n++)
            {
                int next = Neighbor(id, n);
                if (next < 0 || geometry[next].r > 0 || geometry[next].a <= 0 || beachDistance[id] * Cell >= BeachReach || beachDistance[next] <= beachDistance[id] + 1)
                {
                    continue;
                }
                beachDistance[next] = beachDistance[id] + 1;
                source[next] = source[id];
                distance[next] = distance[id] + 1;
                pathEnergy[next] = pathEnergy[id];
                queue.Enqueue(next);
            }
        }
        missing = 0;
        sourceMismatch = 0;
        for (int id = 0; id < count; id++)
        {
            bool expected = geometry[id].g > 0;
            if (expected && source[id] < 0)
            {
                missing++;
            }
            if (!expected && source[id] >= 0)
            {
                sourceMismatch++;
            }
            if (!expected || source[id] < 0)
            {
                continue;
            }
            Vector2 offset = Point(source[id]) - Point(id);
            float path = distance[id] * Cell;
            field[id] = new Color(pathEnergy[id] * Mathf.Exp(-path / PathDecay), offset.x, offset.y, path / 2f);
        }
        var scratch = new Color[count];
        for (int pass = 0; pass < SmoothPasses; pass++)
        {
            for (int id = 0; id < count; id++)
            {
                if (geometry[id].g <= 0)
                {
                    scratch[id] = field[id];
                    continue;
                }
                Color sum = field[id] * 4;
                float weight = 4;
                for (int n = 0; n < 4; n++)
                {
                    int next = Neighbor(id, n);
                    if (next >= 0 && geometry[next].g > 0 && geometry[next].b == geometry[id].b)
                    {
                        sum += field[next];
                        weight++;
                    }
                }
                scratch[id] = sum / weight;
            }
            var swap = field;
            field = scratch;
            scratch = swap;
        }
        return field;
    }

    private static float Limit(float depth)
    {
        return Mathf.Min(1f, Mathf.Max(0.25f, depth + 0.05f) * 1.25f);
    }

    private static Vector2 Point(int id)
    {
        return Origin + new Vector2(id % _size + 0.5f, id / _size + 0.5f) * Cell;
    }

    private static int Neighbor(int id, int n)
    {
        int x = id % _size + Dx[n], z = id / _size + Dz[n];
        return x >= 0 && z >= 0 && x < _size && z < _size ? z * _size + x : -1;
    }
}
