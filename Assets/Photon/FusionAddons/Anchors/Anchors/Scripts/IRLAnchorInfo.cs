using Fusion.XR.Shared.Core.Tools;
using UnityEngine;

namespace Fusion.Addons.AnchorsAddon
{
    /// <summary>
    /// Represent a real life marker. 
    /// The detectedIrlAnchorTag is expected to have the real life point position, and to be active only when the position has been detected
    /// </summary>
    [System.Serializable]
    public class IRLAnchorInfo
    {
        public string anchorId;
        // AnchorTag tracking the actual input anchor position: updated in realtime
        public AnchorTag detectedIrlAnchorTag;
        // AnchorTag used to represent the average position (or more exactly, the frame position - either this being average or latest position - that should be used in actual usage of anchors)
        public AnchorTag stabilizedIrlAnchorTag;
        // AnchorTag used to represent the anchor when it has remained stabilized at the same position for a sufficient amount of time (will switch of position based on actual and average position, and stability of the latest)
        public AnchorTag representationIrlAnchorTag;

        public float lastDetectionTime;
        public float previousLastDetectionTime;
        public bool wasDetectedThisFrame;
        public float distanceToViewer;
        public float angleToViewer;
        public float angleYToViewer;
        public float angleXToViewer;
        public Vector3 lastValidPosition;
        public Quaternion lastValidRotation;
        public bool shouldFreezeValidPositioning;
        public bool shouldIgnore;
        // Either the lastValid pose, or the average pose if poseHistory is true
        public Pose stablePose;
        public Pose previousStablePose;
        public Pose representationPose;

        // If a stability threshold is passed  
        public bool isPoseStable = false;
        public bool hasLongStability = false;


        [Header("Pose history")]
        [Tooltip("If true, the anchor stable posed will be computed from an average of the latest detected poses (in the historyDurationRange)")]
        public bool usePoseHistory;
        [Tooltip("If usePoseHistory is true, duration of the analyzed history")]
        public float historyDuration;
        [Header("Long stability criteria")]
        [Tooltip("Duration needed with isPoseStable true to consider that hasLongStability is true")]
        public float expectedDetectedAnchorsStabilityDuration = 2;
        [Tooltip("Max distance between stable pose and last detected pose to consider that hasLongStability can be true (if stability duration criteria is met)")]
        public float maxDetectedToStabilizedDistanceForLongStability = 0.1f;

        public Pose lastValidAveragePose;
        public (Vector3, float) lastDeviation;
        public float longStabilityProgress = 0;

        public float stabilityStartTime = -1;
        RingHistory<Pose> poseHistory;

        public float stablePoseVariationSpeed;

        public struct TimedPose
        {
            public Vector3 position;
            public Quaternion rotation;
            public float time;
        }

        public IRLAnchorInfo(bool usePoseHistory, float historyDuration, int maxFrameRate = 72)
        {
            ChangePoseHistoryDuration(usePoseHistory, historyDuration, maxFrameRate);
        }

        // Store realTimeInputAnchorTag pose as latest valid pose, and store history if usePoseHistory is active
        public void StoreDetectedPosition()
        {
            lastDetectionTime = Time.time;
            if (shouldFreezeValidPositioning == false)
            {
                lastValidPosition = detectedIrlAnchorTag.transform.position;
                lastValidRotation = detectedIrlAnchorTag.transform.rotation;
            }
            if (usePoseHistory)
            {
                poseHistory.Add(new Pose { position = lastValidPosition, rotation = lastValidRotation }, lastDetectionTime);
            }
        }

        public void StorePreviousStates()
        {
            previousStablePose = stablePose;
            previousLastDetectionTime = lastDetectionTime;
        }

        public void UpdateRelativePositionToTheUser(Transform userTransform)
        {
            var positionInHeadsetReferential = userTransform.InverseTransformPoint(detectedIrlAnchorTag.transform.position);
            var positionOnX = positionInHeadsetReferential;
            positionOnX.y = 0;
            var positionOnY = positionInHeadsetReferential;
            positionOnY.x = 0;
            distanceToViewer = Vector3.Distance(userTransform.position, detectedIrlAnchorTag.transform.position);
            angleToViewer = Vector3.Angle(userTransform.forward, detectedIrlAnchorTag.transform.position - userTransform.position);
            angleXToViewer = Vector3.Angle(Vector3.forward, positionOnX);
            angleYToViewer = Vector3.Angle(Vector3.forward, positionOnY);
        }

