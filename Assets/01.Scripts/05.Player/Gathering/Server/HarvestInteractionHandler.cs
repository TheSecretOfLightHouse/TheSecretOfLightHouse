using Lighthouse.Interaction.Requests.Net;
using Lighthouse.Interaction.Requests.Rules;
using Lighthouse.Interaction.Router.Server;
using Lighthouse.Interaction.Targets.Server;
using Mirror;
using UnityEngine;

[RequireComponent(typeof(NetworkIdentity))]
[RequireComponent(typeof(HarvestDropSpawner))]
public sealed class HarvestInteractionHandler : NetworkBehaviour, IInteractionHandler, INonNetworkTargetAdapter
{
    [SerializeField]
    private LocalHarvestTestSpot[] _testSpots;

    [SerializeField, Min(0.1f)]
    private float _serverRange = 3f;

    private HarvestDropSpawner _dropSpawner;
    private bool _isRegistered;
    private bool _isAdapterRegistered;

    private PersonalHarvestData _sharedHarvestData;

    private void Awake()
    {
        _dropSpawner = GetComponent<HarvestDropSpawner>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        _sharedHarvestData = new PersonalHarvestData();

        _isAdapterRegistered = TargetResolver.RegisterAdapter(InteractionTargetKind.HarvestSpot,this);

        if (!_isAdapterRegistered)
        {
            return;
        }

        _isRegistered = InteractionRouter.RegisterHandler(InteractionAction.Harvest,this);

        if (!_isRegistered)
        {
            TargetResolver.UnregisterAdapter(InteractionTargetKind.HarvestSpot,this);
            _isAdapterRegistered = false;
        }
    }

    public override void OnStopServer()
    {
        if (_isRegistered)
        {
            InteractionRouter.UnregisterHandler(InteractionAction.Harvest,this);
            _isRegistered = false;
        }

        if (_isAdapterRegistered)
        {
            TargetResolver.UnregisterAdapter(InteractionTargetKind.HarvestSpot,this);
            _isAdapterRegistered = false;
        }

        _sharedHarvestData = null;
        base.OnStopServer();
    }

    //id를 서버 위치로 변환하는 함수
    public bool TryResolve(int key,InteractionAction action,out Vector3 position,out float serverRange)
    {
        position = default;
        serverRange = _serverRange;

        if (!isServer ||!_isRegistered ||action != InteractionAction.Harvest ||!TryGetTestTarget(key, out HarvestTarget target))
        {
            return false;
        }

        position = target.transform.position;
        return true;
    }

    public InteractionResultCode Handle(in InteractionContext context)
    {
        if (!isServer || !_isRegistered || !_dropSpawner || _sharedHarvestData == null)
        {
            return InteractionResultCode.Rejected;
        }

        if (context.Action != InteractionAction.Harvest || context.Target.Kind != InteractionTargetKind.HarvestSpot)
        {
            return InteractionResultCode.InvalidTarget;
        }

        if (context.Sender == null || !context.Sender.identity)
        {
            return InteractionResultCode.Rejected;
        }

        PlayerController player = context.Sender.identity.GetComponent<PlayerController>();

        if (!player || !player.isActiveAndEnabled || player.IsDowned || player.IsOnBoat)
        {
            return InteractionResultCode.Rejected;
        }

        if (!TryGetTestTarget(context.Target.Key, out HarvestTarget target))
        {
            return InteractionResultCode.InvalidTarget;
        }

        PlayerHarvestProgress progress = context.Sender.identity.GetComponent<PlayerHarvestProgress>();

        if (!progress || !progress.isActiveAndEnabled)
        {
            return InteractionResultCode.Rejected;
        }

        // 모든 플레이어가 서버 처리기의 같은 잔량 원장을 사용한다.
        progress.BindSharedData(_sharedHarvestData);

        int spotId = context.Target.Key;
        int initialCount = target.InitialCount;

        if (!progress.TryConsumeOne(spotId, initialCount, out _))
        {
            return InteractionResultCode.Rejected;
        }

        bool isSpawned = false;

        try
        {
            isSpawned = _dropSpawner.TrySpawnDrop(target);
        }
        finally
        {
            if (!isSpawned)
            {
                progress.RestoreOne(spotId, initialCount);
            }
        }

        return isSpawned ? InteractionResultCode.Success : InteractionResultCode.Rejected;
    }

    private bool TryGetTestTarget(int spotId,out HarvestTarget target)
    {
        target = null;

        if (spotId <= 0 || _testSpots == null)
        {
            return false;
        }

        LocalHarvestTestSpot matchedSpot = null;

        for (int i = 0; i < _testSpots.Length; i++)
        {
            LocalHarvestTestSpot spot = _testSpots[i];

            if (!spot || spot.SpotId != spotId)
            {
                continue;
            }
            if (matchedSpot)
            {
                return false;
            }

            matchedSpot = spot;
        }

        if (!matchedSpot || !matchedSpot.isActiveAndEnabled)
        {
            return false;
        }

        target = matchedSpot.GetComponent<HarvestTarget>();
        return target && target.IsConfigured;
    }
}
