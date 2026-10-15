using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Targets.Net;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerController))]
public sealed class PlayerHarvestProbe : MonoBehaviour
{
    [SerializeField]
    private PlayerController _player;

    [SerializeField, Min(0.1f)]
    private float _probeDistance = 20f;

    [SerializeField, Min(0.1f)]
    private float _reach = 3f;

    [SerializeField]
    private LayerMask _probeMask = ~0;

    private readonly RaycastHit[] _hits = new RaycastHit[32];
    [SerializeField]
    private InteractionRelay _relay;


    private void Awake()
    {
        if (!_player)
        {
            _player = GetComponent<PlayerController>();
        }

        if (!_relay)
        {
            _relay = GetComponent<InteractionRelay>();
        }
    }

    private void OnEnable()
    {
        if (_player)
        {
            _player.HarvestRequested += HandleHarvestRequested;
        }
    }

    private void OnDisable()
    {
        if (_player)
        {
            _player.HarvestRequested -= HandleHarvestRequested;
        }
    }

    private void HandleHarvestRequested(PlayerController player)
    {
        if (!player || !player.isActiveAndEnabled || player.IsDowned || player.IsOnBoat)
        {
            return;
        }

        ProbeTarget(player.transform);
    }

    private void ProbeTarget(Transform actor)
    {
        Camera mainCamera = Camera.main;

        if (!mainCamera || Mouse.current == null)
        {
            return;
        }

        Vector2 pointerPosition = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(pointerPosition);

        int hitCount = Physics.RaycastNonAlloc(
            ray,
            _hits,
            _probeDistance,
            _probeMask,
            QueryTriggerInteraction.Ignore);

        if (hitCount == _hits.Length)
        {
            return;
        }

        int nearestIndex = -1;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _hits[i];

            if (hit.collider.transform.IsChildOf(actor))
            {
                continue;
            }

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestIndex = i;
            }
        }

        if (nearestIndex < 0)
        {
            return;
        }

        RaycastHit nearestHit = _hits[nearestIndex];

        HarvestTarget target =nearestHit.collider.GetComponentInParent<HarvestTarget>();

        if (!target || !target.IsConfigured)
        {
            return;
        }

        float distanceSquared = (nearestHit.point - actor.position).sqrMagnitude;

        if (distanceSquared > _reach * _reach)
        {
            return;
        }

        InteractionTarget networkTarget = target.GetComponent<InteractionTarget>();

        if (!networkTarget || !_relay)
        {
            return;
        }

        bool sent = _relay.TrySendToNetworkTarget(networkTarget.NetId, InteractionAction.Harvest);

        Debug.Log($"Harvest request sent={sent}");
    }
}