        public void DetermineStablePose(float positionStabilityThreshold, float rotationStabilityThreshold, float averageSpeedLimitForStability)
        {
            if (usePoseHistory)
            {
                poseHistory.oldestValidTime = Time.time - historyDuration;
                stablePose = AverageValidPose(positionStabilityThreshold, rotationStabilityThreshold);
            }
            else
            {
                // Detected is enough without history
                stablePose = new Pose(lastValidPosition, lastValidRotation);
                // Without history, any detected position is considerd as stable
                MarkAsStable();
            }
            stablePoseVariationSpeed = Vector3.Distance(previousStablePose.position, stablePose.position) / Time.deltaTime;
            if (isPoseStable && averageSpeedLimitForStability != -1 && stablePoseVariationSpeed > averageSpeedLimitForStability)
            {
                StopBeingStable();
            }
        }

        public Pose AverageValidPose(float positionStabilityThreshold, float rotationStabilityThreshold)
        {
            Pose pose = new Pose(lastValidPosition, lastValidRotation);
            try
            {
                // TODO Use a progressive computation instead to decrease the computation cost -- for test purposes only here
                pose = AnchorsManipulation.AverageAnchorPose(poseHistory);
            }
            catch
            {
                // 0 entries in the history: we keep the last valid pose
            }

            lastValidAveragePose = pose;

            // determine deviation between average and history position
            lastDeviation = AnchorsManipulation.AverageDeviationToMeanPosition(poseHistory, pose);

            if (positionStabilityThreshold != -1 && positionStabilityThreshold < lastDeviation.Item1.magnitude)
            {
                // Position not stable
                StopBeingStable();
            }
            else if (rotationStabilityThreshold != -1 && rotationStabilityThreshold < lastDeviation.Item2)
            {
                // Rotation not stable
                StopBeingStable();
            }
            else
            {
                MarkAsStable();
            }

            return pose;
        }

        public void MarkAsStable()
        {
            isPoseStable = true;
            if (stabilityStartTime == -1) stabilityStartTime = Time.time;
            EvaluateLongStabilityDuration();
        }

        public void StopBeingStable()
        {
            isPoseStable = false;
            hasLongStability = false;
            longStabilityProgress = 0;
            stabilityStartTime = -1;
        }

        public void ChangeLongStabilityCriterias(float duration, float distance)
        {
            expectedDetectedAnchorsStabilityDuration = duration;
            maxDetectedToStabilizedDistanceForLongStability = distance;
            hasLongStability = false;
            longStabilityProgress = 0;
        }

        public void ChangePoseHistoryDuration(bool usePoseHistory, float historyDuration, int maxFrameRate = 72)
        {
            this.usePoseHistory = usePoseHistory;
            int requiredEntries = 0;
            if (usePoseHistory)
                requiredEntries = maxFrameRate * (int)Mathf.Ceil(historyDuration);
            poseHistory = new RingHistory<Pose>(requiredEntries);
            this.historyDuration = historyDuration;
        }

        public void InvalidatePoseHistory()
        {
            poseHistory = new RingHistory<Pose>(poseHistory.size);
            StopBeingStable();
        }

        public void EvaluateLongStabilityDuration()
        {
            var stabilityDuration = isPoseStable ? Time.time - stabilityStartTime : 0;
            
            hasLongStability = isPoseStable && (stabilityDuration >= expectedDetectedAnchorsStabilityDuration);
            longStabilityProgress = (hasLongStability || expectedDetectedAnchorsStabilityDuration == 0) ? 1 : Mathf.Clamp01(stabilityDuration / expectedDetectedAnchorsStabilityDuration);

            var isDetectedPositionOutOfRange = maxDetectedToStabilizedDistanceForLongStability != 0 && Vector3.Distance(detectedIrlAnchorTag.transform.position, stablePose.position) > maxDetectedToStabilizedDistanceForLongStability;
            if (isDetectedPositionOutOfRange)
            {
                // Just after a rig repositioning, the anchor can be stable, in its position before the user repositioning,
                //  but the real life tracking is already at the new position: it is an hint that we are not anymore in a 
                //  situation where the stable pose can be trusted
                hasLongStability = false;
            }
        }
    }
}