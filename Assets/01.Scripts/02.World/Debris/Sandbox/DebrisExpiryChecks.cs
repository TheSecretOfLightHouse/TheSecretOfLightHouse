using System;
using System.Collections.Generic;
using Lighthouse.World.Debris.Rules;
using UnityEngine;

namespace Lighthouse.World.Debris.Sandbox
{
    public static class DebrisExpiryChecks
    {
        public static bool RunAll()
        {
            bool isPassed = RunOrdering();
            isPassed &= RunRemove();
            isPassed &= RunConfig();
            return isPassed;
        }

        private static bool RunOrdering()
        {
            var list = new DebrisExpiryList();
            var results = new List<DebrisExpiryEntry>();
            var a = new FakeDrop();
            var b = new FakeDrop();
            var c = new FakeDrop();
            var d = new FakeDrop();

            list.Add(a, 1UL, 10.0);
            list.Add(b, 2UL, 5.0);
            list.Add(c, 3UL, 20.0);

            int first = list.CollectExpired(7.0, results);
            bool isPassed = first == 1
                && results.Count == 1
                && ReferenceEquals(results[0].Drop, b)
                && results[0].Generation == 2UL
                && list.Count == 2;

            list.Add(d, 4UL, 6.0);
            results.Clear();
            int second = list.CollectExpired(10.0, results);
            isPassed &= second == 2
                && list.Count == 1
                && Contains(results, a)
                && Contains(results, d);

            results.Clear();
            isPassed &= list.CollectExpired(5.0, results) == 0 && list.Count == 1;

            Report("Ordering", isPassed, "shortened lifetime/reverse scan/generation kept");
            return isPassed;
        }

        private static bool RunRemove()
        {
            var list = new DebrisExpiryList();
            var a = new FakeDrop();

            list.Add(a, 1UL, 5.0);
            list.Add(a, 2UL, 9.0);

            bool isPassed = list.Remove(a) && list.Count == 0 && !list.Remove(a);

            Report("Remove", isPassed, "released drop removes all entries");
            return isPassed;
        }

        private static bool RunConfig()
        {
            DebrisConfig config = ScriptableObject.CreateInstance<DebrisConfig>();
            bool isPassed =
                config.TryGetLifetime(DebrisKind.Resource, out float resourceSeconds) && resourceSeconds > 0f
                && config.TryGetLifetime(DebrisKind.Module, out float moduleSeconds) && moduleSeconds > 0f;

            UnityEngine.Object.DestroyImmediate(config);

            Report("Config", isPassed, "default lifetimes enabled");
            return isPassed;
        }

        private static bool Contains(List<DebrisExpiryEntry> entries, IExpirableDrop drop)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (ReferenceEquals(entries[i].Drop, drop))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Report(string name, bool isPassed, string detail)
        {
            Debug.Log($"[{nameof(DebrisExpiryChecks)}] {name} {(isPassed ? "PASS" : "FAIL")} {detail}");
        }

        private sealed class FakeDrop : IExpirableDrop
        {
            public ulong Generation => 0UL;

            public event Action<IExpirableDrop> Released
            {
                add { }
                remove { }
            }

            public bool TryExpire(ulong generation)
            {
                return false;
            }
        }
    }
}
