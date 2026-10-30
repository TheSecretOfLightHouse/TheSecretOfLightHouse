// Runtime-only MainGame observation. Production MonsterMotion and water contact grid remain in control.
if (!UnityEditor.EditorApplication.isPlaying)
{
    throw new System.InvalidOperationException("Enter MainGame and spawn the BC2 preview first.");
}
var old = System.AppDomain.CurrentDomain.GetData("BC2.MovementPreview") as UnityEditor.EditorApplication.CallbackFunction;
if (old != null)
{
    UnityEditor.EditorApplication.update -= old;
}
var state = System.AppDomain.CurrentDomain.GetData("BC2.Scenario") as System.Collections.Generic.Dictionary<string, object>;
if (state != null && state.TryGetValue("callback", out object callback))
{
    UnityEditor.EditorApplication.update -= (UnityEditor.EditorApplication.CallbackFunction)callback;
}
var previousCamera = System.AppDomain.CurrentDomain.GetData("BC2.PreviewCamera") as UnityEditor.EditorApplication.CallbackFunction;
if (previousCamera != null)
{
    UnityEditor.EditorApplication.update -= previousCamera;
}
UnityEditor.SessionState.SetBool("BC2.PreviewLoop", false);
var shark = UnityEngine.GameObject.Find("BC2_BlueShark");
var motion = shark.GetComponent<Lighthouse.Monsters.AI.Server.MonsterMotion>();
var body = shark.GetComponent<UnityEngine.Rigidbody>();
var effects = UnityEngine.Object.FindFirstObjectByType<Lighthouse.World.Ocean.View.WaterContactEffectsView>();
var focus = new UnityEditor.SerializedObject(effects);
focus.FindProperty("_focus").objectReferenceValue = shark.transform;
focus.ApplyModifiedPropertiesWithoutUndo();
var camera = UnityEngine.GameObject.Find("BC2 Observation Camera").GetComponent<UnityEngine.Camera>();
camera.fieldOfView = 50f;
var route = new[] { new UnityEngine.Vector3(20f, 0f, -150f), new UnityEngine.Vector3(20f, 0f, -180f),
    new UnityEngine.Vector3(-20f, 0f, -180f), new UnityEngine.Vector3(-20f, 0f, -150f) };
int leg = 0;
float speed = shark.GetComponent<Lighthouse.Monsters.AI.Net.MonsterBase>().Profile.PatrolSpeed;
motion.SetMovementLocked(false);
motion.BeginSmoothMovementLeg(route[leg]);
UnityEditor.EditorApplication.CallbackFunction tick = null;
tick = () =>
{
    if (!UnityEditor.EditorApplication.isPlaying || shark == null || camera == null)
    {
        UnityEditor.EditorApplication.update -= tick;
        return;
    }
    if (motion.StepSmoothMovement(speed))
    {
        leg = (leg + 1) % route.Length;
        motion.BeginSmoothMovementLeg(route[leg]);
    }
    UnityEngine.Vector3 center = body.position;
    UnityEngine.Vector3 desired = center - shark.transform.forward * 15f
        + shark.transform.right * 7f + UnityEngine.Vector3.up * 7f;
    camera.transform.position = UnityEngine.Vector3.Lerp(camera.transform.position, desired, 0.08f);
    camera.transform.LookAt(center + shark.transform.forward * 2f);
};
UnityEditor.EditorApplication.update += tick;
System.AppDomain.CurrentDomain.SetData("BC2.MovementPreview", tick);
return new { speed, camera = camera.name, focus = shark.name };
