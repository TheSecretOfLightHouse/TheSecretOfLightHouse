using Lighthouse.Network.Connections.Net;
using Lighthouse.Network.Session.Net;
using Mirror;
using UnityEngine;

namespace Lighthouse.Dev.Network
{
    // JH01 test aid: each local player sends its own id and a forged id once, so every client log shows accept/reject.
    [RequireComponent(typeof(NetworkActor))]
    public sealed class ActorIdProbe : NetworkBehaviour
    {
        private const uint ForgedOffset = 100;

        private NetworkActor _actor;

        private void Awake()
        {
            _actor = GetComponent<NetworkActor>();
        }

        public override void OnStartLocalPlayer()
        {
            CmdProbe(_actor.ActorId);
            CmdProbe(_actor.ActorId + ForgedOffset);
        }

        [Command]
        private void CmdProbe(uint claimedActorId)
        {
            LighthouseNetworkManager manager = LighthouseNetworkManager.Instance;
            bool isAccepted = manager && manager.Connections.IsSender(connectionToClient, claimedActorId);

            Debug.Log($"[{nameof(ActorIdProbe)}] server connId={connectionToClient.connectionId} claimed={claimedActorId} accepted={isAccepted}");
            TargetProbeResult(claimedActorId, isAccepted);
        }

        [TargetRpc]
        private void TargetProbeResult(uint claimedActorId, bool isAccepted)
        {
            Debug.Log($"[{nameof(ActorIdProbe)}] client actor={_actor.ActorId} claimed={claimedActorId} accepted={isAccepted}");
        }
    }
}
