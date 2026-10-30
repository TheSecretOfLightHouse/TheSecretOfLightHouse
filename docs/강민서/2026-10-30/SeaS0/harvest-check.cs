var config=UnityEditor.AssetDatabase.LoadAssetAtPath<Lighthouse.World.MapGeneration.Rules.MapGenerationConfig>("Assets/02.Prefabs/World/Data/MapGeneration/MapGenerationConfig.asset");
var totals=new System.Collections.Generic.Dictionary<int,int>();config.DepositBudget.CollectTotals(totals);
var rows=new System.Collections.Generic.List<object>();
for(int seed=1000;seed<1200;seed++){
 var r=Lighthouse.World.MapGeneration.Rules.MapBuilder.CreateDefault(config).Build(seed,true);
 var plans=new System.Collections.Generic.List<Lighthouse.World.Islands.Rules.IslandSpawnPlan>();
 var errors=new System.Collections.Generic.List<string>();
 bool planned=Lighthouse.World.Islands.Rules.IslandSpawnPlanner.TryBuild(config,r.Layout,plans,errors);
 var assignments=new System.Collections.Generic.List<System.Collections.Generic.List<Lighthouse.World.Harvest.Rules.SpotAssignment>>();
 bool assigned=planned && Lighthouse.World.Harvest.Rules.WorldDepositPlanner.TryBuild(config,plans,new Lighthouse.World.MapGeneration.Rules.SeededRandom(r.Seed),totals,assignments);
 var actual=assignments.SelectMany(x=>x).Where(x=>x.IsAssigned).GroupBy(x=>x.ResourceId).ToDictionary(x=>x.Key,x=>x.Sum(y=>y.InitialAmount));
 rows.Add(new{seed,planned,assigned,errors,actual,matches=totals.All(x=>actual.ContainsKey(x.Key)&&actual[x.Key]==x.Value)});
}
var bindings=UnityEditor.AssetDatabase.LoadMainAssetAtPath("Assets/02.Prefabs/World/Data/Spawning/MonsterSpawnBindings.asset");
var extra=UnityEditor.AssetDatabase.GetDependencies(UnityEditor.AssetDatabase.GetAssetPath(bindings),true).Select(p=>new{path=p,asset=UnityEditor.AssetDatabase.LoadMainAssetAtPath(p)}).Where(x=>x.asset is UnityEngine.ScriptableObject).Select(x=>new{path=x.path,guid=UnityEditor.AssetDatabase.AssetPathToGUID(x.path),type=x.asset.GetType().FullName,json=UnityEditor.EditorJsonUtility.ToJson(x.asset)}).ToArray();
return Newtonsoft.Json.JsonConvert.SerializeObject(new{rows,extra,budget=config.DepositBudget.Entries.Select(e=>new{id=e.Resource.Id,name=e.Resource.name,total=e.TotalAmount}).ToArray()});

