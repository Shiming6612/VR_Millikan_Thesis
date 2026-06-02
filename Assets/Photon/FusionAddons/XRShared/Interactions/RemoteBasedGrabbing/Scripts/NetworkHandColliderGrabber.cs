using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Rig;
using UnityEngine;

namespace Fusion.XR.Shared.Grabbing.NetworkHandColliderBased
{
    /**
     * 
     * Allows a NetworkHand to grab NetworkHandColliderGrabbable objects
     * 
     **/

    [DefaultExecutionOrder(NetworkHandColliderGrabber.EXECUTION_ORDER)]
    public class NetworkHandColliderGrabber : NetworkBehaviour
    {
        public const int EXECUTION_ORDER = ILateralizedNetworkRigPart.EXECUTION_ORDER + 10;
        [Networked]
        public NetworkHandColliderGrabbable GrabbedObject { get; set; }

        [HideInInspector]
        public INetworkRigPart rigPart;
        private void Awake()
        {
            rigPart = GetComponentInParent<INetworkRigPart>();
            if (rigPart == null)
            {
                Debug.LogError("Not placed under a INetworkRigPart");
            }
        }

        Collider lastCheckedCollider;
        NetworkHandColliderGrabbable lastCheckColliderGrabbable;

        private void OnTriggerStay(Collider other)
        {
            // We only trigger grabbing for our local hands
            if (rigPart.Object || rigPart.Object.HasStateAuthority || rigPart.LocalHardwareRigPart == null) return;

            // Exit if an object is already grabbed
            if (GrabbedObject != null)
            {
                // It is already the grabbed object or another, but we don't allow shared grabbing here
                return;
            }

            NetworkHandColliderGrabbable grabbable;

            if (lastCheckedCollider == other)
            {
                grabbable = lastCheckColliderGrabbable;
            } 
            else
            {
                grabbable = other.GetComponentInParent<NetworkHandColliderGrabbable>();
            }
            // To limit the number of GetComponent calls, we cache the latest checked collider grabbable result
            lastCheckedCollider = other;
            lastCheckColliderGrabbable = grabbable;
            if (grabbable != null)
            {
                if (rigPart.LocalHardwareRigPart is IGrabbingProvider grabbingProvider && grabbingProvider.IsGrabbing) Grab(grabbable);
            } 
        }

        // Ask the grabbable object to start following the hand
        public void Grab(NetworkHandColliderGrabbable grabbable)
        {
            Debug.Log($"Try to grab object {grabbable.gameObject.name} with {gameObject.name}");
            grabbable.Grab(this);
            GrabbedObject = grabbable;
        }

        // Ask the grabbable object to stop following the hand
        public void Ungrab(NetworkHandColliderGrabbable grabbable)
        {
            Debug.Log($"Try to ungrab object {grabbable.gameObject.name} with {gameObject.name}");
            GrabbedObject.Ungrab();
            GrabbedObject = null;
        }


        public override void Render()
        {
            base.Render();
            if (rigPart.Object || rigPart.Object.HasStateAuthority || rigPart.LocalHardwareRigPart == null) return;

            // Check if the local hand is still grabbing the object
            if (GrabbedObject != null && rigPart.LocalHardwareRigPart is IGrabbingProvider grabbingProvider && grabbingProvider.IsGrabbing == false)
            {
                // Object released by this hand (we don't wait for a fun to trigger this, to avoid the object to stay sticked to the hand until the next FUN tick)
                Ungrab(GrabbedObject);
            }
        }
    }
}
