var config=UnityEditor.AssetDatabase.LoadAssetAtPath<Lighthouse.World.MapGeneration.Rules.MapGenerationConfig>("Assets/02.Prefabs/World/Data/MapGeneration/MapGenerationConfig.asset");
bool Active(UnityEngine.Transform t){while(t!=null){if(!t.gameObject.activeSelf)return false;t=t.parent;}return true;}
float[] V(UnityEngine.Vector3 v)=>new[]{v.x,v.y,v.z};
var result=new System.Collections.Generic.List<object>();
foreach(var a in new[]{config.LighthouseIsland}.Concat(config.Archetypes).Distinct()){
 for(int vi=0;vi<a.Variants.Count;vi++){
 var p=a.Variants[vi].Prefab; if(p==null)throw new System.Exception("Missing variant");
 var rt=p.transform;var matrix=UnityEngine.Matrix4x4.TRS(UnityEngine.Vector3.zero,rt.localRotation,rt.localScale)*rt.worldToLocalMatrix;
 var records=new System.Collections.Generic.List<object>();
 var groups=new System.Collections.Generic.Dictionary<string,System.Collections.Generic.List<UnityEngine.Vector3>>();
 foreach(var key in new[]{"ground","solid","dockVisual","dockTrigger","placement","allVisual"})groups[key]=new System.Collections.Generic.List<UnityEngine.Vector3>();
 UnityEngine.Vector3[] Corners(UnityEngine.Bounds b){var q=new UnityEngine.Vector3[8];for(int i=0;i<8;i++)q[i]=b.center+UnityEngine.Vector3.Scale(b.extents,new UnityEngine.Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));return q;}
 object Stats(System.Collections.Generic.List<UnityEngine.Vector3> pts){if(pts.Count==0)return null;var min=pts[0];var max=min;float r=0;foreach(var q in pts){min=UnityEngine.Vector3.Min(min,q);max=UnityEngine.Vector3.Max(max,q);r=UnityEngine.Mathf.Max(r,new UnityEngine.Vector2(q.x,q.z).magnitude);}var c=(min+max)*0.5f;return new {min=V(min),max=V(max),size=V(max-min),center=V(c),rootRadius=r,count=pts.Count};}
 void Add(string path,string type,string group,string method,UnityEngine.Vector3[] pts,bool included,string reason){records.Add(new{path,type,group,method,included,reason,bounds=Stats(pts.ToList())});if(group!="excluded")groups[group].AddRange(pts);if(included)groups["placement"].AddRange(pts);}
 foreach(var c in p.GetComponentsInChildren<UnityEngine.Collider>(true)){
 string path=UnityEditor.AnimationUtility.CalculateTransformPath(c.transform,rt);string group=!Active(c.transform)||!c.enabled?"excluded":c.isTrigger?(c.GetComponent<DockZone>()!=null?"dockTrigger":"excluded"):"solid";
 var pts=new UnityEngine.Vector3[0];string method="none";var m=matrix*c.transform.localToWorldMatrix;
 if(c is UnityEngine.MeshCollider mc&&mc.sharedMesh!=null){try{pts=mc.sharedMesh.vertices;method="meshVertices";}catch{pts=Corners(mc.sharedMesh.bounds);method="meshBoundsFallback";}}
 else if(c is UnityEngine.BoxCollider bc){pts=Corners(new UnityEngine.Bounds(bc.center,bc.size));method="boxCorners";}
 else if(c is UnityEngine.SphereCollider sc){pts=Corners(new UnityEngine.Bounds(sc.center,UnityEngine.Vector3.one*sc.radius*2));method="sphereBoxConservative";}
 else if(c is UnityEngine.CapsuleCollider cc){var size=UnityEngine.Vector3.one*cc.radius*2;size[cc.direction]=UnityEngine.Mathf.Max(cc.height,cc.radius*2);pts=Corners(new UnityEngine.Bounds(cc.center,size));method="capsuleBoxConservative";}
 else throw new System.Exception("Unhandled collider "+c.GetType().Name+" "+path);
 pts=pts.Select(q=>m.MultiplyPoint3x4(q)).ToArray();
 Add(path,c.GetType().Name,group,method,pts,group=="solid",group=="solid"?"active enabled solid":group=="dockTrigger"?"approach reservation separate":"inactive/disabled/non-dock trigger");
 }
 foreach(var mf in p.GetComponentsInChildren<UnityEngine.MeshFilter>(true)){
 var mr=mf.GetComponent<UnityEngine.MeshRenderer>();if(mr==null||mf.sharedMesh==null)continue;
 string path=UnityEditor.AnimationUtility.CalculateTransformPath(mf.transform,rt);bool active=Active(mf.transform)&&mr.enabled;
 bool ground=path.StartsWith("Ground/")||path=="Ground";
 bool dock=System.Text.RegularExpressions.Regex.IsMatch(path,"Dock|Pier|Jetty",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
 string group=!active?"excluded":ground?"ground":dock?"dockVisual":"allVisual";
 UnityEngine.Vector3[] pts;string method;try{pts=mf.sharedMesh.vertices;method="meshVertices";}catch{pts=Corners(mf.sharedMesh.bounds);method="meshBoundsFallback";}
 var m=matrix*mf.transform.localToWorldMatrix;pts=pts.Select(q=>m.MultiplyPoint3x4(q)).ToArray();
 Add(path,"MeshRenderer",group,method,pts,active&&(ground||dock),active?(ground?"land mesh including submerged skirt":dock?"dock visual occupancy":"visual-only vegetation/props; solid collider included separately"):"inactive/disabled renderer");
 }
 var docks=p.GetComponentsInChildren<DockZone>(true).Select(d=>{var t=d.transform;var pos=matrix.MultiplyPoint3x4(t.position);var f=matrix.MultiplyVector(t.forward).normalized;return new{path=UnityEditor.AnimationUtility.CalculateTransformPath(t,rt),active=Active(t)&&d.enabled,position=V(pos),forward=V(f),yaw=UnityEngine.Mathf.Atan2(f.x,f.z)*UnityEngine.Mathf.Rad2Deg,landingSurfaces=d.LandingSurfaces.Select(c=>c==null?null:UnityEditor.AnimationUtility.CalculateTransformPath(c.transform,rt)).ToArray()};}).ToArray();
 var rotationAudit=new[]{0f,37f,90f,181f,270f}.Select(yaw=>{var rot=UnityEngine.Quaternion.Euler(0,yaw,0);float radius=0;foreach(var pt in groups["placement"]){var q=rot*pt;radius=UnityEngine.Mathf.Max(radius,new UnityEngine.Vector2(q.x,q.z).magnitude);}return new{yaw,radius};}).ToArray();
 result.Add(new {rotationAudit,name=p.name,id=a.Id,archetype=UnityEditor.AssetDatabase.GetAssetPath(a),variant=vi,weight=a.Variants[vi].Weight,prefab=UnityEditor.AssetDatabase.GetAssetPath(p),guid=UnityEditor.AssetDatabase.AssetPathToGUID(UnityEditor.AssetDatabase.GetAssetPath(p)),configuredRadius=a.Radius,rootPosition=V(rt.localPosition),rootRotation=V(rt.localEulerAngles),rootScale=V(rt.localScale),terrainCount=p.GetComponentsInChildren<UnityEngine.Terrain>(true).Length,groups=groups.ToDictionary(k=>k.Key,k=>Stats(k.Value)),docks,records});
 }
}
return result;

