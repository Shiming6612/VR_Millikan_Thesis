using Fusion.XR.Shared.Core;
using UnityEngine;

namespace Fusion.XR.Shared.Base
{
    [DefaultExecutionOrder(INetworkRig.EXECUTION_ORDER)]
    public class NetworkHand : NetworkRigPart, INetworkHand
    {
        public override RigPartKind Kind => RigPartKind.Hand;
        public RigPartSide _side = RigPartSide.Undefined;
        public RigPartSide Side => _side;

        public IHardwareHand LocalHardwareHand => LocalHardwareRigPart as IHardwareHand;
    }
} 

