using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lighthouse.World.Query.Rules
{
    public static class WorldServices
    {
        private static readonly Dictionary<Type, object> Services = new Dictionary<Type, object>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetServices()
        {
            Services.Clear();
        }

        public static void Register<T>(T service) where T : class
        {
            if (IsMissing(service))
            {
                Debug.LogError($"[{nameof(WorldServices)}] Cannot register a null {typeof(T).Name}.");
                return;
            }

            if (Services.TryGetValue(typeof(T), out object current)
                && !IsMissing(current)
                && !ReferenceEquals(current, service))
            {
                Debug.LogError($"[{nameof(WorldServices)}] {typeof(T).Name} is already registered.");
                return;
            }

            Services[typeof(T)] = service;
        }

        public static bool Unregister<T>(T service) where T : class
        {
            if (!Services.TryGetValue(typeof(T), out object current) || !ReferenceEquals(current, service))
            {
                return false;
            }

            return Services.Remove(typeof(T));
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out object found) && !IsMissing(found) && found is T typed)
            {
                service = typed;
                return true;
            }

            service = null;
            return false;
        }

        private static bool IsMissing(object service)
        {
            if (service is UnityEngine.Object unityObject)
            {
                return unityObject == null;
            }

            return service == null;
        }
    }
}
