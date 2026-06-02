using Fusion.XR.Shared.Utils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.XR.Shared
{
    /***
     * 
     * VelocityExtension provides the AngularVelocityChange helper method : it returns an angular velocity based on the elapsed time between the previous and the new rotation
     * 
     ***/
    public static class VelocityExtension
    {
        public static Vector3 AngularVelocityChange(this Quaternion previousRotation, Quaternion newRotation, float elapsedTime)
        {
            Quaternion rotationStep = newRotation * Quaternion.Inverse(previousRotation);
            rotationStep.ToAngleAxis(out float angle, out Vector3 axis);
            // Angular velocity uses eurler notation, bound to -180° / +180°
            if (angle > 180f)
            {
                angle -= 360f;
            }

            if (Mathf.Abs(angle) > Mathf.Epsilon)
            {
                float radAngle = angle * Mathf.Deg2Rad;
                Vector3 angularStep = axis * radAngle;
                Vector3 angularVelocity = angularStep / elapsedTime;
                if (!float.IsNaN(angularVelocity.x))
                    return angularVelocity;
            }
            return Vector3.zero;
        }

        #region Physics based tracking
        /**
          * Source materials for the physics based tracking logic:
          * - Damper based attraction http://digitalopus.ca/site/pd-controllers/
          * - XRInteractionTookit XRBaseInteractable:PerformVelocityTrackingUpdate
          * - Rotation derivate https://forum.unity.com/threads/manually-calculate-angular-velocity-of-gameobject.289462/#post-4302796
          * - SteamVR like physics (publicly visible here https://github.com/wacki/Unity-VRInputModule/blob/master/Assets/SteamVR/InteractionSystem/Core/Scripts/VelocityEstimator.cs)
         */

        public static void VelocityFollow(this Rigidbody followerRb, Vector3 targetBasePosition, Quaternion targetBaseRotation, Vector3 positionOffset, Quaternion rotationOffset, float elapsedTime)
        {
            (Vector3 targetPosition, Quaternion targetRotation) = TransformManipulations.ApplyUnscaledOffset(
                referenceTransformPosition: targetBasePosition, referenceTransformRotation: targetBaseRotation,
                offset: positionOffset,
                rotationOffset: rotationOffset
            );

            followerRb.VelocityFollow(targetPosition, targetRotation, elapsedTime);
        }

        public static void VelocityFollow(this Rigidbody followerRb, Transform target, Vector3 positionOffset, Quaternion rotationOffset, float elapsedTime)
        {
            followerRb.VelocityFollow(target.TransformPoint(positionOffset), target.rotation * rotationOffset, elapsedTime);
        }

        public static void VelocityFollow(this Rigidbody followerRb, Vector3 targetPosition, Quaternion targetRotation, float elapsedTime)
        {
            Vector3 positionStep = targetPosition - followerRb.transform.position;
            Vector3 velocity = positionStep / elapsedTime;

#if UNITY_6000_0_OR_NEWER
            followerRb.linearVelocity = velocity;
#else
            followerRb.velocity = velocity;
#endif
            followerRb.angularVelocity = followerRb.transform.rotation.AngularVelocityChange(newRotation: targetRotation, elapsedTime: elapsedTime);
        }
        #endregion

    }
}
