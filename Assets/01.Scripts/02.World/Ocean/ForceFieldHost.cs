using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean
{
    [DefaultExecutionOrder(-1000)]
    public class ForceFieldHost : MonoBehaviour
    {
        private ForceFieldRegistry _registry;

        private void Awake()
        {
            _registry = new ForceFieldRegistry();
            WorldServices.Register(_registry);
            WorldServices.Register<IForceFieldProvider>(_registry);
        }

        private void OnDestroy()
        {
            WorldServices.Unregister(_registry);
            WorldServices.Unregister<IForceFieldProvider>(_registry);
        }
    }
}
