using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public class ManualWindInstaller : MonoBehaviour
    {
        [SerializeField] private ManualWindSource _source;
        [SerializeField] private float _forceCoefficient = 1f;

        private ForceFieldRegistry _registry;
        private WindField _field;

        private void OnEnable()
        {
            if (_source == null || !WorldServices.TryGet(out _registry))
            {
                Debug.LogError($"[{nameof(ManualWindInstaller)}] ManualWindSource and a registered ForceFieldRegistry are required.");
                enabled = false;
                return;
            }

            _field = new WindField(_source, _forceCoefficient);
            _registry.Register(_field, false);
        }

        private void OnDisable()
        {
            if (_registry == null || _field == null)
            {
                return;
            }

            _registry.Unregister(_field);
            _field = null;
        }
    }
}
