using Fusion.XR.Shared.Locomotion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForbiddenZones : MonoBehaviour, ILocomotionValidator
{
    [SerializeField] List<Transform> forbiddenZones;


    public bool CanMoveHeadset(Vector3 headsetNewPosition)
    {
        foreach(var zone in forbiddenZones)
        {
            Vector3 zonePosition = zone.position;
            zonePosition.y = headsetNewPosition.y;

            if (Vector3.Distance(zonePosition, headsetNewPosition) < zone.localScale.x/2f)
                return false;
        }
        return true;
    }
}
