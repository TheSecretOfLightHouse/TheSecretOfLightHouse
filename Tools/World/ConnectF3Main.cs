using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Lighthouse.World.Islands.View;
using Lighthouse.World.MapGeneration.Net;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Ocean.Sandbox;
using Lighthouse.World.Ocean.View;

public static class ConnectF3Main
{
    private const string MainPath = "Assets/00.Scenes/Main/MainGame.unity";
    private const string TestPath = "Assets/00.Scenes/Minseo/WaterF3Terrain/WaterF3Terrain.unity";
    private const string SurfPath = "Assets/02.Prefabs/World/F3MainSurf.prefab";

    public static object Main()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Stopped, compiled Editor required.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(MainPath);
        if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(MainPath, OpenSceneMode.Additive);
        var components = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MonoBehaviour>(true)).Where(c=>c!=null).ToArray();
        var composite = components.OfType<CompositeLocalMapBuilder>().Single();
        var islands = components.OfType<IslandInstanceSet>().Single();
        var waves = components.OfType<WaterSamplerHost>().Single();
        var data = AssetDatabase.FindAssets("t:F3IslandData",new[]{"Assets/06.Textures/World/F3Main"})
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<F3IslandData>).ToArray();
        if (data.Length != 6 || data.Any(d=>!d.IsValid)) throw new InvalidOperationException("All six main-island bakes are required.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SurfPath);
        if (prefab == null)
        {
            var test = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(TestPath);
            bool opened = !test.isLoaded;
            if (opened) test=EditorSceneManager.OpenScene(TestPath,OpenSceneMode.Additive);
            var source=test.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<WaterF3Surf>(true)).Single();
            var clone=UnityEngine.Object.Instantiate(source.gameObject);
            clone.name="F3MainSurf";
            clone.SetActive(false);
            var fields=new SerializedObject(clone.GetComponent<WaterF3Surf>());
            fields.FindProperty("_host").objectReferenceValue=null;
            fields.FindProperty("_terrain").objectReferenceValue=null;
            fields.FindProperty("_bands").intValue=4;
            fields.FindProperty("_acrossCount").intValue=289;
            fields.ApplyModifiedPropertiesWithoutUndo();
            foreach(var renderer in clone.GetComponentsInChildren<Renderer>(true))
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    if(materials[i]==null)continue;
                    string path="Assets/07.Materials/World/F3Main_"+materials[i].name+".mat";
                    var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(material==null){material=new Material(materials[i]);AssetDatabase.CreateAsset(material,path);}
                    materials[i]=material;
                }
                renderer.sharedMaterials=materials;
            }
            if(!AssetDatabase.IsValidFolder("Assets/02.Prefabs/World"))AssetDatabase.CreateFolder("Assets/02.Prefabs","World");
            prefab=PrefabUtility.SaveAsPrefabAsset(clone,SurfPath);
            UnityEngine.Object.DestroyImmediate(clone);
            if(opened)EditorSceneManager.CloseScene(test,true);
        }
        var map=composite.GetComponent<F3MapWater>();
        if(map==null)map=composite.gameObject.AddComponent<F3MapWater>();
        var mapFields=new SerializedObject(map);
        mapFields.FindProperty("_islands").objectReferenceValue=islands;
        mapFields.FindProperty("_waves").objectReferenceValue=waves;
        mapFields.FindProperty("_surfPrefab").objectReferenceValue=prefab.GetComponent<WaterF3Surf>();
        var list=mapFields.FindProperty("_data");list.arraySize=data.Length;
        for(int i=0;i<data.Length;i++)list.GetArrayElementAtIndex(i).objectReferenceValue=data[i];
        mapFields.ApplyModifiedPropertiesWithoutUndo();
        var sources=new SerializedObject(composite);
        list=sources.FindProperty("_sources");
        bool contains=false;
        for(int i=0;i<list.arraySize;i++)contains|=list.GetArrayElementAtIndex(i).objectReferenceValue==map;
        if(!contains){int index=list.arraySize;list.arraySize++;list.GetArrayElementAtIndex(index).objectReferenceValue=map;}
        sources.ApplyModifiedPropertiesWithoutUndo();
        var bridge=components.OfType<OceanShaderBridge>().Single();
        if(!bridge.TryGetComponent<OpenSeaSwellBinder>(out _))bridge.gameObject.AddComponent<OpenSeaSwellBinder>();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return new {scene=scene.path,tiles=data.Length,surf=SurfPath,builders=list.arraySize,dirty=scene.isDirty};
    }
}
