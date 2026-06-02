using UnityEngine;

namespace Fusion.XR.Shared.Core.PhysicsGrabbing
{
    public class CollisionAuthorityTransfer : NetworkBehaviour
    {
        [HideInInspector]
        public NetworkPhysicsGrabbable grabbable;

        private void Awake()
        {
            grabbable = GetComponent<NetworkPhysicsGrabbable>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (enabled == false) return;
            if (Object == null || Object.IsValid == false) return;
            if (Object.HasStateAuthority == false) return;
            if (collision.rigidbody == null) return;

            var other = collision.rigidbody.GetComponent<CollisionAuthorityTransfer>();

            if (other.Object.HasStateAuthority) return;
            // We do not "take" a grabbed objet
            if (other.grabbable != null && other.grabbable.IsGrabbed) return;

            bool shouldTakeAuthority = false;

            if (grabbable && grabbable.IsGrabbed)
            {
                // The local user is grabbing the object and hitting a non grabbed object
                shouldTakeAuthority = true;
            }
            else if(grabbable && other.grabbable == null)
            {
                shouldTakeAuthority = true;
            }
            else if(grabbable && other.grabbable)
            {
                shouldTakeAuthority = grabbable.lastGrabbedTick > other.grabbable.lastGrabbedTick;
            }

            if (shouldTakeAuthority)
            {
                other.Object.RequestStateAuthority();
            }
        }
    }
}

