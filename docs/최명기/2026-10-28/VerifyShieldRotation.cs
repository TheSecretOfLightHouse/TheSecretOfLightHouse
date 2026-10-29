if(UnityEngine.Application.isPlaying)throw new System.Exception("Stopped Editor required");
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
System.Action<object,string> call=(o,n)=>o.GetType().GetMethod(n,flags).Invoke(o,null);
var rows=new System.Collections.Generic.List<string>();
foreach(var n in new[]{"captainBoat","Boat","TankerBoat","gardenBoat"}){
var root=UnityEditor.PrefabUtility.LoadPrefabContents("Assets/02.Prefabs/Player/"+n+".prefab");
Lighthouse.Player.Boat.View.BoatShieldView view=null;UnityEngine.GameObject shell=null;
try{
var boat=root.GetComponent<BoatController>();call(boat,"Awake");
view=root.GetComponent<Lighthouse.Player.Boat.View.BoatShieldView>();call(view,"Awake");
root.transform.rotation=UnityEngine.Quaternion.Euler(8f,40f,12f);
view.SetShieldVisible(true);
shell=(UnityEngine.GameObject)view.GetType().GetField("_shell",flags).GetValue(view);
if(!shell||shell.transform.parent)throw new System.Exception("Shield must be independent of boat hierarchy");
var center=(UnityEngine.Vector3)view.GetType().GetField("_localCenter",flags).GetValue(view);
var size=(UnityEngine.Vector3)view.GetType().GetField("_localSize",flags).GetValue(view);
foreach(var angles in new[]{new UnityEngine.Vector3(0,90,0),new UnityEngine.Vector3(22,180,30),new UnityEngine.Vector3(-18,275,-24)}){
root.transform.position+=new UnityEngine.Vector3(4f,.5f,-2f);root.transform.rotation=UnityEngine.Quaternion.Euler(angles);
view.GetType().GetField("_elapsed",flags).SetValue(view,.25f);
call(view,"Update");call(view,"LateUpdate");
if(UnityEngine.Vector3.Distance(shell.transform.position,root.transform.TransformPoint(center))>.0001f)throw new System.Exception("Shield position did not follow");
if(UnityEngine.Quaternion.Angle(shell.transform.rotation,UnityEngine.Quaternion.identity)>.001f)throw new System.Exception("Shield inherited boat rotation");
if(UnityEngine.Vector3.Distance(shell.transform.lossyScale,UnityEngine.Vector3.Scale(size,root.transform.lossyScale))>.0001f)throw new System.Exception("Shield changed size");
if(shell.scene!=root.scene)throw new System.Exception("Shield must unload with its boat scene");
}
rows.Add(n+": spawn orientation and 3 movement/yaw/pitch/roll cases passed");
}finally{
if(shell)UnityEngine.Object.DestroyImmediate(shell);
if(view)view.GetType().GetField("_shell",flags).SetValue(view,null);
UnityEditor.PrefabUtility.UnloadPrefabContents(root);
}
}
return rows.ToArray();
