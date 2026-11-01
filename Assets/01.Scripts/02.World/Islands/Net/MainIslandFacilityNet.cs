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
        private TowerMatchNet _match;

        private void Awake()
        {
            _workbench = GetComponent<WorkbenchNet>();
            _tower = GetComponent<TowerNet>();
            _match = GetComponent<TowerMatchNet>();
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
            if (_match)
            {
                WorldServices.Register<TowerMatchNet>(_match);
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
            if (_match)
            {
                WorldServices.Unregister<TowerMatchNet>(_match);
            }
        }
    }
}
