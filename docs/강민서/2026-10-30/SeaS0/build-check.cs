var config=UnityEditor.AssetDatabase.LoadAssetAtPath<Lighthouse.World.MapGeneration.Rules.MapGenerationConfig>("Assets/02.Prefabs/World/Data/MapGeneration/MapGenerationConfig.asset");
var spawn=UnityEditor.AssetDatabase.LoadAssetAtPath<Lighthouse.World.Spawning.Rules.SpawnerPlacementConfig>("Assets/02.Prefabs/World/Data/SpawnerPlacementConfig_Test.asset");
var rows=new System.Collections.Generic.List<object>();
var results=new System.Collections.Generic.List<Lighthouse.World.MapGeneration.Rules.MapBuildResult>();
var builder=Lighthouse.World.MapGeneration.Rules.MapBuilder.CreateDefault(config);
var watch=System.Diagnostics.Stopwatch.StartNew();
for(int seed=1000;seed<1200;seed++){
 try {var r=builder.Build(seed,true);results.Add(r);
 var client=builder.Build(r.Seed,false);
 int count=-1;string error=null;
 try{count=Lighthouse.World.Spawning.Server.SpawnerPlacementStep.Build(r.Layout,config,spawn,(ulong)seed).Count;}catch(System.Exception ex){error=ex.Message;}
 rows.Add(new{requestedSeed=seed,usedSeed=r.Seed,attempts=r.Attempts,valid=r.IsValid,issues=r.Issues.ToArray(),hash=r.Layout?.Hash,islands=r.Layout?.Islands.Count,reefs=r.Layout?.Reefs.Count,clientMatches=client.Layout?.Hash==r.Layout?.Hash,spawnerSeed=seed,spawners=count,spawnError=error});
 }catch(System.Exception ex){rows.Add(new{requestedSeed=seed,error=ex.ToString()});}
}
watch.Stop();
var repeatA=builder.Build(1000,true);var repeatB=builder.Build(1000,true);
var log=new System.Collections.Generic.List<string>();
UnityEngine.Application.LogCallback callback=(message,trace,type)=>{if(message.Contains("[MapCheck]"))log.Add(message);};
UnityEngine.Application.logMessageReceived+=callback;
bool quota=false,spacing=false,geometry=false;
try{quota=Lighthouse.World.MapGeneration.Sandbox.MapLayoutRuleChecks.CheckQuotas(config,results);spacing=Lighthouse.World.MapGeneration.Sandbox.MapLayoutRuleChecks.CheckSameTypeSpacing(config,results);geometry=Lighthouse.World.MapGeneration.Sandbox.MapLayoutRuleChecks.CheckGeometry(config,results);}finally{UnityEngine.Application.logMessageReceived-=callback;}
return Newtonsoft.Json.JsonConvert.SerializeObject(new{rows,elapsedMs=watch.Elapsed.TotalMilliseconds,sameSeed=repeatA.Layout.Hash==repeatB.Layout.Hash,quota,spacing,geometry,log,representativeLayout=repeatA.Layout.Islands.Select(i=>new{i.Index,i.ArchetypeId,i.VariantIndex,zone=i.Zone.ToString(),x=i.Position.x,z=i.Position.y,i.Radius,i.YawDegrees}).ToArray()});

