using Lighthouse.World.Ocean.Rules;
using Lighthouse.World.Query.Rules;
using UnityEngine;

namespace Lighthouse.World.Ocean.Sandbox
{
    public class HazardLogProbe : MonoBehaviour
    {
        private IHazardEvents _events;

        private void OnEnable()
        {
            if (!WorldServices.TryGet(out _events))
            {
                Debug.LogError($"[{nameof(HazardLogProbe)}] IHazardEvents is not registered. Add MockServicesInstaller to the scene.");
                enabled = false;
                return;
            }

            _events.HazardOccurred += HandleHazardOccurred;
        }

        private void OnDisable()
        {
            if (_events != null)
            {
                _events.HazardOccurred -= HandleHazardOccurred;
                _events = null;
            }
        }

        private void HandleHazardOccurred(HazardHit hit)
        {
            Debug.Log($"[{nameof(HazardLogProbe)}] {hit.Type} strength={hit.Strength:F2} ship={hit.ShipId} at {hit.Position}");
        }
    }
}
