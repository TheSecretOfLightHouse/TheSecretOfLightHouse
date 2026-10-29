using System.Collections.Generic;
using Lighthouse.World.Query.Rules;
using Mirror;
using UnityEngine;

namespace Lighthouse.Monsters.AI.Server
{
    public abstract class MonsterBoatTarget : NetworkBehaviour
    {
        private static readonly List<MonsterBoatTarget> RegisteredTargets = new List<MonsterBoatTarget>();
        public static IReadOnlyList<MonsterBoatTarget> Targets => RegisteredTargets;

        public abstract bool IsEligible { get; }
        public abstract float HitRadius { get; }
        public bool IsInSafeZone => WorldServices.TryGet<IWorldQuery>(out IWorldQuery world)
            && world.IsSafeZone(transform.position);

        public abstract bool TryApplyDamage(float damage);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTargets()
        {
            RegisteredTargets.Clear();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            RegisterTarget();
        }

        public override void OnStopServer()
        {
            RegisteredTargets.Remove(this);
            base.OnStopServer();
        }

        protected virtual void OnEnable()
        {
            if (isServer)
            {
                RegisterTarget();
            }
        }

        protected virtual void OnDisable()
        {
            RegisteredTargets.Remove(this);
        }

        protected virtual void OnDestroy()
        {
            RegisteredTargets.Remove(this);
        }

        private void RegisterTarget()
        {
            if (isActiveAndEnabled && !RegisteredTargets.Contains(this))
            {
                RegisteredTargets.Add(this);
            }
        }
    }
}
