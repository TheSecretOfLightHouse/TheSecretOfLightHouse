if (UnityEditor.EditorApplication.isPlaying || UnityEditor.EditorApplication.isCompiling)
{
    throw new System.InvalidOperationException("BC2 prefab attachment requires edit mode with compiled scripts.");
}
var results = new System.Collections.Generic.List<object>();
foreach (string name in new[] { "Angler", "BlueMarline", "BlueShark", "BubbleTurtle", "Manta", "Spinocroc", "MosaWhale" })
{
    string path = "Assets/02.Prefabs/Monsters/" + name + ".prefab";
    var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
    try
    {
        var body = root.GetComponent<UnityEngine.Rigidbody>();
        var scratch = new System.Collections.Generic.List<UnityEngine.Collider>();
        if (!Lighthouse.World.Ocean.Rules.BuoyancyBoundsCollector.TryCollect(body, scratch, out var bounds))
        {
            throw new System.InvalidOperationException("No physical bounds: " + path);
        }
        body.useGravity = true;
        body.constraints &= ~UnityEngine.RigidbodyConstraints.FreezePositionY;
        var adapter = root.GetComponent<Lighthouse.Monsters.AI.Server.MonsterWaterBody>();
        if (adapter == null)
        {
            adapter = root.AddComponent<Lighthouse.Monsters.AI.Server.MonsterWaterBody>();
        }
        adapter.enabled = true;
        var buoyancy = root.GetComponent<Lighthouse.World.Ocean.BuoyancyObject>();
        var drift = root.GetComponent<Lighthouse.World.Ocean.WaveDriftBody>();
        var config = new UnityEditor.SerializedObject(adapter);
        var points = config.FindProperty("_buoyancyPoints");
        points.arraySize = 3;
        points.GetArrayElementAtIndex(0).vector3Value = new UnityEngine.Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        points.GetArrayElementAtIndex(1).vector3Value = bounds.center;
        points.GetArrayElementAtIndex(2).vector3Value = new UnityEngine.Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        float depth = name == "Angler" ? 2f : name == "Manta" ? 3f : 0f;
        if (name == "MosaWhale")
        {
            var cycle = root.GetComponent<Lighthouse.Monsters.MosaWhale.Server.MosaWhaleCycle>();
            depth = UnityEngine.Mathf.Max(0.01f, cycle.Settings.PatrolDepth - root.transform.TransformVector(bounds.center).y);
        }
        config.FindProperty("_defaultDepth").floatValue = depth;
        config.FindProperty("_transitionTimeout").floatValue = 15f;
        config.ApplyModifiedPropertiesWithoutUndo();
        var water = new UnityEditor.SerializedObject(buoyancy);
        water.FindProperty("_pool").intValue = (int)Lighthouse.World.Ocean.Rules.SamplerPool.Drift;
        water.FindProperty("_manualPoints").arraySize = 0;
        water.FindProperty("_depthTolerance").floatValue = 0.35f;
        var profile = water.FindProperty("_profile");
        profile.FindPropertyRelative("_buoyancyRatio").floatValue = 2f;
        profile.FindPropertyRelative("_verticalDrag").floatValue = 5f;
        profile.FindPropertyRelative("_horizontalDrag").floatValue = 0.2f;
        profile.FindPropertyRelative("_plateThickness").floatValue = bounds.size.y * UnityEngine.Mathf.Abs(root.transform.lossyScale.y) / 3f;
        profile.FindPropertyRelative("_targetStrength").floatValue = 16f;
        profile.FindPropertyRelative("_targetMaxAcceleration").floatValue = 40f;
        water.ApplyModifiedPropertiesWithoutUndo();
        var driftConfig = new UnityEditor.SerializedObject(drift);
        driftConfig.FindProperty("_depthFadeDistance").floatValue = 4f;
        driftConfig.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
        results.Add(new { path, depth, points = 3, height = bounds.size.y * UnityEngine.Mathf.Abs(root.transform.lossyScale.y) });
    }
    finally
    {
        UnityEditor.PrefabUtility.UnloadPrefabContents(root);
    }
}
return new { results };
