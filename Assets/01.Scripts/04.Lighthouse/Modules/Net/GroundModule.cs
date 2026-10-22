using System;
using Lighthouse.Workbench.Net;
using Lighthouse.World.Debris.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Modules.Net
{
    // A carried module left on a shore by a player who was not rescued (2026-10-22, T34). Anyone on foot may pick it up,
    // and it rots away once World's debris lifetime runs out. Which module it is and where it came from stay on the server.
    // One instance per drop and never reused, so its generation only has to differ from every other drop's.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkIdentity))]
    public sealed class GroundModule : NetworkBehaviour, IExpirableDrop
    {
        // Server-only.
        private uint _moduleId;
        private WorkbenchNet _source;
        private ulong _generation;
        private bool _isReleased;

        public ulong Generation => _generation;
        // Server-only.
        public uint ModuleId => _moduleId;
        // Server-only. The bench it was carried from; picking it up holds it against that bench again.
        public WorkbenchNet Source => _source;
        // Server-only. False once picked up, rotted away or torn down.
        public bool IsAvailable => !_isReleased;

        // Raised once when it stops being available, however that happened. World's tracker listens to drop it.
        public event Action<IExpirableDrop> Released;
        // Server-only. Raised once when the lifetime ran out before anyone picked it up.
        public event Action<GroundModule> Expired;

        public override void OnStopServer()
        {
            // Torn down with the match: the tracker still has to let go of it.
            Release();
            Expired = null;
            base.OnStopServer();
        }

        [Server]
        public void Initialize(uint moduleId, WorkbenchNet source, ulong generation)
        {
            _moduleId = moduleId;
            _source = source;
            _generation = generation;
            _isReleased = false;
        }

        // Requests are handled one at a time on the server, so whichever of pick-up and expiry comes first wins.
        public bool TryExpire(ulong generation)
        {
            if (!isServer || _isReleased || generation != _generation)
            {
                return false;
            }

            Release();
            Expired?.Invoke(this);
            return true;
        }

        [Server]
        public bool TryTake()
        {
            if (_isReleased)
            {
                return false;
            }

            Release();
            return true;
        }

        private void Release()
        {
            if (_isReleased)
            {
                return;
            }

            _isReleased = true;
            Action<IExpirableDrop> released = Released;
            Released = null;
            released?.Invoke(this);
        }
    }
}
