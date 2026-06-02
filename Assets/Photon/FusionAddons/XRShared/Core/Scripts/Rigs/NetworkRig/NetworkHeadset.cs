using UnityEngine;
using Fusion.XR.Shared.Core;

namespace Fusion.XR.Shared.Base
{
    [DefaultExecutionOrder(INetworkRig.EXECUTION_ORDER)]
    public class NetworkHeadset : NetworkRigPart, INetworkHeadset
    {
        public override RigPartKind Kind => RigPartKind.Headset;
    }
} 

