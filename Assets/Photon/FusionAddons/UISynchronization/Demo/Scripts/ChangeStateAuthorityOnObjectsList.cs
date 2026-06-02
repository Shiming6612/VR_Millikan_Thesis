using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class ChangeStateAuthorityOnObjectsList : MonoBehaviour
{

    public List<NetworkObject> networkObjects = new List<NetworkObject>();
    public GameObject rootObjectToSearchNetworkObjects;

    private void Awake()
    {
        if (networkObjects.Count == 0 && rootObjectToSearchNetworkObjects == null)
        {
            rootObjectToSearchNetworkObjects = this.gameObject;
        }


        if (networkObjects.Count == 0 && rootObjectToSearchNetworkObjects != null)
        {
            foreach (NetworkObject networkObject in rootObjectToSearchNetworkObjects.GetComponentsInChildren<NetworkObject>(true))
            {
                networkObjects.Add(networkObject);
            }
        }
    }

    [EditorButton("RequestStateAuthorityOnAllObjectsInList")]
    public void RequestStateAuthorityOnAllObjectsInList()
    {
        foreach (NetworkObject networkObject in networkObjects)
        {
            networkObject.RequestStateAuthority();
        }
    }

    [EditorButton("ReleaseStateAuthorityOnAllObjectsInList")]
    public void ReleaseStateAuthorityOnAllObjectsInList()
    {
        foreach (NetworkObject networkObject in networkObjects)
        {
            networkObject.ReleaseStateAuthority();
        }
    }
}
