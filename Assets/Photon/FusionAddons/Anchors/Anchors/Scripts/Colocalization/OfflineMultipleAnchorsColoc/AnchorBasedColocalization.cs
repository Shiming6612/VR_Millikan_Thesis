using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Utils;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Fusion.Addons.AnchorsAddon
{
    /// <summary>
    /// Alternative coloc implementation, that does not rely on anchor positioning over the network.
    /// It it notably relevant if the reference anchors positions in the virtual scene is hardcoded (LBE, ..)
    /// </summary>
    [RequireComponent(typeof(IRLAnchorTracking))]
    [DefaultExecutionOrder(10_000)]
    public class AnchorBasedColocalization : MonoBehaviour, IRLAnchorTracking.IIRLAnchorTrackingListener
    {
        [Header("Reference anchors")]
        public Transform referenceAnchorsContainer;
        public List<AnchorTag> localAnchors = new List<AnchorTag>();

        Dictionary<string, AnchorTag> localAnchorTagsById = new Dictionary<string, AnchorTag>();

        List<Transform> validLocalAnchorTransforms = new List<Transform>();
        List<IRLAnchorInfo> validWorldAnchors = new List<IRLAnchorInfo>();

        public UnityEvent onStateChange = new UnityEvent();

        public bool autofixGroundPosition = true;

        public IRLAnchorTracking irlAnchorTracking;

        public enum State
        {
            Inactive,
            LookingForColocalization,
            ColocalizationFound
        }
        [Header("Colocalization Status")]
        public State state = State.LookingForColocalization;

        [Header("Colocalization options")]
        public bool allowColocalizationUpdate = true;
        public float minPositionChangeForUpdate = 0.1f;
        public float minAngleChangeForUpdate = 5f;
        public bool useTripletAlgorithm = false;
        public float minimumDelayBeforeColocalizationUpdate = 5;
        public bool resetWorldAnchorsStabilityOnColocalization = false;

        public float lastColocation = -1;

        [System.Serializable]
        public struct AnchorStabilityState
        {
            public string anchorId;
            public float stabilityDuration;
            public bool hasLongStability;
        }

        [System.Serializable]
        public struct StabilityState
        {
            public float expectedDetectedAnchorsStabilityDuration;
            public List<AnchorStabilityState> anchorsStabilityStates;
            public int longStabilityAnchorsCount;
            public float positionChange;
            public float minPositionChangeForUpdate;
            public float angleChange;
            public float minAngleChangeForUpdate;
            public bool colocalizationUpdateChecked;
        }

        public StabilityState stabilityState = new StabilityState { expectedDetectedAnchorsStabilityDuration = 2, anchorsStabilityStates = new List<AnchorStabilityState>() };

        private void Awake()
        {
            if(irlAnchorTracking == null) irlAnchorTracking = GetComponent<IRLAnchorTracking>();

            UpdateReferenceAnchors();
            stabilityState = new StabilityState { expectedDetectedAnchorsStabilityDuration = 2, anchorsStabilityStates = new List<AnchorStabilityState>()};
        }
        
        public void UpdateReferenceAnchors()
        {
            if (referenceAnchorsContainer)
            {
                localAnchors = new List<AnchorTag>();
                foreach (var anchorTag in referenceAnchorsContainer.GetComponentsInChildren<AnchorTag>())
                {
                    localAnchors.Add(anchorTag);
                }

                localAnchorTagsById.Clear();
                foreach (var a in localAnchors) localAnchorTagsById[a.anchorId] = a;
            }
        }

        public void AddReferenceAnchor(AnchorTag anchor)
        {
            localAnchors.Add(anchor);
            localAnchorTagsById[anchor.anchorId] = anchor;
        }

        public void RemoveReferenceAnchor(AnchorTag anchor)
        {
            localAnchors.Remove(anchor);
            localAnchorTagsById.Remove(anchor.anchorId);
        }

        #region WorldAnchorTracking.IAnchorBasedObjectSynchronizationListener
        public void OnIRLAnchorSpawn(IRLAnchorTracking worldAnchorTracking, string anchorId)
        {
        }

        public void OnIRLAnchorDetectedThisFrame(IRLAnchorTracking worldAnchorTracking, IRLAnchorInfo anchor)
        {
            if (localAnchorTagsById.ContainsKey(anchor.anchorId) && localAnchorTagsById[anchor.anchorId].enabled)
            {
                validWorldAnchors.Add(anchor);
                validLocalAnchorTransforms.Add(localAnchorTagsById[anchor.anchorId].transform);
            } 
            else
            {
                if (anchor.representationIrlAnchorTag != null)
                {
                    if(anchor.representationIrlAnchorTag.detailText) anchor.representationIrlAnchorTag.detailText.color = Color.red;
                    anchor.representationIrlAnchorTag.SetDetailText("/!\\ Anchor not used for current room calibration /!\\");
                }
            }
        }

        public void OnDetectionStarted(IRLAnchorTracking worldAnchorTracking)
        {
            validLocalAnchorTransforms.Clear();
            validWorldAnchors.Clear();
        }

        public void OnDetectionFinished(IRLAnchorTracking worldAnchorTracking)
        {
            stabilityState.anchorsStabilityStates.Clear();
            stabilityState.longStabilityAnchorsCount = 0;
            stabilityState.colocalizationUpdateChecked = false;

            if (enabled != false && state != State.Inactive)
            {
                bool completeStability = true;
                List<Pose> worldAnchorPoses = new List<Pose>();
                List<Pose> localAnchorPoses = new List<Pose>();
                foreach (var validWorldAnchor in validWorldAnchors)
                {
                    if (validWorldAnchor.hasLongStability == false)
                    {
                        completeStability = false;
                    }
                    else
                    {
                        stabilityState.longStabilityAnchorsCount++;
                    }
                    stabilityState.anchorsStabilityStates.Add(new AnchorStabilityState { anchorId = validWorldAnchor.anchorId, hasLongStability = validWorldAnchor.hasLongStability, stabilityDuration = validWorldAnchor.longStabilityProgress * validWorldAnchor.expectedDetectedAnchorsStabilityDuration });
                    //TODO Check if we should check that the last position is not too changing relatively to the average
                    //Debug.LogError("Last valid position error: " + (validWorldAnchor.lastValidPosition - validWorldAnchor.framePose.position).magnitude);
                    worldAnchorPoses.Add(validWorldAnchor.stablePose);
                }
                foreach (var t in validLocalAnchorTransforms)
                {
                    var localAnchor = new Pose(t.position, t.rotation);
                    localAnchorPoses.Add(localAnchor);
                }

                if (completeStability && localAnchorPoses.Count > 0 && worldAnchorPoses.Count > 0)
                {
                    if (state == State.LookingForColocalization)
                    {
                        Colocalize(worldAnchorPoses, localAnchorPoses);
                        if(resetWorldAnchorsStabilityOnColocalization) ResetValidWorldAnchorsStability();
                    }
                    else if (state == State.ColocalizationFound && allowColocalizationUpdate)
                    {
                        bool canUpdate = minimumDelayBeforeColocalizationUpdate == -1 || ((Time.time - lastColocation) > minimumDelayBeforeColocalizationUpdate);
                        // If colocalization is found 
                        if (canUpdate && TryColocalizationUpdate(worldAnchorPoses, localAnchorPoses))
                        {
                            if (resetWorldAnchorsStabilityOnColocalization) ResetValidWorldAnchorsStability();
                        }
                    }
                }
            }
            if (onStateChange != null) onStateChange.Invoke();
        }

        void ResetValidWorldAnchorsStability()
        {
            // reset stability to avoid being reusable immediatly
            foreach (var validWorldAnchor in validWorldAnchors)
            {
                validWorldAnchor.StopBeingStable();
            }
        }
        
        protected virtual bool TryColocalizationUpdate(List<Pose> worldAnchorPoses, List<Pose> localAnchorPoses)
        {
            (var rigPosition, var rigRotation) = FindNewrigPose(worldAnchorPoses, localAnchorPoses);
            var rig = HardwareRigsRegistry.GetHardwareRig();
            bool updateRigPosition = false;
            var positionChange = Vector3.Distance(rig.transform.position, rigPosition);
            var angleChange = Quaternion.Angle(rig.transform.rotation, rigRotation);
            stabilityState.colocalizationUpdateChecked = true;
            stabilityState.positionChange = positionChange;
            stabilityState.minPositionChangeForUpdate = minPositionChangeForUpdate;
            stabilityState.angleChange = angleChange;
            stabilityState.minAngleChangeForUpdate = minAngleChangeForUpdate;
            if (minPositionChangeForUpdate >= 0 && positionChange > minPositionChangeForUpdate)
            {
                updateRigPosition = true;
            }
            if (minAngleChangeForUpdate >= 0 && angleChange > minAngleChangeForUpdate)
            {
                updateRigPosition = true;
            }
            if (updateRigPosition)
            {
                Debug.LogError($"=> Updating position positionChange: {positionChange} / angleChange: {angleChange}");
                ApplyColocationPose(rig.transform, rigPosition, rigRotation);
                return true;
            }
            return false;
        }

        void ApplyColocationPose(Transform rigTransform, Vector3 rigPosition, Quaternion rigRotation)
        {
            rigTransform.rotation = rigRotation;
            rigTransform.position = rigPosition;
            state = State.ColocalizationFound;
            lastColocation = Time.time;
        }

        protected virtual (Vector3 rigPosition, Quaternion rigRotation) FindNewrigPose(List<Pose> worldAnchorPoses, List<Pose> localAnchorPoses)
        {
            Pose averageLocalAnchor;
            Pose averageWorldAnchor;
            if (useTripletAlgorithm)
            {
                averageLocalAnchor = AnchorsManipulation.TripletRepresentativeAnchorPose(localAnchorPoses);
                averageWorldAnchor = AnchorsManipulation.TripletRepresentativeAnchorPose(worldAnchorPoses);
            }
            else
            {
                averageLocalAnchor = AnchorsManipulation.AverageAnchorPose(localAnchorPoses);
                averageWorldAnchor = AnchorsManipulation.AverageAnchorPose(worldAnchorPoses);
            }
            var rig = HardwareRigsRegistry.GetHardwareRig();
            (var rigPosition, var rigRotation) = TransformManipulations.DetermineNewRigPositionToMovePositionToTargetPosition(
                    averageWorldAnchor.position, averageWorldAnchor.rotation,
                    averageLocalAnchor.position, averageLocalAnchor.rotation,
                    rig.transform,
                    rig.Headset.transform,
                    ignoreYAxisMove: !autofixGroundPosition, keepUpDirection: true
            );
            return (rigPosition, rigRotation);
        }

        protected virtual void Colocalize(List<Pose> worldAnchorPoses, List<Pose> localAnchorPoses)
        {
            (var rigPosition, var rigRotation) = FindNewrigPose(worldAnchorPoses, localAnchorPoses);
            var rig = HardwareRigsRegistry.GetHardwareRig();
            Debug.LogError("=> Running colocalisation");
            ApplyColocationPose(rig.transform, rigPosition, rigRotation);
        }
        #endregion
    }
}
