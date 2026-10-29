if(UnityEngine.Application.isPlaying || Mirror.NetworkServer.active || Mirror.NetworkClient.active)throw new System.Exception("Requires stopped/disconnected Editor");
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
System.Action<object,string> call=(o,n)=>o.GetType().GetMethod(n,flags).Invoke(o,null);
System.Action<object,string,object> set=(o,n,v)=>o.GetType().GetField(n,flags).SetValue(o,v);
System.Func<object,string,object> get=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);
System.Action<bool,string> check=(ok,msg)=>{if(!ok)throw new System.Exception(msg);};
var active=typeof(Mirror.NetworkServer).GetProperty("active").GetSetMethod(true);
var rows=new System.Collections.Generic.List<string>();
foreach(var n in new[]{"captainBoat","Boat","TankerBoat","gardenBoat"}){
var root=UnityEditor.PrefabUtility.LoadPrefabContents("Assets/02.Prefabs/Player/"+n+".prefab");
try{
var identity=root.GetComponent<Mirror.NetworkIdentity>();call(identity,"InitializeNetworkBehaviours");typeof(Mirror.NetworkIdentity).GetProperty("isServer").SetValue(identity,true);
var durability=root.GetComponent<BoatDurability>();var shield=root.GetComponent<BoatShield>();var view=root.GetComponent<Lighthouse.Player.Boat.View.BoatShieldView>();
call(durability,"Awake");call(shield,"Awake");call(view,"Awake");call(root.GetComponent<BoatController>(),"Awake");
active.Invoke(null,new object[]{true});durability.OnStartServer();shield.OnStartServer();
check(!shield.HasShield,"Initially clear");check(shield.TryGrantShield(),"Grant accepted");check(shield.HasShield,"Shield state set");
check(!shield.TryGrantShield(),"Duplicate grant refused");
durability.ApplyDamage(17f);check(!shield.HasShield,"Hit consumes shield");check(durability.CurrentDurability==durability.MaxDurability,"First hit fully blocked");
durability.ApplyDamage(17f);check(durability.CurrentDurability==durability.MaxDurability-17f,"Next hit damages normally");
check(!shield.TryAbsorbDamage(),"Empty shield does not absorb");
active.Invoke(null,new object[]{false});
var shellPrefab=(UnityEngine.GameObject)get(view,"_shellPrefab");var pulsePrefab=(UnityEngine.GameObject)get(view,"_pulsePrefab");
check(shellPrefab && pulsePrefab,"Visual references present");
check(shellPrefab.GetComponentsInChildren<UnityEngine.Collider>(true).Length==0,"No shell collider");
check(shellPrefab.GetComponentsInChildren<UnityEngine.Rigidbody>(true).Length==0,"No shell rigidbody");
check(shellPrefab.GetComponentsInChildren<UnityEngine.Animation>(true).Length==0,"No repeated growth animation");
check(shellPrefab.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true).Length==0,"No demo scripts");
view.SetShieldVisible(true);var shell=(UnityEngine.GameObject)get(view,"_shell");check(shell,"Shell appears");
view.SetShieldVisible(true);check((UnityEngine.GameObject)get(view,"_shell")==shell,"Repeated state does not duplicate shell");
set(view,"_elapsed",.25f);call(view,"Update");check(shell.transform.localScale==UnityEngine.Vector3.Scale((UnityEngine.Vector3)get(view,"_localSize"),root.transform.lossyScale),"Growth reaches intended world size");
UnityEngine.Object.DestroyImmediate(shell);set(view,"_shell",null);
var pulse=UnityEngine.Object.Instantiate(pulsePrefab);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(pulse,root.scene);
try{int total=0;foreach(var p in pulse.GetComponentsInChildren<UnityEngine.ParticleSystem>(true)){p.Simulate(.08f,true,true,true);total+=p.particleCount;}check(total>0,"Pulse emits");foreach(var p in pulse.GetComponentsInChildren<UnityEngine.ParticleSystem>(true)){p.Simulate(3f,true,true,true);check(p.particleCount==0,"Pulse expires");}}finally{UnityEngine.Object.DestroyImmediate(pulse);}
rows.Add(n+": shield rules, visual references, shell growth/idempotence and pulse emission/expiry passed");
}finally{active.Invoke(null,new object[]{false});UnityEditor.PrefabUtility.UnloadPrefabContents(root);}
}
return rows.ToArray();
