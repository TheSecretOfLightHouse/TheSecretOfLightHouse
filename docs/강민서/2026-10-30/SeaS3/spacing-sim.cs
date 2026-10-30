// Read-only simulation: clones MapGenerationConfig + archetypes in memory, never saves.
var path = "Assets/02.Prefabs/World/Data/MapGeneration/MapGenerationConfig.asset";
var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<Lighthouse.World.MapGeneration.Rules.MapGenerationConfig>(path);
var realR = new System.Collections.Generic.Dictionary<int, float> { {1000, 18.253f}, {101, 9.366f}, {102, 10.409f}, {103, 10.640f}, {104, 8.043f}, {105, 10.053f} };
var docks = new System.Collections.Generic.Dictionary<int, UnityEngine.Vector3[]>
{
    {1000, new[] { new UnityEngine.Vector3(-6.4f, 0.5f, -9.5f), new UnityEngine.Vector3(12f, 0.5f, -3.7f) }},
    {101, new[] { new UnityEngine.Vector3(-2.5f, 1.08f, -4.35f) }}, {102, new[] { new UnityEngine.Vector3(3.3f, 0.88f, -4.16f) }},
    {103, new[] { new UnityEngine.Vector3(-5.63f, 0.86f, -2.24f) }}, {104, new[] { new UnityEngine.Vector3(-3.1f, 0.8f, -2.5f) }},
    {105, new[] { new UnityEngine.Vector3(2.88f, 0.41f, -4.78f) }}
};
System.Func<System.Collections.Generic.List<float>, object> stats = l =>
{
    if (l.Count == 0) return new { n = 0 };
    var s = l.OrderBy(x => x).ToList();
    System.Func<float, float> pc = q => s[UnityEngine.Mathf.Min(s.Count - 1, (int)(q * (s.Count - 1) + 0.5f))];
    return new { n = s.Count, min = s[0], p10 = pc(0.1f), median = pc(0.5f), p90 = pc(0.9f), max = s[s.Count - 1] };
};
System.Func<UnityEngine.Vector2, UnityEngine.Vector2, UnityEngine.Vector2, float> segDist = (a, b, c) =>
{
    var ab = b - a; float l = ab.sqrMagnitude; float t = l < 1e-6f ? 0f : UnityEngine.Mathf.Clamp01(UnityEngine.Vector2.Dot(c - a, ab) / l);
    return (c - (a + ab * t)).magnitude;
};

