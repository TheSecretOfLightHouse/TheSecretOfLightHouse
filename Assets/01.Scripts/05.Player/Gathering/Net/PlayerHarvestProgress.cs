using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerHarvestProgress : NetworkBehaviour
{
    private PersonalHarvestData _data;

    //public override void OnStartServer()
    //{
    //    _data = null;

    //    base.OnStartServer();
    //    //_data = new PersonalHarvestData();
    //}

    public override void OnStopServer()
    {
        _data = null;
        base.OnStopServer();
    }

    [Server]
    public void BindSharedData(PersonalHarvestData sharedData)
    {
        _data = sharedData;
    }

    [Server]
    public int GetRemaining(int spotId, int initialCount)
    {
        if (_data == null)
        {
            return 0;
        }

        return _data.GetRemaining(spotId, initialCount);
    }

    [Server]
    public bool TryConsumeOne(int spotId, int initialCount, out int remaining)
    {
        remaining = 0;

        if (_data == null)
        {
            return false;
        }

        return _data.TryConsumeOne(spotId, initialCount, out remaining);
    }

    [Server]
    public void RestoreOne(int spotId, int initialCount)
    {
        if (_data == null)
        {
            return;
        }

        _data.RestoreOne(spotId, initialCount);
    }
}
