using Fusion.XR.Shared.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.AnchorsAddon
{
    /// <summary>
    /// AnchorsManipulation contains the various methods to compute poses or object position when using anchors.
    /// </summary>
    public static class AnchorsManipulation
    {
        public delegate Pose PoseModifier(Pose pose);

        [System.Serializable]
        public struct AverageAnchorAlgorithmSettings
        {
            public bool forceUp;

            public static AverageAnchorAlgorithmSettings DefaultSettings = new AverageAnchorAlgorithmSettings { forceUp = false };
        }

        public static Pose AverageAnchorPose(IEnumerable<Pose> anchorPoses, PoseModifier poseModifier = null, AverageAnchorAlgorithmSettings? avgSettings = null, List<int> weigths = null)
        {
            Vector3 averagePosition = Vector3.zero;
            Vector3 averageForwardDirection = Vector3.zero;
            Vector3 averageUpDirection = Vector3.zero;
            Vector3? firstAnchorForward = null;
            Vector3? firstAnchorUp = null;
            int anchorPosesTotalWeight = 0;
            int i = 0;
            foreach (var anchorPose in anchorPoses)
            {
                var pose = anchorPose;
                if(poseModifier != null)
                {
                    pose = poseModifier(pose);
                }
                var forward = pose.rotation * Vector3.forward;
                var up = pose.rotation * Vector3.up;
                if (firstAnchorForward == null)
                {
                    firstAnchorForward = forward;
                }
                if (firstAnchorUp == null)
                {
                    firstAnchorUp = up;
                }
                int weight = 1;
                if (weigths != null && weigths.Count > i)
                {
                    weight = weigths[i];
                }
                averagePosition += weight * pose.position;
                averageForwardDirection += weight * forward;
                averageUpDirection += weight * up;
                anchorPosesTotalWeight += weight;
                i++;
            }
            if (i == 0) throw new System.Exception("Not enough anchors to average them");

            averageForwardDirection = averageForwardDirection.normalized;
            averageUpDirection = averageUpDirection.normalized;
            averagePosition = averagePosition / anchorPosesTotalWeight;
            Quaternion averageRotation;
            if (avgSettings?.forceUp == true)
            {
                averageRotation = Quaternion.LookRotation(averageForwardDirection, Vector3.up);
            }
            else
            {
                averageRotation = Quaternion.LookRotation(averageForwardDirection, averageUpDirection);
            }
            return new Pose { position = averagePosition, rotation = averageRotation };
        }

        public static (Vector3, float) AverageDeviationToMeanPosition(IEnumerable<Pose> anchorPoses, Pose mean)
        {
            Vector3 positionDeviation = Vector3.zero;
            float rotationDeviation = 0;
            int poseCount = 0;
            foreach (var anchorPose in anchorPoses)
            {
                var forward = anchorPose.rotation * Vector3.forward;
                var up = anchorPose.rotation * Vector3.up;
                var right = anchorPose.rotation * Vector3.right;

                var localPositionDeviation = (anchorPose.position - mean.position);
                localPositionDeviation = new Vector3(Mathf.Abs(localPositionDeviation.x), Mathf.Abs(localPositionDeviation.y), Mathf.Abs(localPositionDeviation.z));
                positionDeviation += localPositionDeviation;
                rotationDeviation += Quaternion.Angle(anchorPose.rotation, mean.rotation);
                poseCount++;
            }
            positionDeviation = positionDeviation / poseCount;
            rotationDeviation = rotationDeviation / poseCount;
            return (positionDeviation, rotationDeviation);
        }

        public static Pose TripletRepresentativeAnchorPose(List<Pose> anchorPoses, PoseModifier poseModifier = null)
        {
            if(anchorPoses.Count < 3)
            {
                var settings = AverageAnchorAlgorithmSettings.DefaultSettings;
                return AverageAnchorPose(anchorPoses, poseModifier, settings);
            }
            //TODO Prepare a version without list allocation
            var tripletAnchors = new List<Pose>();
            int tripletAnchorCount = anchorPoses.Count / 3;
            var remainingAnchors = anchorPoses.Count % 3;
            if(remainingAnchors > 0) {
                // The last triplet will reuse some of the start anchors to be complete
                tripletAnchorCount++;
            }
            for (int i = 0; i < tripletAnchorCount; i++) {
                var a = anchorPoses[(i * 3) % anchorPoses.Count];
                var b = anchorPoses[(i * 3 + 1) % anchorPoses.Count];
                var c = anchorPoses[(i * 3 + 2) % anchorPoses.Count];
                tripletAnchors.Add(TripletNormalAnchor(a, b, c, poseModifier));
            }
            return TripletRepresentativeAnchorPose(tripletAnchors, poseModifier);
        }

        public static Pose TripletNormalAnchor(Pose a, Pose b, Pose c, PoseModifier poseModifier = null)
        {
            if (poseModifier != null) {
                a = poseModifier(a);
                b = poseModifier(b);
                c = poseModifier(c);
            }
            // A,B,C => up = Vector3.Cross(AB, AC), forward = AB+AC or AB-AC (to maximize vector length)
            var position = (a.position + b.position + c.position) / 3;
            var ab = b.position - a.position;
            var ac = c.position - a.position;
            var up = Vector3.Cross(ab, ac);
            Vector3 forward;
            if (Vector3.Dot(ab, ac) > 0)
            {
                // Same "direction", we can add them
                forward = ab + ac;
            }
            else
            {
                // Opposite "direction", to avoid small vector, we use the opposite
                forward = ab - ac;
            }
            var rotation = Quaternion.LookRotation(forward, up);
            return new Pose { position = position, rotation = rotation };
        }

        /// <summary>
        /// Return the object pose, so that its relativePose ends up at the absolute pose (after applying this pose to the object)
        /// 
        /// relativePose poses must be relative to the objectTransform referential
        /// </summary>
        public static (Vector3 newReferencePosition, Quaternion newReferencerotation) ObjectPositionToMoveRelativePoseToTargetAbsolutePose(
            Transform referenceTransform,
            Pose relativePose,
            Pose absoluteTargetPose
            )
        {
            return TransformManipulations.ReferentialPositionToRespectOffsetsOfPositionedObject(
                referenceTransform, 
                absoluteTargetPose.position, absoluteTargetPose.rotation, 
                relativePose.position, relativePose.rotation, 
                acceptLossyScale: true);
        }

        /// <summary>
        /// Return the object pose, so that each of its relativePoses ends up 
        ///  at the matching absolute pose in the absolutePoses list (after applying this pose to the object)
        /// 
        /// Uses an average pose computation for both (smoothing any incompatibility between the requested positions)
        /// 
        /// relativePoses poses must be relative to the objectTransform referential
        /// </summary>
        public static (Vector3 newReferencePosition, Quaternion newReferencerotation) ObjectPositionToMoveRelativePosesToTargetAbsolutePoses(Transform objectTransform, 
            List<Pose> relativePoses, List<Pose> absoluteTargetPoses, 
            AverageAnchorAlgorithmSettings? avgSettings = null, bool useTripletAlgorithm = false, List<int> weigths = null, Transform commonReferenceAnchor = null)
        {
            Pose averageRelativePose;
            Pose averageTargetAbsolutePose;

            if (useTripletAlgorithm)
            {
                averageRelativePose = TripletRepresentativeAnchorPose(relativePoses);
                averageTargetAbsolutePose = TripletRepresentativeAnchorPose(absoluteTargetPoses);
            }
            else
            {
                averageRelativePose = AverageAnchorPose(relativePoses, avgSettings: avgSettings, weigths: weigths);
                averageTargetAbsolutePose = AverageAnchorPose(absoluteTargetPoses, avgSettings: avgSettings, weigths: weigths);
            }
            if (commonReferenceAnchor)
            {
                commonReferenceAnchor.position = averageTargetAbsolutePose.position;
                commonReferenceAnchor.rotation = averageTargetAbsolutePose.rotation;
            }
            return ObjectPositionToMoveRelativePoseToTargetAbsolutePose(objectTransform, averageRelativePose, averageTargetAbsolutePose);
        }

        /// <summary>
        /// Return the object pose, so that each of its child relativeReferenceTransforms ends up 
        ///  at the matching absolute pose in the absolutePoses list (after applying this pose to the object)
        /// 
        /// Uses an average pose computation for both (smoothing any incompatibility between the requested positions)
        /// </summary>
        public static (Vector3 newReferencePosition, Quaternion newReferencerotation) ObjectPositionToMoveRelativeReferenceTransformsToTargetAbsolutePoses(Transform objectTransform, 
            List<Transform> relativeReferenceTransforms, List<Pose> absoluteTargetPoses, AverageAnchorAlgorithmSettings? avgSettings = null, bool useTripletAlgorithm = false, Transform commonReferenceAnchor = null)
        {
            var relativePoses = new List<Pose>();
            foreach(var t in relativeReferenceTransforms)
            {
                var relativePose = new Pose(objectTransform.InverseTransformPoint(t.position), Quaternion.Inverse(objectTransform.rotation) * t.rotation);
                relativePoses.Add(relativePose);
            }
            return ObjectPositionToMoveRelativePosesToTargetAbsolutePoses(objectTransform: objectTransform, relativePoses: relativePoses, absoluteTargetPoses: absoluteTargetPoses, avgSettings: avgSettings, useTripletAlgorithm:useTripletAlgorithm, commonReferenceAnchor: commonReferenceAnchor);
        }
    }
}
