using UnityEngine;

namespace Fusion.XR.Shared.Core
{
    // Should be placed next to a INetworkRigPart
    [DefaultExecutionOrder(NetworkGrabber.EXECUTION_ORDER)]
    public class NetworkGrabber : NetworkBehaviour, INetworkGrabber, IRigPartPositionModifierProxy
    {
        public const int EXECUTION_ORDER = INetworkRig.EXECUTION_ORDER + 10;
        #region INetworkGrabber
        public INetworkRigPart RigPart => networkRigPart;
        #endregion

        public INetworkRigPart networkRigPart;
        public INetworkGrabbable GrabbedObject { get; set; } = null;

        protected virtual void Awake()
        {
            networkRigPart = GetComponent<INetworkRigPart>();
            if (networkRigPart == null)
            {
                Debug.LogError("[NetworkGrabber] Missing INetworkRigPart (NetworkHand, NetworkController, ...)");
            }
        }
        
        #region IRigPartPositionModifierProxy
        // Forward the rig positoin modifier request to the grabbe dobject (which will decide what to do and how to modify hand position)
        public IRigPartPositionModifier ActualModifier => (GrabbedObject is IRigPartPositionModifier) ? ((IRigPartPositionModifier)GrabbedObject) : null;
        #endregion

    }
}
