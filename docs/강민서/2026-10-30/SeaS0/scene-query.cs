var output = new System.Collections.Generic.List<object>();
foreach(var path in new[]{"Assets/00.Scenes/Main/MainGame.unity","Assets/00.Scenes/Minseo/WorldSessionGame.unity"}) {
 if(!System.IO.File.Exists(path)){output.Add(new{path,error="not found"});continue;}
 var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(path);
 try {foreach(var root in scene.GetRootGameObjects()) foreach(var c in root.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true)) {
 if(c==null)continue;
 var n=c.GetType().FullName;
 if(!(n.Contains("MapStateNet")||n.Contains("GameClock")||n.Contains("IslandWorldHost")||n.Contains("Spawn")||n.Contains("Weather")||n.Contains("StormWall")||n.Contains("IslandInstanceSet")||n.Contains("WaterSamplerHost")||n.Contains("DayNightSun")))continue;
 var so=new UnityEditor.SerializedObject(c);var p=so.GetIterator();var fields=new System.Collections.Generic.Dictionary<string,object>();
 while(p.NextVisible(true)){if(p.propertyType==UnityEditor.SerializedPropertyType.ObjectReference)fields[p.propertyPath]=p.objectReferenceValue==null?null:(object)new {name=p.objectReferenceValue.name,path=UnityEditor.AssetDatabase.GetAssetPath(p.objectReferenceValue),type=p.objectReferenceValue.GetType().FullName};else if(p.propertyType==UnityEditor.SerializedPropertyType.Float)fields[p.propertyPath]=p.floatValue;else if(p.propertyType==UnityEditor.SerializedPropertyType.String)fields[p.propertyPath]=p.stringValue;else if(p.propertyType==UnityEditor.SerializedPropertyType.Enum)fields[p.propertyPath]=p.enumValueIndex;else if(p.propertyType==UnityEditor.SerializedPropertyType.Integer)fields[p.propertyPath]=p.intValue;else if(p.propertyType==UnityEditor.SerializedPropertyType.Boolean)fields[p.propertyPath]=p.boolValue;}
 output.Add(new{scene=path,gameObject=c.gameObject.name,type=n,active=c.gameObject.activeInHierarchy,enabled=c.enabled,fields});}}
 finally {UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
}
return output;
