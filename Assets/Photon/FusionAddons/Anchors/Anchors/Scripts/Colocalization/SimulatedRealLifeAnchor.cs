using Fusion.XR.Shared.Core;
using UnityEngine;

/**
 * For Debug purposes
 * Once it has detected its position relative to the hardware rig, will reerve its offset to it
 * Usefull to simulate real life anchors staying in vision after a repositioning due to colocalization
 */
public class SimulatedRealLifeAnchor : MonoBehaviour
{
    [Tooltip("If true, current offset if forgotten, allowing to move the transform to set a new offset onve allowMove is unchecked again")]
    public bool allowMove = false;

    public Vector3 positionOffsetToRig = Vector3.zero;
    public Quaternion rotationOffsetToRig = Quaternion.identity;
    bool offsetsFound = false;
    IHardwareRig rig;

    private void Update()
    {
        if (allowMove)
        {
            offsetsFound = false;
        }
        if(rig == null) rig = HardwareRigsRegistry.GetHardwareRig();
        if(rig != null && offsetsFound == false && allowMove == false)
        {
            offsetsFound = true;
            positionOffsetToRig = rig.transform.InverseTransformPoint(transform.position);
            rotationOffsetToRig = Quaternion.Inverse(rig.transform.rotation) * transform.rotation;
        }
        if (offsetsFound && rig != null)
        {
            transform.rotation = rig.transform.rotation * rotationOffsetToRig;
            transform.position = rig.transform.TransformPoint(positionOffsetToRig);
        }
    }
}
