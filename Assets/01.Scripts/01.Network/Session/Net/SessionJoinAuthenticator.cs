using Mirror;
using UnityEngine;

namespace Lighthouse.Network.Session.Net
{
    // Rejects joins after the lobby has handed off, before Mirror sends the scene message.
    // Rejecting any later starts the game scene load on the client, and a disconnect mid-load strands it in that scene.
    [DisallowMultipleComponent]
    public sealed class SessionJoinAuthenticator : NetworkAuthenticator
    {
        [SerializeField]
        private LighthouseNetworkManager _manager;

        private void Awake()
        {
            if (!_manager)
            {
                _manager = GetComponent<LighthouseNetworkManager>();
            }
        }

        public override void OnServerAuthenticate(NetworkConnectionToClient conn)
        {
            if (_manager && !_manager.CanAcceptJoin)
            {
                _manager.RejectJoin(conn);
                return;
            }

            ServerAccept(conn);
        }

        // No credentials; the server decides from the session phase alone.
        public override void OnClientAuthenticate()
        {
            ClientAccept();
        }
    }
}
