using Fusion.XR.Shared.Core.HardwareBasedGrabbing;
using Fusion.XR.Shared.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace Fusion.XR.Shared.Core.PhysicsGrabbing
{
    /**
     * Position based grabbable: will follow the grabber position accuratly while grabbed
     */
    public class PhysicsGrabbable : Grabbable
    {
        #region Follow configuration        
        [Header("Physics follow configuration")]
        [Range(0, 1)]
        public float followVelocityAttenuation = 0.5f;
        public float maxVelocity = 10f;
        #endregion

        protected override Vector3 Velocity
        {
            get
            {
#if UNITY_6000_0_OR_NEWER
                return rb.linearVelocity;
#else
                return rb.velocity;
#endif
            }
        }

        protected override Vector3 AngularVelocity
        {
            get
            {
                return rb.angularVelocity;
            }
        }

        public override void LockObjectPhysics()
        {

        }

        public override void UnlockObjectPhysics()
        {
        }
        protected override void TrackVelocity()
        {
            // No need to track simulated velocities here: we have the actual rigidbody's velocities for that
        }

        public override void Follow(Vector3 followedTransformPosition, Quaternion followedTransformRotation, Vector3 localPositionOffsetToFollowed, Quaternion localRotationOffsetTofollowed)
        {
        }

        public virtual void VelocityFollow(Vector3 followedTransformPosition, Quaternion followedTransformRotation, Vector3 localPositionOffsetToFollowed, Quaternion localRotationOffsetTofollowed, float elapsedTime)
        {
            // Compute the requested velocity to joined target position during a Runner.DeltaTime
            rb.VelocityFollow(followedTransformPosition, followedTransformRotation, localPositionOffsetToFollowed, localRotationOffsetTofollowed, elapsedTime);

            // To avoid a too aggressive move, we attenuate and limit a bit the expected velocity
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity *= followVelocityAttenuation; // followVelocityAttenuation = 0.5F by default
            rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, maxVelocity); // maxVelocity = 10f by default
#else
            rb.velocity *= followVelocityAttenuation; // followVelocityAttenuation = 0.5F by default
            rb.velocity = Vector3.ClampMagnitude(rb.velocity, maxVelocity); // maxVelocity = 10f by default
#endif
        }

    }

}
