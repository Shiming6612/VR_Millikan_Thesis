using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Core.HardwareBasedGrabbing;
using Fusion.XR.Shared.Utils;
using UnityEngine;

namespace Fusion.Addon.Colocalization
{
    /**
     * 
     * 
     * 
     **/

    public class SelfLocomotionGrabbable : Grabbable
    {
        IHardwareRig hardwareRig;

        [Header("World locomotion logic")]
        public bool amplifyBasedOnMoveSpeed = false;

        public AnimationCurve moveSpeedAmplificationCurve = new AnimationCurve(
            new Keyframe(0, 0),
            new Keyframe(0.3f, 0),
            new Keyframe(0.6f, 5f),
            new Keyframe(0.9f, 20f)
            );



        public enum ConstraintType
        {
            RotationFilter,
            LookAtHeadset
        }
        public ConstraintType rotationConstraintType = ConstraintType.RotationFilter;

        public Vector3 rotationConstraint = new Vector3(0, 1, 0);
        public Vector3 positionConstraint = new Vector3(1, 1, 1);
        public bool invertLookAtHeadsetDirection = false;

        const int BUFFER_SIZE = 10;
        float[] moveAmplitudes = new float[BUFFER_SIZE];
        float[] deltaTimes = new float[BUFFER_SIZE];
        int nextBufferEntry = 0;

        public override void Ungrab()
        {
            base.Ungrab();
            for (int i = 0; i < BUFFER_SIZE; i++)
            {
                deltaTimes[i] = 0;
            }
        }

        public bool applyGrabberOffset = true;

        public override void Follow(Vector3 followedTransformPosition, Quaternion followedTransformRotation, Vector3 localPositionOffsetToFollowed, Quaternion localRotationOffsetTofollowed)
        {
            // We do not want the object to move (only our rig), so we put it back in place: storing its original position for restoration at the end of the method
            var initialObjectPose = new Pose(transform.position, transform.rotation);

            // We move the trasnform's position/rotation to find where we would have the object, if it was a real grabbing
            // Note that we add constraints here to this target position

            base.Follow(followedTransformPosition, followedTransformRotation, localPositionOffsetToFollowed, localRotationOffsetTofollowed);


            var freelyMovedRotation = transform.rotation;
            var freelyMovedPosition = transform.position;
            Vector3 grabbingPointCurrentPosition;
            grabbingPointCurrentPosition = followedTransformPosition;

            var localGrappingPointPositionForGrabbableTransform = transform.InverseTransformPoint(grabbingPointCurrentPosition);
            if (rotationConstraintType == ConstraintType.RotationFilter)
            {
                transform.rotation = Quaternion.Euler(
                    rotationConstraint.x == 0 ? 0 : freelyMovedRotation.eulerAngles.x,
                    rotationConstraint.y == 0 ? 0 : freelyMovedRotation.eulerAngles.y,
                    rotationConstraint.z == 0 ? 0 : freelyMovedRotation.eulerAngles.z
                   );
            }
            else
            {
                if (hardwareRig == null) hardwareRig = currentGrabber.GetComponentInParent<IHardwareRig>();
                if (hardwareRig == null)
                {
                    // Probably using the spatial grabbing (no parent rig)
                    hardwareRig = HardwareRigsRegistry.GetHardwareRig();
                }
                if (hardwareRig != null)
                {
                    var direction = hardwareRig.Headset.transform.position - transform.position;
                    if (invertLookAtHeadsetDirection)
                    {
                        direction = -direction;
                    }
                    transform.rotation = Quaternion.LookRotation(direction);
                }
            }

            if (applyGrabberOffset)
            {
                var newGrabbbingPointPosition = transform.TransformPoint(localGrappingPointPositionForGrabbableTransform);
                transform.position = transform.position - (newGrabbbingPointPosition - grabbingPointCurrentPosition);
            }

            if (positionConstraint != Vector3.one)
            {
                transform.position = new Vector3(
                    positionConstraint.x == 0 ? initialObjectPose.position.x : freelyMovedPosition.x,
                    positionConstraint.y == 0 ? initialObjectPose.position.y : freelyMovedPosition.y,
                    positionConstraint.z == 0 ? initialObjectPose.position.z : freelyMovedPosition.z
                   );
            }

            // transform.position/rotation contains the places where the object would be with our current rig setup
            // But we want the object to stay in place, so our target rig position is the one resulting
            // from this new transform.position/rotation being replaced by the initial position objectPose
            var desiredPose = initialObjectPose;
            var sourcePose = new Pose(transform.position, transform.rotation);

            var rig = HardwareRigsRegistry.GetHardwareRig();
            (var rigPosition, var rigRotation) = TransformManipulations.DetermineNewRigPositionToMovePositionToTargetPosition(
                    sourcePose.position, sourcePose.rotation,
                    desiredPose.position, desiredPose.rotation,
                    rig.transform,
                    rig.Headset.transform,
                    ignoreYAxisMove: false, keepUpDirection: true
            );

            float amplification = 1;

            if (amplifyBasedOnMoveSpeed)
            {
                var rigMove = rigPosition - rig.transform.position;

                moveAmplitudes[nextBufferEntry] = rigMove.magnitude;
                deltaTimes[nextBufferEntry] = Time.deltaTime;
                nextBufferEntry = (nextBufferEntry + 1) % BUFFER_SIZE;

                var speed = rigMove.magnitude / Time.deltaTime;


                var totalTime = 0f;
                var totalMagnitude = 0f;
                for (int i = 0; i < BUFFER_SIZE; i++) {
                    if (deltaTimes[i] > 0)
                    {
                        totalTime += deltaTimes[i];
                        totalMagnitude += moveAmplitudes[i];
                    }
                }
                if (totalTime > 0) speed = totalMagnitude / totalTime;

                amplification = 1 + moveSpeedAmplificationCurve.Evaluate(speed);
            }
            rig.transform.position = rig.transform.position + amplification * (rigPosition - rig.transform.position);
            rig.transform.rotation = rigRotation;

            var amplifiedMove = rig.transform.position - rigPosition;

            // We do not want the object to move (only our rig), so we put it back in place: putting it back in its position
            transform.position = initialObjectPose.position + amplifiedMove;
            transform.rotation = initialObjectPose.rotation;
        }
    }

}

