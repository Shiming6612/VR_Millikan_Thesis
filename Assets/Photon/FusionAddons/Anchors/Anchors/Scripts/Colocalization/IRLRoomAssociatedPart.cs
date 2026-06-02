using Fusion;
using Fusion.XR.Shared.Utils;
using System.Collections;
using UnityEngine;

/// <summary>
/// Makes an object follow the local room member
/// Should be used for local objects only
/// </summary>
public class IRLRoomAssociatedPart : MonoBehaviour
{
    IRLRoomManager roomManager;

    private void Awake()
    {       
        roomManager = FindAnyObjectByType<IRLRoomManager>();
    }

    NetworkIRLRoomMember localMember = null;
    Vector3 offsetPositionToLocalMember;
    Quaternion offsetRotationToLocalMember;

    public void Update()
    {
        bool localUserDetectedThisFrame = false;
        if (localMember == null)
        {
            localMember = roomManager.localNetworkIRLRoomMember;
            if (localMember != null)
            {
                localUserDetectedThisFrame = true;
            }
        }

        if (localUserDetectedThisFrame)
        {
            offsetPositionToLocalMember = localMember.transform.InverseTransformPoint(transform.position);
            offsetRotationToLocalMember = Quaternion.Inverse(localMember.transform.rotation) * transform.rotation;
        }
        if (localMember != null)
        {
            transform.position = localMember.transform.TransformPoint(offsetPositionToLocalMember);
            transform.rotation = localMember.transform.rotation * offsetRotationToLocalMember;
        }
    }
}
