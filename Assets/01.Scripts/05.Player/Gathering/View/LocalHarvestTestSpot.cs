using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(HarvestTarget))]
public sealed class LocalHarvestTestSpot : MonoBehaviour
{
    [SerializeField, Min(1)]
    private int _spotId = 1;

    public int SpotId => _spotId;
}
