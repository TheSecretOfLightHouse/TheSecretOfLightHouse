// Read-only S4 simulation: clones MapGenerationConfig in memory (zone radii only), never saves assets.
var path = "Assets/02.Prefabs/World/Data/MapGeneration/MapGenerationConfig.asset";
var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<Lighthouse.World.MapGeneration.Rules.MapGenerationConfig>(path);
var spCfg = UnityEditor.AssetDatabase.LoadAssetAtPath<Lighthouse.World.Spawning.Rules.SpawnerPlacementConfig>("Assets/02.Prefabs/World/Data/SpawnerPlacementConfig_Test.asset");
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
// t = 0.9 + d / 7.70 (S2 Calm measurement 2 fit)
System.Func<float, float> tm = d => 0.9f + d / 7.70f;

System.Func<string, float[], float, int, float[], object> run = (name, zr, scale, attemptsOverride, extras) =>
{
    var cc = UnityEngine.Object.Instantiate(cfg);
    var so = new UnityEditor.SerializedObject(cc);
    so.FindProperty("_radiusScale").floatValue = scale;
    if (attemptsOverride > 0) so.FindProperty("_maxBuildAttempts").intValue = attemptsOverride;
    var zp = so.FindProperty("_zones");
    for (int i = 0; i < 3; i++)
    {
        zp.GetArrayElementAtIndex(i).FindPropertyRelative("_innerRadius").floatValue = zr[i * 2];
        zp.GetArrayElementAtIndex(i).FindPropertyRelative("_outerRadius").floatValue = zr[i * 2 + 1];
        zp.GetArrayElementAtIndex(i).FindPropertyRelative("_extraIslandSpacing").floatValue = extras[i];
    }
    so.ApplyModifiedPropertiesWithoutUndo();
    var cfgErrors = new System.Collections.Generic.List<string>();
    bool cfgOk = Lighthouse.World.MapGeneration.Rules.MapGenerationConfigValidator.TryValidate(cc, cfgErrors);
    float midR = zr[1] * scale, outR = zr[3] * scale;
    bool wallOk = (midR - 10f > spCfg.SafeRadius) && (outR - midR > 20f);

    var builder = Lighthouse.World.MapGeneration.Rules.MapBuilder.CreateDefault(cc);
    var seen = new System.Collections.Generic.HashSet<int>();
    int invalid = 0, uniq = 0, retries = 0, maxAtt = 0, spOk = 0, spFail = 0;
    var issueKinds = new System.Collections.Generic.Dictionary<string, int>();
    var spErr = new System.Collections.Generic.List<string>();
    var counts = new System.Collections.Generic.List<float>(); var gaps = new System.Collections.Generic.List<float>();
    var dLh = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<float>>();
    var cLh = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<float>>();
    var tZ = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<float>>();
    var far = new System.Collections.Generic.List<float>(); var reefN = new System.Collections.Generic.List<float>();
    int legs = 0, blocked = 0;
    var spCount = new System.Collections.Generic.List<float>(); var spIsl = new System.Collections.Generic.List<float>(); var spLh = new System.Collections.Generic.List<float>();
    int spNear = 0, spTotal = 0;
    var spBy = new System.Collections.Generic.Dictionary<string, int>();
    var pts = new System.Collections.Generic.List<Lighthouse.World.Spawning.Rules.SpawnerPoint>();
    for (int seed = 1000; seed < 1200; seed++)
    {
        var r = builder.Build(seed, true);
        if (!r.IsValid || r.Layout == null)
        {
            invalid++;
            if (r.Issues != null) foreach (var s in r.Issues.Take(2)) { var k = System.Text.RegularExpressions.Regex.Replace(s, "[0-9.]+", "#"); issueKinds[k] = issueKinds.ContainsKey(k) ? issueKinds[k] + 1 : 1; }
            continue;
        }
        if (r.Attempts > 1) retries++; maxAtt = System.Math.Max(maxAtt, r.Attempts);
        if (!seen.Add(r.Seed)) continue; uniq++;
        var isl = r.Layout.Islands; var reefs = r.Layout.Reefs; counts.Add(isl.Count); reefN.Add(reefs.Count);
        System.Func<Lighthouse.World.MapGeneration.Rules.IslandPlacement, UnityEngine.Vector2[]> dw = p =>
        { var q = UnityEngine.Quaternion.Euler(0f, p.YawDegrees, 0f); return docks[p.ArchetypeId].Select(d => { var w = q * d; return new UnityEngine.Vector2(p.Position.x + w.x, p.Position.y + w.z); }).ToArray(); };
        var lh = isl.First(i => i.ArchetypeId == 1000); var lhd = dw(lh); float fmax = 0f;
        for (int j = 0; j < isl.Count; j++)
        {
            var p = isl[j]; var pd = dw(p);
            for (int k = j + 1; k < isl.Count; k++) gaps.Add((p.Position - isl[k].Position).magnitude - realR[p.ArchetypeId] - realR[isl[k].ArchetypeId]);
            if (p.ArchetypeId == 1000) continue;
            float best = float.MaxValue; UnityEngine.Vector2 ba = default, bb = default;
            foreach (var a in lhd) foreach (var b in pd) { float d = (a - b).magnitude; if (d < best) { best = d; ba = a; bb = b; } }
            string z = p.Zone.ToString(); if (!dLh.ContainsKey(z)) { dLh[z] = new System.Collections.Generic.List<float>(); cLh[z] = new System.Collections.Generic.List<float>(); tZ[z] = new System.Collections.Generic.List<float>(); }
            dLh[z].Add(best); cLh[z].Add((p.Position - lh.Position).magnitude); tZ[z].Add(tm(best)); fmax = UnityEngine.Mathf.Max(fmax, best); legs++;
            bool hit = false; foreach (var rf in reefs) if (segDist(ba, bb, rf.Position) < rf.Radius + 6f) { hit = true; break; }
            for (int k = 0; k < isl.Count && !hit; k++) { if (k == j || isl[k].ArchetypeId == 1000) continue; if (segDist(ba, bb, isl[k].Position) < realR[isl[k].ArchetypeId] + 6f) hit = true; }
            if (hit) blocked++;
        }
        far.Add(fmax);
        try
        {
            var reg = Lighthouse.World.Spawning.Server.SpawnerPlacementStep.Build(r.Layout, cc, spCfg, (ulong)seed);
            reg.SetUnlockedZones(Lighthouse.World.Ocean.Rules.SeaZone.All); pts.Clear(); reg.QuerySpawners(UnityEngine.Vector3.zero, 5000f, pts);
            spOk++; spCount.Add(pts.Count);
            foreach (var sp in pts)
            {
                var sp2 = new UnityEngine.Vector2(sp.Position.x, sp.Position.z);
                float m = float.MaxValue; foreach (var i2 in isl) m = UnityEngine.Mathf.Min(m, (sp2 - i2.Position).magnitude - realR[i2.ArchetypeId]);
                spIsl.Add(m); spLh.Add(sp2.magnitude); spTotal++; if (m < 10f) spNear++;
                string zk = sp.Zone.ToString(); spBy[zk] = spBy.ContainsKey(zk) ? spBy[zk] + 1 : 1;
            }
        }
        catch (System.Exception ex) { spFail++; if (spErr.Count < 3) spErr.Add(seed + ": " + ex.Message); }
    }
    var res = new { name, zr, scale, cfgOk, cfgErrors, wallOk, invalid, issueKinds, uniqueLayouts = uniq, retriedBuilds = retries, maxAttempts = maxAtt,
        islandCount = stats(counts), reefCount = stats(reefN), coastGapRealM = stats(gaps), blockedStraightLegPct = legs > 0 ? 100f * blocked / legs : 0f, farthestDockFromLighthouseM = stats(far),
        dockFromLighthouseByZone = dLh.ToDictionary(k => k.Key, k => stats(k.Value)), centerFromLighthouseByZone = cLh.ToDictionary(k => k.Key, k => stats(k.Value)),
        modelCruiseSecByZone = tZ.ToDictionary(k => k.Key, k => stats(k.Value)),
        spawner = new { ok = spOk, fail = spFail, errors = spErr, count = stats(spCount), pointsByZone = spBy, gapToIslandCoastM = stats(spIsl), distFromOriginM = stats(spLh), nearIslandUnder10m = spNear, total = spTotal } };
    UnityEngine.Object.DestroyImmediate(cc);
    return res;
};

var results = new System.Collections.Generic.List<object>();
var zrA = new float[6]; var exA = new float[3];
for (int zi = 0; zi < 3; zi++) { zrA[zi*2] = cfg.Zones[zi].InnerRadius; zrA[zi*2+1] = cfg.Zones[zi].OuterRadius; exA[zi] = cfg.Zones[zi].ExtraIslandSpacing; }
results.Add(run("applied_asset|retry", zrA, cfg.RadiusScale, 0, exA));
results.Add(run("applied_asset|first", zrA, cfg.RadiusScale, 1, exA));
var json = Newtonsoft.Json.JsonConvert.SerializeObject(results, Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText(@"C:\Users\inha\Documents\GitHub\TheSecretOfLightHouse\docs\강민서\2026-10-30\SeaS4\applied-result.json", json, new System.Text.UTF8Encoding(false));
return "written " + json.Length;
