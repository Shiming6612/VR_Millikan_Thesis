using Fusion.XR.Shared.Core;
using UnityEngine;

namespace Fusion.XR.Shared.Base
{
    [DefaultExecutionOrder(INetworkRig.EXECUTION_ORDER)]
    public class NetworkController : NetworkRigPart, INetworkController
    {
        public override RigPartKind Kind => RigPartKind.Controller;
        public RigPartSide _side = RigPartSide.Undefined;
        public RigPartSide Side => _side;
    }
} 

