if (!UnityEditor.EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MonsterBuoyancyChecks")
{
    throw new System.InvalidOperationException("Open the BC2 check scene and enter Play before starting the host.");
}
if (Mirror.NetworkServer.active)
{
    throw new System.InvalidOperationException("A server is already active; restart this check scene before repeating.");
}
UnityEngine.Application.runInBackground = true;
Mirror.NetworkManager.singleton.StartHost();
int index = 0;
foreach (string name in new[] { "Angler", "BlueMarline", "BlueShark", "BubbleTurtle", "Manta", "Spinocroc", "MosaWhale" })
{
    var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/02.Prefabs/Monsters/" + name + ".prefab");
    var body = UnityEngine.Object.Instantiate(prefab, new UnityEngine.Vector3(index++ * 45f, 0f, 0f), UnityEngine.Quaternion.identity);
    body.name = "BC2_" + name;
    Mirror.NetworkServer.Spawn(body);
    body.GetComponent<Lighthouse.Monsters.AI.Net.MonsterBase>().StopBehavior();
}
var host = UnityEngine.Object.FindFirstObjectByType<Lighthouse.World.Ocean.Rules.WaterSamplerHost>();
host.Inputs.TrySetWaves(new[] {
    new Lighthouse.World.Ocean.Rules.WaveParams { Amplitude = 3f, Wavelength = 30f, Direction = new UnityEngine.Vector2(1f, 0f), Steepness = 0.3f, Speed = 3f },
    new Lighthouse.World.Ocean.Rules.WaveParams { Amplitude = 1.5f, Wavelength = 14f, Direction = new UnityEngine.Vector2(0.6f, 0.8f), Steepness = 0.2f, Speed = 2.2f }
});
Lighthouse.World.Query.Rules.WorldServices.Register<Lighthouse.World.Ocean.Rules.IForceFieldProvider>(new Lighthouse.World.Ocean.Rules.ForceFieldRegistry());
UnityEditor.SessionState.SetBool("BC2.PreviewLoop", false);
return new { server = Mirror.NetworkServer.active, spawned = index };
