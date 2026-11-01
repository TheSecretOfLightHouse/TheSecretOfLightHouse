using Lighthouse.Interaction.Relay.Net;
using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.World.Harvest.Rules;
using Lighthouse.Systems.Audio;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Lighthouse.Player.Gathering.View;

[RequireComponent(typeof(PlayerController))]
public sealed class PlayerHarvestProbe : MonoBehaviour
{
    [SerializeField]
    private PlayerController _player;

    [SerializeField, Min(0.1f)]
    private float _probeDistance = 20f;

    [SerializeField, Min(0.1f)]
    private float _reach = 1f;

    [SerializeField]
    private LayerMask _probeMask = ~0;

    [SerializeField]
    private InteractionRelay _relay;

    [SerializeField]
    private PlayerActionEvents _actionEvents;

    [SerializeField]
    private ParticleSystem _impactPrefab;

    [SerializeField] private ParticleSystem _treeImpactPrefab;
    [SerializeField] private ParticleSystem _sandImpactPrefab;
    [SerializeField] private ParticleSystem _crystalImpactPrefab;
    [SerializeField] private ParticleSystem _oreImpactPrefab;

    [SerializeField] private AudioClip _rockHitClip;
    [SerializeField] private AudioClip _treeHitClip;
    [SerializeField] private AudioClip _sandHitClip;
    [SerializeField] private AudioClip _crystalHitClip;
    [SerializeField] private AudioClip _oreHitClip;
    [SerializeField, Range(0f, 1f)] private float _hitVolume = 0.65f;

    [SerializeField, Min(0.01f)]
    private float _impactScale = 0.65f;

    [SerializeField, Min(0.1f)]
    private float _impactCleanupDelay = 3f;

    [SerializeField, Min(0f)]
    private float _impactSurfaceOffset = 0.025f;

    private readonly RaycastHit[] _hits = new RaycastHit[32];
    private HarvestSpot _pendingSpot;
    private Vector3 _pendingPoint;
    private Vector3 _pendingNormal;
    private float _pendingTime;

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

        if (!_actionEvents)
        {
            _actionEvents = GetComponent<PlayerActionEvents>();
        }
    }

    private void OnEnable()
    {
        if (_actionEvents)
        {
            _actionEvents.HarvestImpact += HandleHarvestImpact;
        }
        if (_player)
        {
            _player.HarvestRequested += HandleHarvestRequested;
        }
    }

    private void OnDisable()
    {
        _pendingSpot = null;
        if (_actionEvents)
        {
            _actionEvents.HarvestImpact -= HandleHarvestImpact;
        }
        if (_player)
        {
            _player.HarvestRequested -= HandleHarvestRequested;
        }
    }

    private void Start()
    {
        if (_player && (_player.isLocalPlayer || _player.CanSimulateLocalWorld)
            && !TryGetComponent<HarvestSpotSparkles>(out _))
        {
            HarvestSpotSparkles sparkles = gameObject.AddComponent<HarvestSpotSparkles>();
            sparkles.Initialize(_player, _impactPrefab);
        }
    }

    private void HandleHarvestRequested(PlayerController player)
    {
        if (!player || !player.isActiveAndEnabled || player.IsDowned || player.IsOnBoat
            || (player.PassiveController && !player.PassiveController.IsJobAbilityEnabled))
        {
            return;
        }

        ProbeTarget(player.transform);
    }

    private void ProbeTarget(Transform actor)
    {
        Camera mainCamera = Camera.main;

        if (!mainCamera || Mouse.current == null || !_relay)
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

        HarvestSpot spot = nearestHit.collider.GetComponentInParent<HarvestSpot>();

        if (!spot || !spot.isActiveAndEnabled || !spot.IsBound)
        {
            return;
        }

        float distanceSquared = (nearestHit.point - actor.position).sqrMagnitude;

        if (distanceSquared > _reach * _reach)
        {
            return;
        }

        _pendingSpot = spot;
        _pendingPoint = nearestHit.point;
        _pendingNormal = nearestHit.normal;
        _pendingTime = Time.time;

        if (_actionEvents)
        {
            _actionEvents.Publish(PlayerAnimationAction.Dig);
        }

        _relay.TrySendToNonNetworkTarget(
            InteractionTargetKind.HarvestSpot,
            spot.SpotId,
            InteractionAction.Harvest);

    }

    private void HandleHarvestImpact()
    {
        HarvestSpot spot = _pendingSpot;
        _pendingSpot = null;
        // Discard interrupted swings rather than replaying them on a later animation.
        if (!spot || !spot.isActiveAndEnabled || !spot.IsBound || !_player
            || !_player.isActiveAndEnabled || _player.IsDowned || _player.IsOnBoat
            || Time.time - _pendingTime > 2f
            || (_player.PassiveController && !_player.PassiveController.IsJobAbilityEnabled)
            || (_pendingPoint - _player.transform.position).sqrMagnitude > _reach * _reach)
        {
            return;
        }

        PlayImpact(_pendingPoint, _pendingNormal, spot.Category);
        PlayHitSound(_pendingPoint, spot.Category);
    }

    private AudioClip GetHitClip(HarvestSpotCategory category)
    {
        switch (category)
        {
            case HarvestSpotCategory.Rock:
                return _rockHitClip;
            case HarvestSpotCategory.Tree:
                return _treeHitClip;
            case HarvestSpotCategory.Sand:
                return _sandHitClip;
            case HarvestSpotCategory.Crystal:
                return _crystalHitClip;
            case HarvestSpotCategory.Ore:
                return _oreHitClip;
            default:
                return null;
        }
    }

    private void PlayHitSound(Vector3 position, HarvestSpotCategory category)
    {
        AudioClip clip = GetHitClip(category);
        SoundManager sound = SoundManager.Instance;
        if (clip && sound)
        {
            sound.PlayEffectAt(clip, position, _hitVolume);
        }
    }

    private ParticleSystem GetImpactPrefab(HarvestSpotCategory category)
    {
        switch (category)
        {
            case HarvestSpotCategory.Tree:
                return _treeImpactPrefab ? _treeImpactPrefab : _impactPrefab;
            case HarvestSpotCategory.Sand:
                return _sandImpactPrefab ? _sandImpactPrefab : _impactPrefab;
            case HarvestSpotCategory.Crystal:
                return _crystalImpactPrefab ? _crystalImpactPrefab : _impactPrefab;
            case HarvestSpotCategory.Ore:
                return _oreImpactPrefab ? _oreImpactPrefab : _impactPrefab;
            default:
                return _impactPrefab;
        }
    }

    private void PlayImpact(Vector3 position, Vector3 normal, HarvestSpotCategory category)
    {
        ParticleSystem prefab = GetImpactPrefab(category);
        if (!prefab)
        {
            return;
        }

        ParticleSystem impact = Instantiate(
            prefab, position + normal * _impactSurfaceOffset,
            Quaternion.FromToRotation(Vector3.up, normal));
        impact.transform.localScale = Vector3.one * _impactScale;
        impact.Play(true);
        Destroy(impact.gameObject, _impactCleanupDelay);
    }
}
