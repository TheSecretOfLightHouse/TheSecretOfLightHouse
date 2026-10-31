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
        public static event System.Action<MonsterBoatTarget> TargetRegistered;
        public static event System.Action<MonsterBoatTarget> TargetUnregistered;

        public abstract bool IsEligible { get; }
        public abstract float HitRadius { get; }
        public bool IsInSafeZone => WorldServices.TryGet<IWorldQuery>(out IWorldQuery world)
            && world.IsSafeZone(transform.position);

        public abstract bool TryApplyDamage(float damage);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTargets()
        {
            RegisteredTargets.Clear();
            TargetRegistered = null;
            TargetUnregistered = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            RegisterTarget();
        }

        public override void OnStopServer()
        {
            UnregisterTarget();
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
            UnregisterTarget();
        }

        protected virtual void OnDestroy()
        {
            UnregisterTarget();
        }

        private void RegisterTarget()
        {
            if (isActiveAndEnabled && !RegisteredTargets.Contains(this))
            {
                RegisteredTargets.Add(this);
                TargetRegistered?.Invoke(this);
            }
        }

        private void UnregisterTarget()
        {
            if (RegisteredTargets.Remove(this))
            {
                TargetUnregistered?.Invoke(this);
            }
        }
    }
}
