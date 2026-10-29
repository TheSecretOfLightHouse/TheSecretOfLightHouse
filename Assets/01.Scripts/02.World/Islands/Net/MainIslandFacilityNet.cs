using Lighthouse.Tower.Net;
using Lighthouse.Workbench.Net;
using Lighthouse.World.Query.Rules;
using Mirror;

namespace Lighthouse.World.Islands.Net
{
    // Network facilities announce themselves independently of local terrain construction.
    public sealed class MainIslandFacilityNet : NetworkBehaviour
    {
        private WorkbenchNet _workbench;
        private TowerNet _tower;

        private void Awake()
        {
            _workbench = GetComponent<WorkbenchNet>();
            _tower = GetComponent<TowerNet>();
        }

        public override void OnStartServer()
        {
            Register();
        }

        public override void OnStartClient()
        {
            Register();
        }

        public override void OnStopServer()
        {
            Unregister();
        }

        public override void OnStopClient()
        {
            Unregister();
        }

        private void Register()
        {
            if (_workbench)
            {
                WorldServices.Register<WorkbenchNet>(_workbench);
            }

            if (_tower)
            {
                WorldServices.Register<TowerNet>(_tower);
            }
        }

        private void Unregister()
        {
            if (_workbench)
            {
                WorldServices.Unregister<WorkbenchNet>(_workbench);
            }

            if (_tower)
            {
                WorldServices.Unregister<TowerNet>(_tower);
            }
        }
    }
}
