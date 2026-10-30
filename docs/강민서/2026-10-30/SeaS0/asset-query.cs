var config=UnityEditor.AssetDatabase.LoadAssetAtPath<Lighthouse.World.MapGeneration.Rules.MapGenerationConfig>("Assets/02.Prefabs/World/Data/MapGeneration/MapGenerationConfig.asset");
var paths=new System.Collections.Generic.HashSet<string>();
paths.Add(UnityEditor.AssetDatabase.GetAssetPath(config));
foreach(var guid in UnityEditor.AssetDatabase.FindAssets("t:ScriptableObject",new[]{"Assets/02.Prefabs/World/Data"}))paths.Add(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
var items=new System.Collections.Generic.List<object>();
foreach(var path in paths.OrderBy(x=>x)){var asset=UnityEditor.AssetDatabase.LoadMainAssetAtPath(path);if(asset==null)continue;items.Add(new{path,guid=UnityEditor.AssetDatabase.AssetPathToGUID(path),type=asset.GetType().FullName,json=UnityEditor.EditorJsonUtility.ToJson(asset)});}
var prefabs=new System.Collections.Generic.List<object>();
foreach(var a in new[]{config.LighthouseIsland}.Concat(config.Archetypes)){foreach(var v in a.Variants){prefabs.Add(new{archetype=a.DisplayName,id=a.Id,archetypePath=UnityEditor.AssetDatabase.GetAssetPath(a),prefab=UnityEditor.AssetDatabase.GetAssetPath(v.Prefab),guid=UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(v.Prefab)),weight=v.Weight,scale=new[]{v.Prefab.transform.localScale.x,v.Prefab.transform.localScale.y,v.Prefab.transform.localScale.z}});}}
return Newtonsoft.Json.JsonConvert.SerializeObject(new{assets=items,prefabs});
