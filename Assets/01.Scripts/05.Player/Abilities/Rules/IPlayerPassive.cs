using UnityEngine;

public interface IPlayerPassive
{
    void Attach(PlayerController player);
    void Refresh();
    void Detach();
}

