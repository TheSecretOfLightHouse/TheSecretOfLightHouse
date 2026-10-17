using System.Collections.Generic;
using Lighthouse.Monsters.AI.Net;
using UnityEngine;

namespace Lighthouse.Monsters.Spawning.Server
{
    internal static class MonsterSpawnCollisionRegistry
    {
        private static readonly Dictionary<Collider, MonsterBase> Owners = new Dictionary<Collider, MonsterBase>();

        internal static void Register(MonsterBase monster, Collider[] colliders)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    Owners[colliders[i]] = monster;
                }
            }
        }

        internal static void Unregister(Collider[] colliders)
        {
            if (colliders == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (!ReferenceEquals(colliders[i], null))
                {
                    Owners.Remove(colliders[i]);
                }
            }
        }

        internal static bool IsMonster(Collider collider)
        {
            return collider != null && Owners.TryGetValue(collider, out MonsterBase owner) && owner != null;
        }
    }
}
