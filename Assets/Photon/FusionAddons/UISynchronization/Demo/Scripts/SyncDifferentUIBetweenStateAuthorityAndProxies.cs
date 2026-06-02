using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class SyncDifferentUIBetweenStateAuthorityAndProxies : NetworkBehaviour, IStateAuthorityChanged
{
    public List<GameObject> objectListForStateAuthority = new List<GameObject>();
    public List<GameObject> objectListForProxies = new List<GameObject>();

    public override void Spawned()
    {
        base.Spawned();
        ConfigureObjectVisibilityBasedOnStateAuthority();
    }

    public void ConfigureObjectVisibilityBasedOnStateAuthority()
    {
        foreach (GameObject obj in objectListForStateAuthority)
        {
            obj.SetActive(Object.HasStateAuthority);
        }
        foreach (GameObject obj in objectListForProxies)
        {
            obj.SetActive(!Object.HasStateAuthority);
        }
    }

    public void StateAuthorityChanged()
    {
        ConfigureObjectVisibilityBasedOnStateAuthority();
    }
}
