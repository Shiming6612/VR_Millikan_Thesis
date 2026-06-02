using Fusion.XR.Shared.Core;
using UnityEngine;

namespace Fusion.Addons.VisionOsHelpers
{
    public class NetworkSpatialPointer : NetworkRigPart
    {
        public override RigPartKind Kind => RigPartKind.Pointer;
        public SpatialPointerId pointerId = SpatialPointerId.Undefined;

        protected override void Awake()
        {
            base.Awake();
            if (pointerId == SpatialPointerId.Undefined)
            {
                Debug.LogError("[NetworkSpatialPointer] pointerId should be set to a not undefined value");
            }
        }

        protected override bool IsMatchingHardwareRigPart(IHardwareRigPart rigPart)
        {
            if (rigPart is HardwareSpatialPointer hardwarePointer && hardwarePointer.pointerId == pointerId)
            {
                return true;
            }
            return false;
        }
    }
}
