if (UnityEngine.Application.isPlaying || Mirror.NetworkServer.active || Mirror.NetworkClient.active)
    throw new System.InvalidOperationException("Run only with the game stopped and disconnected.");
var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
System.Action<object,string,object> set = (o,n,v) => o.GetType().GetField(n,flags).SetValue(o,v);
System.Func<object,string,object> get = (o,n) => o.GetType().GetField(n,flags).GetValue(o);
System.Action<object,string> call = (o,n) => { try { o.GetType().GetMethod(n,flags).Invoke(o,null); } catch (System.Exception ex) { throw new System.Exception(o.GetType().Name + "." + n + ": " + ex.ToString()); } };
System.Action<bool,string> check = (ok,n) => { if (!ok) throw new System.Exception(n); };
var activeProperty = typeof(Mirror.NetworkServer).GetProperty("active");
var results = new System.Collections.Generic.List<string>();
var paths = new[] { "Assets/02.Prefabs/Player/Captain 1.prefab", "Assets/02.Prefabs/Player/Tech 1.prefab", "Assets/02.Prefabs/Player/Tanker 1.prefab", "Assets/02.Prefabs/Player/Farm 1.prefab" };
foreach (var path in paths)
{
    UnityEngine.GameObject root = null;
    UnityEngine.GameObject boatRoot = null;
    UnityEngine.GameObject helper = null;
    try
    {
        root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
        boatRoot = UnityEditor.PrefabUtility.LoadPrefabContents("Assets/02.Prefabs/Player/Boat.prefab");
        call(root.GetComponent<Mirror.NetworkIdentity>(),"InitializeNetworkBehaviours");
        call(boatRoot.GetComponent<Mirror.NetworkIdentity>(),"InitializeNetworkBehaviours");
        var player = root.GetComponent<PlayerController>();
        var boat = boatRoot.GetComponent<BoatController>();
        var recovery = root.GetComponent<PlayerWreckRecovery>();
        call(player,"Awake");
        call(boat,"Awake");
        call(recovery,"Awake");
        set(player,"_camera",null);
        set(player,"_animationDriver",null);
        set(boat,"_wreckEffectPrefab",null);
        var marker = root.AddComponent<UnityEngine.AudioSource>();
        marker.playOnAwake = false;
        set(player,"_wreckBuoyancy",marker);
        var playerIdentity = root.GetComponent<Mirror.NetworkIdentity>();
        typeof(Mirror.NetworkIdentity).GetProperty("isServer").SetValue(playerIdentity,true);
        typeof(Mirror.NetworkIdentity).GetProperty("isLocalPlayer").SetValue(playerIdentity,true);
        activeProperty.GetSetMethod(true).Invoke(null,new object[] { true });
        set(player,"_ownedBoat",boat);
        set(boat,"_owner",player);
        set(boat,"_isOccupied",true);
        call(player,"RefreshLocalControl");
        call(player,"RefreshState");
        check(!marker.enabled,"Normal Player buoyancy must be off");
        check(player.ApplyBoarding(boat),"Board normal boat");
        var body = root.GetComponent<UnityEngine.Rigidbody>();
        check(body.isKinematic && !body.detectCollisions,"Boarded body");
        set(boatRoot.GetComponent<BoatDurability>(),"_isWrecked",true);
        call(boat,"ApplyWreckPresentation");
        var buoyancy = boatRoot.GetComponent<Lighthouse.World.Ocean.BuoyancyBody>();
        check(buoyancy && !buoyancy.enabled && !buoyancy.IsSimulating,"Wreck boat buoyancy off");
        var wreckPosition = boatRoot.transform.position;
        check(recovery.TryBeginRecovery(wreckPosition),"Begin wreck recovery");
        check(player.IsDowned && !player.IsOnBoat && !boat.IsOccupied,"Detached and Downed");
        check(!body.isKinematic && body.detectCollisions && marker.enabled,"Local physics and optional buoyancy restored");
        check((UnityEngine.Vector3)get(recovery,"_wreckPosition") == wreckPosition,"Settlement origin preserved");
        check(!recovery.TryBeginRecovery(wreckPosition),"Duplicate wreck rejected");
        check(!player.ApplyBoarding(boat),"Wreck cannot board again");
        var playerPosition = root.transform.position;
        boatRoot.transform.position += UnityEngine.Vector3.right * 50f;
        call(player,"LateUpdate");
        check(root.transform.position == playerPosition,"No boat anchor following");
        body.linearVelocity = UnityEngine.Vector3.one * 4f;
        player.ApplyRecoveryPosition(boat,new UnityEngine.Vector3(10f,3f,20f));
        check(root.transform.position == new UnityEngine.Vector3(10f,3f,20f) && body.linearVelocity == UnityEngine.Vector3.zero,"Already detached recovery teleport");
        helper = new UnityEngine.GameObject("RecoveryFailureFixture");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(helper,root.scene);
        var spawner = helper.AddComponent<Lighthouse.Player.Boat.PlayerBoatSpawner>();
        set(spawner,"_playerRecoveryPoint",helper.transform);
        recovery.ConfigureRecovery(spawner,helper.transform,1);
        check(!recovery.TryBeginCompletion(WreckRecoveryResult.Rescued),"Missing connection recovery fails safely");
        check((bool)get(recovery,"_isCompletingRecovery"),"Failed completion remains resumable");
        set(recovery,"_nextCompletionRetryTime",0d);
        call(recovery,"Update");
        check((double)get(recovery,"_nextCompletionRetryTime") > 0d,"Failed completion scheduled for retry");

        var manager = helper.AddComponent<Lighthouse.Network.Session.Net.LighthouseNetworkManager>();
        helper.AddComponent<Mirror.NetworkIdentity>();
        var lobby = helper.AddComponent<Lighthouse.Network.Session.Net.LobbyPlayer>();
        set(manager,"_lobbyPlayerPrefab",lobby);
        set(manager,"_gameScene","Fixture");
        typeof(Mirror.NetworkManager).GetProperty("singleton").GetSetMethod(true).Invoke(null,new object[] { manager });
        set(manager,"_phase",Lighthouse.Network.Session.Rules.MatchPhase.Loading);
        call(player,"RefreshState");
        check(!marker.enabled,"Loading buoyancy off");
        set(manager,"_phase",Lighthouse.Network.Session.Rules.MatchPhase.InProgress);
        call(player,"RefreshState");
        check(marker.enabled,"In-progress buoyancy on");
        set(manager,"_phase",Lighthouse.Network.Session.Rules.MatchPhase.Ended);
        call(player,"RefreshState");
        check(!marker.enabled,"Ended buoyancy off");
        typeof(Mirror.NetworkManager).GetProperty("singleton").GetSetMethod(true).Invoke(null,new object[] { null });
        player.SetDowned(false);
        check(!marker.enabled,"Recovered buoyancy off");
        player.SetDowned(true);
        check(marker.enabled,"Detached wreck buoyancy on");
        typeof(Mirror.NetworkIdentity).GetProperty("isLocalPlayer").SetValue(playerIdentity,false);
        call(player,"RefreshLocalControl");
        check(body.isKinematic && !marker.enabled,"Remote Player does not simulate buoyancy");
        results.Add(path + ": 21 checks passed");
    }
    catch (System.Exception ex) { return new[] { path, ex.ToString() }; }
    finally
    {
        activeProperty.GetSetMethod(true).Invoke(null,new object[] { false });
        typeof(Mirror.NetworkManager).GetProperty("singleton").GetSetMethod(true).Invoke(null,new object[] { null });
        if (helper) UnityEngine.Object.DestroyImmediate(helper);
        if (root) UnityEditor.PrefabUtility.UnloadPrefabContents(root);
        if (boatRoot) UnityEditor.PrefabUtility.UnloadPrefabContents(boatRoot);
    }
}
return results.ToArray();
