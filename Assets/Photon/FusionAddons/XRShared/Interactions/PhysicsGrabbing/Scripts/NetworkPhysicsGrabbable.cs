//#define USE_PHYSICSADDON
#if USE_PHYSICSADDON
using Fusion.Addons.Physics;
#endif
using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Core.HardwareBasedGrabbing;
using Fusion.XR.Shared.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace Fusion.XR.Shared.Core.PhysicsGrabbing
{
    /**
     * 
     * Declare that this game object can be grabbed by a NetworkGrabber
     * 
     * Handle following the grabbing NetworkGrabber
     * 
     **/
    [DefaultExecutionOrder(NetworkGrabbable.EXECUTION_ORDER)]
    public class NetworkPhysicsGrabbable : NetworkGrabbable, IRigPartPositionModifier
    {
        [HideInInspector] 
        public Tick lastGrabbedTick = 0;
        bool collisionDetectedThisFrame;

        protected override void Awake()
        {
            base.Awake();
#if !FUSION_2_1_OR_NEWER
            Debug.LogError("NetworkPhysicsGrabbable requires Fusion 2.1 and forcast physics option enabled.");
#endif
        }
        protected override void FindGrabbable()
        {
            grabbable = GetComponent<PhysicsGrabbable>();
            if (grabbable == null)
            {
                // We do not use requireComponent as this classes can be subclassed
                grabbable = gameObject.AddComponent<PhysicsGrabbable>();
            }
        }

        public override void Render()
        {
            base.Render();
            if (IsGrabbed)
            {
                lastGrabbedTick = Runner.Tick;
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            collisionDetectedThisFrame = true;
        }

        private void FixedUpdate()
        {
            collisionDetectedThisFrame = false;
            var isGrabbed = IsGrabbed;
            Vector3 followedGrabberRootPosition = Vector3.zero;
            Quaternion followedGrabberRootRotation = Quaternion.identity;
            Vector3 localPositionOffset = default;
            Quaternion localRotationOffset = default;
            NetworkGrabber networkGrabber = null;
            if (isTakingAuthority && extrapolateWhileTakingAuthority && grabbable.currentGrabber)
            {
                // Extrapolation while taking authority
                isGrabbed = true;
                networkGrabber = grabbable.currentGrabber.NetworkGrabber;

                // Extrapolation: Make visual representation follow grabber, adding position/rotation offsets
                // We are currently waiting for the authority transfer: the network vars are not already set, so we use the temporary versions
                localPositionOffset = grabbable.localPositionOffset;
                localRotationOffset = grabbable.localRotationOffset;
            }
            else if (IsGrabbed)
            {
                networkGrabber = CurrentGrabber;
                localPositionOffset = LocalPositionOffset;
                localRotationOffset = LocalRotationOffset;
            }

            if (networkGrabber)
            {
                followedGrabberRootPosition = networkGrabber.networkRigPart.DisplayedPositionWithoutModifiers;
                followedGrabberRootRotation = networkGrabber.networkRigPart.DisplayedRotationWithoutModifiers;
            }

            if (isGrabbed)
            {
                if (grabbable is PhysicsGrabbable physicsGrabbable)
                {
                    // Follow grabber, adding position/rotation offsets
                    physicsGrabbable.VelocityFollow(followedGrabberRootPosition, followedGrabberRootRotation, localPositionOffset, localRotationOffset, elapsedTime: Time.fixedDeltaTime);
                }
                else
                {
                    Debug.LogError("Should use a physics grabbable");
                }
            }
        }

        #region IRigPartPositionModifier
        public bool IsModificationActive => IsGrabbed;
        public Quaternion RotationModification => transform.rotation * Quaternion.Inverse(LocalRotationOffset);
        public Vector3 PositionModification { 
            get {
                (var positionOffsetAppliedToGrabber, _) = TransformManipulations.ApplyUnscaledOffset(
                    referenceTransformPosition: CurrentGrabber.transform.position, referenceTransformRotation: CurrentGrabber.transform.rotation,
                    offset: LocalPositionOffset,
                    rotationOffset: LocalRotationOffset);
                return CurrentGrabber.transform.position + (transform.position - positionOffsetAppliedToGrabber);
            } 
        } 
        public bool IsHapticFeedbackRequired => collisionDetectedThisFrame;
        public IRigPartPositionModifier.ModificationPositioningMode PositioningMode => IRigPartPositionModifier.ModificationPositioningMode.Absolute;
        public bool ApplyOnlyLocally => false;
        #endregion
    }
}
