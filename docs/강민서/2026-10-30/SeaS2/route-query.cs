
var config = UnityEditor.AssetDatabase.LoadAssetAtPath<Lighthouse.World.MapGeneration.Rules.MapGenerationConfig>("Assets/02.Prefabs/World/Data/MapGeneration/MapGenerationConfig.asset");
var builder = Lighthouse.World.MapGeneration.Rules.MapBuilder.CreateDefault(config);

// S1 root-local DockZone positions (m) and S1 dock-inclusive radii (m).
var docks = new System.Collections.Generic.Dictionary<int, UnityEngine.Vector3[]>
{
    {1000, new[] { new UnityEngine.Vector3(-6.4f, 0.5f, -9.5f), new UnityEngine.Vector3(12f, 0.5f, -3.7f) }},
    {101, new[] { new UnityEngine.Vector3(-2.5f, 1.08f, -4.35f) }},
    {102, new[] { new UnityEngine.Vector3(3.3f, 0.88f, -4.16f) }},
    {103, new[] { new UnityEngine.Vector3(-5.63f, 0.86f, -2.24f) }},
    {104, new[] { new UnityEngine.Vector3(-3.1f, 0.8f, -2.5f) }},
    {105, new[] { new UnityEngine.Vector3(2.88f, 0.41f, -4.78f) }}
};
var realR = new System.Collections.Generic.Dictionary<int, float>
{
    {1000, 18.253f}, {101, 9.366f}, {102, 10.409f}, {103, 10.640f}, {104, 8.043f}, {105, 10.053f}
};
const float clearance = 6f;

System.Func<Lighthouse.World.MapGeneration.Rules.IslandPlacement, UnityEngine.Vector2[]> dockWorld = p =>
{
    var q = UnityEngine.Quaternion.Euler(0f, p.YawDegrees, 0f);
    return docks[p.ArchetypeId].Select(d => { var w = q * d; return new UnityEngine.Vector2(p.Position.x + w.x, p.Position.y + w.z); }).ToArray();
};
System.Func<UnityEngine.Vector2, UnityEngine.Vector2, UnityEngine.Vector2, float> segDist = (a, b, c) =>
{
    var ab = b - a; float l = ab.sqrMagnitude;
    float t = l < 1e-6f ? 0f : UnityEngine.Mathf.Clamp01(UnityEngine.Vector2.Dot(c - a, ab) / l);
    return (c - (a + ab * t)).magnitude;
};
System.Func<float, float, float, float> cruise = (d, v, a) =>
{
    float rampD = v * v / (2f * a), rampT = v / a;
    return d <= rampD ? UnityEngine.Mathf.Sqrt(2f * d / a) : rampT + (d - rampD) / v;
};
System.Func<System.Collections.Generic.List<float>, object> stats = l =>
{
    if (l.Count == 0) return new { n = 0 };
    var s = l.OrderBy(x => x).ToList();
    System.Func<float, float> pc = q => s[UnityEngine.Mathf.Min(s.Count - 1, (int)(q * (s.Count - 1) + 0.5f))];
    return new { n = s.Count, min = s[0], p10 = pc(0.1f), median = pc(0.5f), p90 = pc(0.9f), max = s[s.Count - 1], mean = s.Average() };
};

var seen = new System.Collections.Generic.HashSet<int>();
int requested = 0, invalid = 0, duplicates = 0, layouts = 0, blocked = 0, legs = 0;
var dLhAll = new System.Collections.Generic.List<float>(); var cLhAll = new System.Collections.Generic.List<float>(); var nnAll = new System.Collections.Generic.List<float>(); var farthest = new System.Collections.Generic.List<float>();
var tN = new System.Collections.Generic.List<float>(); var tV = new System.Collections.Generic.List<float>();
var byZone = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<float>>();
var byType = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<float>>();
var rowsOut = new System.Collections.Generic.List<object>();

for (int seed = 1000; seed < 1200; seed++)
{
    requested++;
    var r = builder.Build(seed, true);
    if (!r.IsValid || r.Layout == null) { invalid++; continue; }
    if (!seen.Add(r.Seed)) { duplicates++; continue; }
    layouts++;
    var isl = r.Layout.Islands; var reefs = r.Layout.Reefs;
    var lh = isl.First(i => i.ArchetypeId == 1000);
    var lhDocks = dockWorld(lh);
    float far = 0f;
    for (int j = 0; j < isl.Count; j++)
    {
        var p = isl[j];
        var pd = dockWorld(p);
        if (p.ArchetypeId != 1000)
        {
            float best = float.MaxValue; UnityEngine.Vector2 ba = default, bb = default;
            foreach (var a in lhDocks) foreach (var b in pd) { float d = (a - b).magnitude; if (d < best) { best = d; ba = a; bb = b; } }
            dLhAll.Add(best); cLhAll.Add((p.Position - lh.Position).magnitude); far = UnityEngine.Mathf.Max(far, best);
            tN.Add(cruise(best, 8f, 4f)); tV.Add(cruise(best, 16f, 8f));
            string z = p.Zone.ToString();
            if (!byZone.ContainsKey(z)) byZone[z] = new System.Collections.Generic.List<float>();
            byZone[z].Add(best);
            if (!byType.ContainsKey(p.ArchetypeId)) byType[p.ArchetypeId] = new System.Collections.Generic.List<float>();
            byType[p.ArchetypeId].Add(best);
            legs++;
            bool hit = false;
            foreach (var rf in reefs) if (segDist(ba, bb, rf.Position) < rf.Radius + clearance) { hit = true; break; }
            if (!hit) for (int k = 0; k < isl.Count && !hit; k++)
            {
                if (k == j || isl[k].ArchetypeId == 1000) continue;
                if (segDist(ba, bb, isl[k].Position) < realR[isl[k].ArchetypeId] + clearance) hit = true;
            }
            if (hit) blocked++;
        }
        float nn = float.MaxValue;
        for (int k = 0; k < isl.Count; k++)
        {
            if (k == j) continue;
            foreach (var a in pd) foreach (var b in dockWorld(isl[k])) nn = UnityEngine.Mathf.Min(nn, (a - b).magnitude);
        }
        nnAll.Add(nn);
    }
    farthest.Add(far);
    rowsOut.Add(new { requestedSeed = seed, usedSeed = r.Seed, islands = isl.Count, reefs = reefs.Count, farthestDockM = far });
}

var summary = new
{
    note = "straight dock-to-dock model, no waves/turns/berthing; speeds 8 m/s a4 (normal), 16 m/s a8 (Navigator x2)",
    requested, invalid, duplicates, uniqueValidLayouts = layouts, legs, blockedStraightLegs = blocked,
    dockToDockFromLighthouseM = stats(dLhAll), centerToCenterFromLighthouseM = stats(cLhAll),
    nearestNeighborDockM = stats(nnAll), farthestIslandDockFromLighthouseM = stats(farthest),
    cruiseSecNormal = stats(tN), cruiseSecNavigator = stats(tV),
    byZone = byZone.ToDictionary(kv => kv.Key, kv => stats(kv.Value)),
    byArchetype = byType.ToDictionary(kv => kv.Key.ToString(), kv => stats(kv.Value))
};
var json = Newtonsoft.Json.JsonConvert.SerializeObject(new { summary, rows = rowsOut }, Newtonsoft.Json.Formatting.Indented);
System.IO.File.WriteAllText(@"C:\Users\inha\Documents\GitHub\TheSecretOfLightHouse\docs\강민서\2026-10-30\SeaS2\route-baseline.json", json, new System.Text.UTF8Encoding(false));
return Newtonsoft.Json.JsonConvert.SerializeObject(summary);