System.Func<string, System.Collections.Generic.Dictionary<int, float>, float, float[], float, object> run = (name, radii, channel, extras, scale) =>
{
    var cc = UnityEngine.Object.Instantiate(cfg);
    var so = new UnityEditor.SerializedObject(cc);
    var arr = so.FindProperty("_archetypes");
    for (int i = 0; i < arr.arraySize; i++)
    {
        var src = (Lighthouse.World.Islands.Rules.IslandArchetype)arr.GetArrayElementAtIndex(i).objectReferenceValue;
        var cl = UnityEngine.Object.Instantiate(src);
        var so2 = new UnityEditor.SerializedObject(cl); so2.FindProperty("_radius").floatValue = radii[src.Id]; so2.ApplyModifiedPropertiesWithoutUndo();
        arr.GetArrayElementAtIndex(i).objectReferenceValue = cl;
    }
    var lhP = so.FindProperty("_lighthouseIsland"); var lhSrc = (Lighthouse.World.Islands.Rules.IslandArchetype)lhP.objectReferenceValue;
    var lhCl = UnityEngine.Object.Instantiate(lhSrc); var so3 = new UnityEditor.SerializedObject(lhCl); so3.FindProperty("_radius").floatValue = radii[lhSrc.Id]; so3.ApplyModifiedPropertiesWithoutUndo();
    lhP.objectReferenceValue = lhCl;
    so.FindProperty("_sailingChannelWidth").floatValue = channel; so.FindProperty("_radiusScale").floatValue = scale;
    var zp = so.FindProperty("_zones");
    for (int i = 0; i < zp.arraySize && i < extras.Length; i++) zp.GetArrayElementAtIndex(i).FindPropertyRelative("_extraIslandSpacing").floatValue = extras[i];
    so.ApplyModifiedPropertiesWithoutUndo();

    var builder = Lighthouse.World.MapGeneration.Rules.MapBuilder.CreateDefault(cc);
    var seen = new System.Collections.Generic.HashSet<int>();
    int invalid = 0, uniq = 0, retries = 0, maxAtt = 0;
    var firstIssues = new System.Collections.Generic.List<string>();
    var counts = new System.Collections.Generic.List<float>(); var gaps = new System.Collections.Generic.List<float>();
    var dLh = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<float>>();
    var cLh = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<float>>();
    var nn = new System.Collections.Generic.List<float>(); int legs = 0, blocked = 0; var far = new System.Collections.Generic.List<float>();
    for (int seed = 1000; seed < 1200; seed++)
    {
        var r = builder.Build(seed, true);
        if (!r.IsValid || r.Layout == null) { invalid++; if (firstIssues.Count < 3 && r.Issues != null) firstIssues.AddRange(r.Issues.Take(1)); continue; }
        if (r.Attempts > 1) retries++; maxAtt = System.Math.Max(maxAtt, r.Attempts);
        if (!seen.Add(r.Seed)) continue; uniq++;
        var isl = r.Layout.Islands; var reefs = r.Layout.Reefs; counts.Add(isl.Count);
        System.Func<Lighthouse.World.MapGeneration.Rules.IslandPlacement, UnityEngine.Vector2[]> dw = p =>
        { var q = UnityEngine.Quaternion.Euler(0f, p.YawDegrees, 0f); return docks[p.ArchetypeId].Select(d => { var w = q * d; return new UnityEngine.Vector2(p.Position.x + w.x, p.Position.y + w.z); }).ToArray(); };
        var lh = isl.First(i => i.ArchetypeId == 1000); var lhd = dw(lh); float fmax = 0f;
        for (int j = 0; j < isl.Count; j++)
        {
            var p = isl[j]; var pd = dw(p);
            for (int k = j + 1; k < isl.Count; k++) gaps.Add((p.Position - isl[k].Position).magnitude - realR[p.ArchetypeId] - realR[isl[k].ArchetypeId]);
            float nnj = float.MaxValue; for (int k = 0; k < isl.Count; k++) { if (k == j) continue; foreach (var a in pd) foreach (var b in dw(isl[k])) nnj = UnityEngine.Mathf.Min(nnj, (a - b).magnitude); }
            nn.Add(nnj);
            if (p.ArchetypeId == 1000) continue;
            float best = float.MaxValue; UnityEngine.Vector2 ba = default, bb = default;
            foreach (var a in lhd) foreach (var b in pd) { float d = (a - b).magnitude; if (d < best) { best = d; ba = a; bb = b; } }
            string z = p.Zone.ToString(); if (!dLh.ContainsKey(z)) { dLh[z] = new System.Collections.Generic.List<float>(); cLh[z] = new System.Collections.Generic.List<float>(); }
            dLh[z].Add(best); cLh[z].Add((p.Position - lh.Position).magnitude); fmax = UnityEngine.Mathf.Max(fmax, best); legs++;
            bool hit = false; foreach (var rf in reefs) if (segDist(ba, bb, rf.Position) < rf.Radius + 6f) { hit = true; break; }
            for (int k = 0; k < isl.Count && !hit; k++) { if (k == j || isl[k].ArchetypeId == 1000) continue; if (segDist(ba, bb, isl[k].Position) < realR[isl[k].ArchetypeId] + 6f) hit = true; }
            if (hit) blocked++;
        }
        far.Add(fmax);
    }
    var res = new { name, channel, scale, extras, radii = radii.OrderBy(k => k.Key).ToDictionary(k => k.Key.ToString(), k => k.Value), invalid, uniqueLayouts = uniq, retriedBuilds = retries, maxAttempts = maxAtt, firstIssues,
        islandCount = stats(counts), coastGapRealM = stats(gaps), nearestNeighborDockM = stats(nn), blockedStraightLegPct = legs > 0 ? 100f * blocked / legs : 0f, farthestDockFromLighthouseM = stats(far),
        dockFromLighthouseByZone = dLh.ToDictionary(k => k.Key, k => stats(k.Value)), centerFromLighthouseByZone = cLh.ToDictionary(k => k.Key, k => stats(k.Value)) };
    UnityEngine.Object.DestroyImmediate(cc);
    return res;
};

var baseR = new System.Collections.Generic.Dictionary<int, float> { {1000, 33f}, {101, 33f}, {102, 34f}, {103, 34f}, {104, 32f}, {105, 32f} };
var realRound = new System.Collections.Generic.Dictionary<int, float> { {1000, 19f}, {101, 10f}, {102, 11f}, {103, 11f}, {104, 9f}, {105, 11f} };
var results = new System.Collections.Generic.List<object>();
results.Add(run("A_base", baseR, 20f, new[] { 0f, 4f, 8f }, 1f));
results.Add(run("B_realR_ch20_ex048", realRound, 20f, new[] { 0f, 4f, 8f }, 1f));
results.Add(run("C_realR_ch20_ex0", realRound, 20f, new[] { 0f, 0f, 0f }, 1f));
results.Add(run("D_realR_ch14_ex0", realRound, 14f, new[] { 0f, 0f, 0f }, 1f));
results.Add(run("E_realR_ch20_ex0_scale048", realRound, 20f, new[] { 0f, 0f, 0f }, 0.48f));
results.Add(run("F_realR_ch14_ex0_scale048", realRound, 14f, new[] { 0f, 0f, 0f }, 0.48f));
var json = Newtonsoft.Json.JsonConvert.SerializeObject(results, Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText(@"C:\Users\inha\Documents\GitHub\TheSecretOfLightHouse\docs\강민서\2026-10-30\SeaS3\spacing-sim-result.json", json, new System.Text.UTF8Encoding(false));
return Newtonsoft.Json.JsonConvert.SerializeObject(results);
