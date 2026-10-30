// Run through Unity eval_file. This audit does not mutate assets or runtime objects.
var rows = new System.Collections.Generic.List<object>();
foreach (string name in new[] { "Angler", "BlueMarline", "BlueShark", "BubbleTurtle", "Manta", "Spinocroc", "MosaWhale" })
{
    string path = "Assets/02.Prefabs/Monsters/" + name + ".prefab";
    var root = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
    if (root == null)
    {
        throw new System.InvalidOperationException("Missing BC2 prefab: " + path);
    }
    var body = root.GetComponent<UnityEngine.Rigidbody>();
    if (body == null)
    {
        throw new System.InvalidOperationException("Missing Rigidbody: " + path);
    }
    var colliders = new System.Collections.Generic.List<object>();
    foreach (var collider in root.GetComponentsInChildren<UnityEngine.Collider>(true))
    {
        if (collider.isTrigger)
        {
            continue;
        }
        string shape = collider.GetType().Name;
        if (collider is UnityEngine.CapsuleCollider capsule)
        {
            shape += " center=" + capsule.center + " height=" + capsule.height
                + " radius=" + capsule.radius + " direction=" + capsule.direction;
        }
        else if (collider is UnityEngine.BoxCollider box)
        {
            shape += " center=" + box.center + " size=" + box.size;
        }
        colliders.Add(new { shape, scale = collider.transform.lossyScale.ToString() });
    }
    rows.Add(new
    {
        path,
        gravity = body.useGravity,
        mass = body.mass,
        constraints = body.constraints.ToString(),
        kinematic = body.isKinematic,
        buoyancy = root.GetComponent<Lighthouse.World.Ocean.BuoyancyObject>() != null,
        waterBody = root.GetComponent<Lighthouse.World.Ocean.WaterBody>() != null,
        drift = root.GetComponent<Lighthouse.World.Ocean.WaveDriftBody>() != null,
        colliders
    });
}
return new { playing = UnityEditor.EditorApplication.isPlaying, frame = UnityEngine.Time.frameCount, rows };
