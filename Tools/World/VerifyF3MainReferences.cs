using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Lighthouse.World.Islands.View;
using Lighthouse.World.MapGeneration.Net;
using Lighthouse.World.MapGeneration.Rules;
using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Ocean.Sandbox;
using Lighthouse.World.Ocean.View;

public static class VerifyF3MainReferences
{
    public static object Main()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Stopped, compiled Editor required.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/00.Scenes/Main/MainGame.unity");
        if(!scene.isLoaded)throw new InvalidOperationException("MainGame must be loaded.");
        var components=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).Where(c=>c!=null).ToArray();
        var map=components.OfType<F3MapWater>().Single();
        var composite=components.OfType<CompositeLocalMapBuilder>().Single();
        var sources=new SerializedObject(composite).FindProperty("_sources");
        var builders=Enumerable.Range(0,sources.arraySize).Select(i=>sources.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
        if(builders.Last()!=map || builders.Count(x=>x==map)!=1)throw new InvalidOperationException("F3 must be the final local builder exactly once.");
        var fields=new SerializedObject(map);
        foreach(string property in new[]{"_islands","_waves","_surfPrefab"})
        {
            if(fields.FindProperty(property).objectReferenceValue==null)throw new InvalidOperationException("Missing F3 reference: "+property);
        }
        var data=AssetDatabase.FindAssets("t:F3IslandData",new[]{"Assets/06.Textures/World/F3Main"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<F3IslandData>).ToArray();
        var mapState=components.OfType<MapStateNet>().Single();
        var variants=mapState.Config.Archetypes.Concat(new[]{mapState.Config.LighthouseIsland}).Where(a=>a!=null).SelectMany(a=>a.Variants).Where(v=>v!=null).Select(v=>v.Prefab).Distinct().ToArray();
        foreach(var prefab in variants)
        {
            if(!data.Any(d=>d.Prefab==prefab && d.IsValid))throw new InvalidOperationException("No F3 tile for configured prefab: "+prefab.name);
        }
        var tiles=data.Select(d=>
        {
            if(!d.IsValid || !F3DirectionalTransmission.TryReadHeader(d.Directions.GetData<byte>(),out int size,out int count,out float angle) || size!=d.Geometry.width)
                throw new InvalidOperationException("Invalid F3 baked references: "+d.name);
            EditorUtility.SetDirty(d);
            return new {prefab=d.Prefab.name,size,count,cell=d.CellSize,geometry=AssetDatabase.GetAssetPath(d.Geometry)};
        }).ToArray();
        var surf=(WaterF3Surf)fields.FindProperty("_surfPrefab").objectReferenceValue;
        var surfFields=new SerializedObject(surf);
        foreach(string property in new[]{"_filter","_spray"})
        {
            if(surfFields.FindProperty(property).objectReferenceValue==null)throw new InvalidOperationException("Missing surf reference: "+property);
        }
        if(surf.gameObject.activeSelf)throw new InvalidOperationException("Surf template must be initialized while inactive.");
        var materialPaths=surf.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Select(AssetDatabase.GetAssetPath).Distinct().ToArray();
        var oceanMaterials=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).SelectMany(r=>r.sharedMaterials)
            .Where(m=>m!=null && m.shader.name=="Lighthouse/World/ClearOcean").Distinct().ToArray();
        if(oceanMaterials.Length==0)throw new InvalidOperationException("Main ocean material is missing.");
        var materials=oceanMaterials.Concat(surf.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials)).Where(m=>m!=null).Distinct().ToArray();
        foreach(var material in materials)ShaderUtil.CompilePass(material,0,true);
        var shaders=materials.Select(m=>m.shader).Distinct().ToArray();
        var shaderReports=shaders.Select(s=>new {shader=s.name,error=ShaderUtil.ShaderHasError(s),messages=ShaderUtil.GetShaderMessages(s).Select(m=>new {m.message,m.line,m.severity}).ToArray()}).ToArray();
        if(shaderReports.Any(s=>s.error))throw new InvalidOperationException(Newtonsoft.Json.JsonConvert.SerializeObject(shaderReports));
        int missing=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
        if(missing!=0)throw new InvalidOperationException("Main scene has missing scripts: "+missing);
        string[] generated=AssetDatabase.GetDependencies("Assets/02.Prefabs/World/F3MainSurf.prefab",true);
        var sceneDependencies=generated.Where(p=>p.EndsWith(".unity")).ToArray();
        if(sceneDependencies.Length!=0)throw new InvalidOperationException("Surf prefab depends on a scene.");
        AssetDatabase.SaveAssets();
        return Newtonsoft.Json.JsonConvert.SerializeObject(new {scene=scene.path,dirty=scene.isDirty,play=EditorApplication.isPlaying,
            builders=builders.Select(b=>b.GetType().Name).ToArray(),configuredPrefabs=variants.Length,tiles,
            materialPaths,shaders=shaderReports,missingScripts=missing,
            forbiddenSandboxArt=generated.Where(p=>p.StartsWith("Assets/00.Scenes/")).ToArray(),
            swellBinders=components.OfType<OpenSeaSwellBinder>().Count()});
    }
}
