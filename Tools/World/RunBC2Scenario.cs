if (!UnityEditor.EditorApplication.isPlaying || !Mirror.NetworkServer.active)
{
    throw new System.InvalidOperationException("BC2 scenario requires the dedicated BC2 host and seven spawned prefabs.");
}
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
var spin = UnityEngine.GameObject.Find("BC2_Spinocroc").GetComponent<Lighthouse.Monsters.Spinocroc.Server.SpinocrocPreparation>();
var spinWater = spin.GetComponent<Lighthouse.Monsters.AI.Server.MonsterWaterBody>();
var cycle = UnityEngine.GameObject.Find("BC2_MosaWhale").GetComponent<Lighthouse.Monsters.MosaWhale.Server.MosaWhaleCycle>();
var movement = cycle.GetComponent<Lighthouse.Monsters.MosaWhale.Server.MosaWhaleMovement>();
var targeting = cycle.GetComponent<Lighthouse.Monsters.MosaWhale.Server.MosaWhaleTargeting>();
var bodies = UnityEngine.Object.FindObjectsByType<Lighthouse.Monsters.AI.Server.MonsterWaterBody>(UnityEngine.FindObjectsSortMode.None);
var samples = new System.Collections.Generic.List<object>();
var events = new System.Collections.Generic.List<string>();
var state = new System.Collections.Generic.Dictionary<string, object>();
state["samples"] = samples;
state["events"] = events;
state["done"] = false;
System.AppDomain.CurrentDomain.SetData("BC2.Scenario", state);
System.Action<object, string, object> set = (obj, field, value) => obj.GetType().GetField(field, flags).SetValue(obj, value);
System.Action<Lighthouse.Monsters.MosaWhale.Rules.MosaWhalePhase, float> phase = (value, duration) =>
{
    typeof(Lighthouse.Monsters.MosaWhale.Server.MosaWhaleCycle).GetMethod("SetPhase", flags).Invoke(cycle, new object[] { value, duration });
    events.Add("Mosa " + value + " t=" + UnityEngine.Time.time);
};
UnityEngine.Vector3 spinOrigin = spinWater.Body.position;
set(spin, "_returnDepth", spinWater.Buoyancy.TargetDepth);
set(spin, "_ownsMotion", true);
set(spin, "_savedConstraints", spinWater.Body.constraints);
set(spin, "_reappearPosition", spinWater.Body.position + new UnityEngine.Vector3(20f, 0f, 0f));
spin.GetComponent<Lighthouse.Monsters.AI.Server.MonsterMotion>().SetMovementLocked(true);
spinWater.Buoyancy.SetTargetDepth(spin.Settings.DiveDepth);
typeof(Lighthouse.Monsters.Spinocroc.Server.SpinocrocPreparation).GetMethod("SetPhase", flags).Invoke(spin, new object[] { Lighthouse.Monsters.Spinocroc.Rules.SpinocrocPhase.Diving });
set(cycle, "_isRunning", true);
set(targeting, "_lastTargetPosition", cycle.transform.position);
phase(Lighthouse.Monsters.MosaWhale.Rules.MosaWhalePhase.Diving, cycle.Settings.DiveDuration);
double start = UnityEditor.EditorApplication.timeSinceStartup;
double nextSample = start;
int stage = 0;
bool spinDone = false;
bool previewLoop = UnityEditor.SessionState.GetBool("BC2.PreviewLoop", false);
double nextStart = 0;
UnityEditor.EditorApplication.CallbackFunction tick = null;
tick = () =>
{
    if (!UnityEditor.EditorApplication.isPlaying || spin == null || cycle == null)
    {
        state["aborted"] = true;
        state["done"] = true;
        UnityEditor.EditorApplication.update -= tick;
        return;
    }
    double now = UnityEditor.EditorApplication.timeSinceStartup;
    if (nextStart > 0)
    {
        if (now < nextStart)
        {
            return;
        }
        samples.Clear();
        events.Clear();
        stage = 0;
        spinDone = false;
        start = now;
        nextSample = now;
        nextStart = 0;
        state["done"] = false;
        spinWater.Body.position = spinOrigin;
        spinWater.Body.linearVelocity = UnityEngine.Vector3.zero;
        spinWater.Buoyancy.InvalidateSamples();
        set(spin, "_returnDepth", spinWater.Buoyancy.TargetDepth);
        set(spin, "_ownsMotion", true);
        set(spin, "_savedConstraints", spinWater.Body.constraints);
        set(spin, "_reappearPosition", spinWater.Body.position + new UnityEngine.Vector3(20f, 0f, 0f));
        spin.GetComponent<Lighthouse.Monsters.AI.Server.MonsterMotion>().SetMovementLocked(true);
        spinWater.Buoyancy.SetTargetDepth(spin.Settings.DiveDepth);
        typeof(Lighthouse.Monsters.Spinocroc.Server.SpinocrocPreparation).GetMethod("SetPhase", flags).Invoke(spin, new object[] { Lighthouse.Monsters.Spinocroc.Rules.SpinocrocPhase.Diving });
        set(cycle, "_isRunning", true);
        set(targeting, "_lastTargetPosition", cycle.transform.position);
        phase(Lighthouse.Monsters.MosaWhale.Rules.MosaWhalePhase.Diving, cycle.Settings.DiveDuration);
    }
    if (now >= nextSample)
    {
        nextSample = now + 0.1;
        foreach (var water in bodies)
        {
            var reading = water.Buoyancy.Reading;
            samples.Add(new { name = water.name, t = UnityEngine.Time.time, y = water.Body.position.y,
                velocity = water.Body.linearVelocity.y, depth = reading.MeanDepth, target = water.Buoyancy.TargetDepth,
                valid = reading.IsValid, fraction = reading.SubmergedFraction, gravity = water.Body.useGravity,
                simulating = water.Buoyancy.IsSimulating, lease = water.Buoyancy.HasLease,
                spin = spin.Phase.ToString(), mosa = cycle.Phase.ToString() });
        }
    }
    if (!spinDone && spin.IsReadyForCharge)
    {
        events.Add("Spin ready t=" + UnityEngine.Time.time + " x=" + spin.transform.position.x);
        spin.Cancel();
        spinDone = true;
    }
    double elapsed = Mirror.NetworkTime.time - cycle.PhaseStartedAt;
    if (stage == 0 && elapsed >= cycle.PhaseDuration && movement.CanLeaveDepthPhase)
    {
        targeting.LockAttackPosition();
        phase(Lighthouse.Monsters.MosaWhale.Rules.MosaWhalePhase.Telegraph, cycle.Settings.TelegraphDuration);
        stage++;
    }
    else if (stage == 1 && elapsed >= cycle.PhaseDuration && movement.CanLeaveDepthPhase)
    {
        set(cycle, "_breachStartedAt", Mirror.NetworkTime.time);
        set(cycle, "_breachDuration", cycle.Settings.BreachDuration);
        phase(Lighthouse.Monsters.MosaWhale.Rules.MosaWhalePhase.Rising, cycle.Settings.RiseDuration);
        stage++;
    }
    else if (stage == 2 && elapsed >= cycle.PhaseDuration)
    {
        phase(Lighthouse.Monsters.MosaWhale.Rules.MosaWhalePhase.Apex, cycle.Settings.ApexDuration);
        stage++;
    }
    else if (stage == 3 && elapsed >= cycle.PhaseDuration)
    {
        phase(Lighthouse.Monsters.MosaWhale.Rules.MosaWhalePhase.Reentering, cycle.Settings.ReentryDuration);
        stage++;
    }
    else if (stage == 4 && elapsed >= cycle.PhaseDuration)
    {
        phase(Lighthouse.Monsters.MosaWhale.Rules.MosaWhalePhase.Recovering, cycle.Settings.RecoveryDuration);
        stage++;
    }
    else if (stage == 5 && elapsed >= cycle.PhaseDuration && movement.CanLeaveDepthPhase)
    {
        events.Add("Mosa recovery reached t=" + UnityEngine.Time.time);
        cycle.EndCycle();
        stage++;
    }
    if ((stage == 6 && spinDone) || now - start > 75)
    {
        state["stage"] = stage;
        state["spinDone"] = spinDone;
        state["done"] = true;
        state["timedOut"] = now - start > 75;
        if (previewLoop)
        {
            nextStart = now + 4;
        }
        else
        {
            UnityEditor.EditorApplication.update -= tick;
        }
    }
};
UnityEditor.EditorApplication.update += tick;
state["callback"] = tick;
return new { started = true, note = "State entry is injected; real server physics and production phase handlers execute. Target selection/BT/damage are not validated." };
