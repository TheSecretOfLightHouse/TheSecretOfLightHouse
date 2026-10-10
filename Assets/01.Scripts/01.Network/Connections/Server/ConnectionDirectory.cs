using System.Collections.Generic;
using Mirror;

namespace Lighthouse.Network.Connections.Server
{
    // Server-only map between connections and actor ids.
    // ActorId is kept separate from connectionId so a reconnect policy (JH07) can later rebind a person to a new connection.
    public sealed class ConnectionDirectory
    {
        public const uint InvalidActorId = 0;

        private readonly Dictionary<int, uint> _actorIdByConnectionId = new Dictionary<int, uint>();
        private readonly Dictionary<uint, NetworkConnectionToClient> _connectionByActorId = new Dictionary<uint, NetworkConnectionToClient>();

        private uint _nextActorId = 1;

        public int Count => _connectionByActorId.Count;

        public uint Register(NetworkConnectionToClient conn)
        {
            if (_actorIdByConnectionId.TryGetValue(conn.connectionId, out uint existingActorId))
            {
                return existingActorId;
            }

            uint actorId = _nextActorId++;

            _actorIdByConnectionId.Add(conn.connectionId, actorId);
            _connectionByActorId.Add(actorId, conn);

            return actorId;
        }

        public bool Unregister(NetworkConnectionToClient conn, out uint actorId)
        {
            if (!_actorIdByConnectionId.Remove(conn.connectionId, out actorId))
            {
                return false;
            }

            _connectionByActorId.Remove(actorId);
            return true;
        }

        public bool TryGetActorId(NetworkConnectionToClient conn, out uint actorId)
        {
            if (conn == null)
            {
                actorId = InvalidActorId;
                return false;
            }

            return _actorIdByConnectionId.TryGetValue(conn.connectionId, out actorId);
        }

        public bool TryGetConnection(uint actorId, out NetworkConnectionToClient conn)
        {
            return _connectionByActorId.TryGetValue(actorId, out conn);
        }

        // Requests must never be trusted to name their own actor; the claimed id has to match the sending connection.
        public bool IsSender(NetworkConnectionToClient sender, uint claimedActorId)
        {
            return claimedActorId != InvalidActorId
                && TryGetActorId(sender, out uint actorId)
                && actorId == claimedActorId;
        }

        public void Clear()
        {
            _actorIdByConnectionId.Clear();
            _connectionByActorId.Clear();
            _nextActorId = 1;
        }
    }
}
