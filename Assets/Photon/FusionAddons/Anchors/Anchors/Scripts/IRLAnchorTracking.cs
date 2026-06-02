using Fusion.XR.Shared.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Fusion.Addons.AnchorsAddon
{
    /// <summary>
    /// Track real life anchor position and provide a stabilized version of it (alongside a representation version that "jumps" less).
    /// Detect each frames if their game object is active, compute average position and stability evaluation
    /// Notify listeners of detection start/end and of detected anchors during analysis.
    /// 
    /// Anchor detection can be either automatic, or be called on demand
    /// </summary>
    [DefaultExecutionOrder(10_000)]
    public class IRLAnchorTracking : MonoBehaviour
    {
        public interface IIRLAnchorTrackingListener
        {
            void OnIRLAnchorSpawn(IRLAnchorTracking irlAnchorTracking, string anchorId);
            void OnIRLAnchorDetectedThisFrame(IRLAnchorTracking irlAnchorTracking, IRLAnchorInfo anchorInfo);
            void OnDetectionStarted(IRLAnchorTracking irlAnchorTracking);
            void OnDetectionFinished(IRLAnchorTracking irlAnchorTracking);
        }

        public enum DetectionTriggerMode
        {
            DuringLateUpdate,
            OnDemand
        }

        public DetectionTriggerMode detectionTriggerMode = DetectionTriggerMode.DuringLateUpdate;

        [Header("Anchors")]
        public List<IRLAnchorInfo> irlAnchorsInfo = new List<IRLAnchorInfo>();

        [Header("Predefined anchors")]
        public List<string> predefinedIrlAnchorIds = new List<string> { "0", "1", "2", "3", "4", "5" };
        public string predefinedIrlAnchorsPrefix = "";

        [Header("Anchors prefab")]
        public GameObject detectedIrlAnchorTagPrefab;
        public GameObject stabilizedIrlAnchorTagPrefab;
        public GameObject representationIrlAnchorTagPrefab;

        [Header("Algorithm settings - Pose history and stability criteria")]
        [Tooltip("If true, the anchor stable posed will be computed from an average of the latest detected poses (in the historyDurationRange)")]
        public bool usePoseHistory = true;
        [Tooltip("If usePoseHistory is true, duration of the analyzed history")]
        public int historyDuration = 4;
        [Tooltip("Duration needed with isPoseStable true to consider that hasLongStability is true")]
        public float expectedDetectedAnchorsStabilityDuration = 2;
        [Tooltip("Max distance between stable pose and last detected pose to consider that hasLongStability can be true (if stability duration criteria is met)")]
        public float maxDetectedToStabilizedDistanceForLongStability = 0.1f;

        [Tooltip("Allows, alongside historyDuration, to compute the required size of history entries")]
        public int maxFps = 72;
        [Tooltip("Length of the average history position deviation (computed with AverageDeviationToMeanPosition as the average distance to the mean position), under which the stabilized position (aka the 'mean' position) is considered stable")]
        public float positionStabilityThreshold = 0.05f;
        [Tooltip("Angle of the average history rotation deviation (computed with AverageDeviationToMeanPosition as the average angle to the mean rotation), under which the stabilized rotation (aka the 'mean' rotation) is considered stable")]
        public float rotationStabilityThreshold = 5f;
        // Note: Non -1 values lead to high difficulties to have a stable anchor (requires a very steady position)
        public float averageSpeedLimitForStability = -1;

        [Header("Anchors representation settings")]
        public bool disableIrlAnchorsAtStart = true;
        public bool displayStabilizedAnchor = true;
        public bool displayRepresentationAnchor = true;
        [Tooltip("Smoothing rate of the representation anchor move toward the detected position when the stabilized anchor is not yet stable")]
        public float representationConvergenceToRealtimePoseFactor = 0.05f;
        [Tooltip("Smoothing rate of the representation anchor move toward the stabilized position when the stabilized anchor is stable")]
        public float representationConvergenceToStablePoseFactor = 0.15f;
        [Tooltip("Above this distance, the representation anchor will jump to the stabilized anchor position")]
        public float representationTeleport = 0.5f;
        public float stabilitydetectabilityRangeScale = 30;

        [Header("Spawned anchors")]
        public Transform spawnedDetectedIrlAnchorRoot;
        public Transform spawnedStabilizedIrlAnchorRoot;
        public Transform spawnedRepresentationIrlAnchorRoot;
        public int minimalSpawnedIrlAnchorId = 0;

        [Header("Event")]
        public UnityEvent<int> OnDetectedAnchorsCountChanged;
        private int previousDetectedAnchorsCount = 0;


        IHardwareRig hardwareRig;
        [HideInInspector]
        public List<GameObject> spawnedIrlAnchors = new List<GameObject>();

        [HideInInspector]
        public List<IIRLAnchorTrackingListener> listeners = new List<IIRLAnchorTrackingListener>();

        bool _lastUsePoseHistory = false;
        float _lastHistoryDuration = 4;
        float _lastStabilityDuration = -1;
        int _lastMaxFps = 72;
        float _lastMaxLongStabilityDistanceToDetected = -1;

        public Dictionary<string, IRLAnchorInfo> irlAnchorsInfoById = new Dictionary<string, IRLAnchorInfo>();
        [HideInInspector] public int detectedAnchorsCount = 0;

        public bool invalidateStabilityHistoryOnRigMove = true;

        Vector3 lastRigPosition;
        Quaternion lastRigRotation;

        private void Awake()
        {
            listeners.AddRange(GetComponentsInChildren<IIRLAnchorTrackingListener>());

            if (spawnedDetectedIrlAnchorRoot == null)
            {
                var root = new GameObject("SpawnedDetectedIRLAnchorRoot");
                root.transform.parent = transform;
                root.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                spawnedDetectedIrlAnchorRoot = root.transform;
            }
            if (spawnedStabilizedIrlAnchorRoot == null)
            {
                var root = new GameObject("SpawnedStabilizedIRLAnchorRoot");
                root.transform.parent = transform;
                root.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                spawnedStabilizedIrlAnchorRoot = root.transform;
            }
            if (spawnedRepresentationIrlAnchorRoot == null)
            {
                var root = new GameObject("SpawnedRepresentationIRLAnchorRoot");
                root.transform.parent = transform;
                root.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                spawnedRepresentationIrlAnchorRoot = root.transform;
            }

            ClearIRLAnchors();
            SpawnPredefinedAnchors();
        }

        public void SpawnPredefinedAnchors()
        {
            foreach (var anchorId in predefinedIrlAnchorIds)
            {
                SpawnIRLAnchor(predefinedIrlAnchorsPrefix+anchorId);
            }
        }

        public void ClearIRLAnchors()
        {
            irlAnchorsInfoById.Clear();
            irlAnchorsInfo.Clear();
            if (spawnedIrlAnchors.Count > 0)
            {
                for (int i = spawnedIrlAnchors.Count - 1; i >= 0; i--)
                {
                    GameObject.Destroy(spawnedIrlAnchors[i]);
                }
                spawnedIrlAnchors.Clear();
            }
        }

        [ContextMenu("SpawnActiveNextIRLAnchor")]
        public void SpawnActiveNextIRLAnchor()
        {
            SpawnNextIRLAnchor(overrideDisableAtStart: false);
        }

        public GameObject SpawnNextIRLAnchor(bool? overrideDisableAtStart = null)
        {
            int maxId = minimalSpawnedIrlAnchorId - 1;
            foreach (var w in irlAnchorsInfo)
            {
                var anchorId = Int32.Parse(w.anchorId);
                if (anchorId > maxId)
                {
                    maxId = anchorId;
                }
            }
            return SpawnIRLAnchor($"{predefinedIrlAnchorsPrefix}{(maxId + 1)}", overrideDisableAtStart);
        }

        public GameObject SpawnIRLAnchor(string anchorId, bool? overrideDisableAtStart = null)
        {
            if (detectedIrlAnchorTagPrefab)
            {
                // Spawn core anchor (that tracks actual anchor position, unless freezed or ignored)
                var detectedAnchorGO = GameObject.Instantiate(detectedIrlAnchorTagPrefab);
                detectedAnchorGO.name = "Detected-" + anchorId;
                detectedAnchorGO.transform.parent = spawnedDetectedIrlAnchorRoot;
                spawnedIrlAnchors.Add(detectedAnchorGO);
                var a = detectedAnchorGO.GetComponentInChildren<AnchorTag>();
                a.anchorId = anchorId;

                return RegisterIRLAnchor(a, overrideDisableAtStart);
            }

            return null;
        }

        public void UnregisterIRLAnchor(AnchorTag anchorTag)
        {
            if (irlAnchorsInfoById.ContainsKey(anchorTag.anchorId))
            {
                var anchorInfo = irlAnchorsInfoById[anchorTag.anchorId];
                if (anchorInfo.stabilizedIrlAnchorTag)
                {
                    Destroy(anchorInfo.stabilizedIrlAnchorTag.gameObject);
                }
                if (anchorInfo.representationIrlAnchorTag)
                {
                    Destroy(anchorInfo.representationIrlAnchorTag.gameObject);
                }
                irlAnchorsInfo.Remove(anchorInfo);
                irlAnchorsInfoById.Remove(anchorTag.anchorId);
            }
        }

        public GameObject RegisterIRLAnchor(AnchorTag anchorTag, bool? overrideDisableAtStart = null)
        {
            AnchorTag matchingStabilizedAnchor = null;
            AnchorTag matchingRepresentationAnchor = null;

            // Spawn stabilized anchor (used to represent average position - or more exactly, frame position, either this being average or latest position)
            if (stabilizedIrlAnchorTagPrefab)
            {
                var stabilizedIrlAnchorGO = GameObject.Instantiate(stabilizedIrlAnchorTagPrefab);
                stabilizedIrlAnchorGO.name = "Stabilized-" + anchorTag.anchorId;
                stabilizedIrlAnchorGO.transform.parent = spawnedStabilizedIrlAnchorRoot;
                spawnedIrlAnchors.Add(stabilizedIrlAnchorGO);
                matchingStabilizedAnchor = stabilizedIrlAnchorGO.GetComponentInChildren<AnchorTag>();
                matchingStabilizedAnchor.anchorId = anchorTag.anchorId;
                matchingStabilizedAnchor.referenceAnchor = anchorTag;
            }

            // Spawn representation anchor
            if (representationIrlAnchorTagPrefab)
            {
                var representationIrlAnchorGO = GameObject.Instantiate(representationIrlAnchorTagPrefab);
                representationIrlAnchorGO.name = "Representation-" + anchorTag.anchorId;
                representationIrlAnchorGO.transform.parent = spawnedRepresentationIrlAnchorRoot;
                spawnedIrlAnchors.Add(representationIrlAnchorGO);
                matchingRepresentationAnchor = representationIrlAnchorGO.GetComponentInChildren<AnchorTag>();
                matchingRepresentationAnchor.anchorId = anchorTag.anchorId;
                matchingRepresentationAnchor.referenceAnchor = anchorTag;
            }
            RegisterIRLAnchor(anchorTag, matchingStabilizedAnchor, matchingRepresentationAnchor, overrideDisableAtStart);
            return anchorTag.gameObject;
        }

        void RegisterIRLAnchor(AnchorTag detectedAnchor, AnchorTag matchingStabilizedAnchor, AnchorTag matchingRepresentationAnchor, bool? overrideDisableAtStart = null)
        {
            bool disableAnchors = disableIrlAnchorsAtStart;
            if (overrideDisableAtStart is bool overrideDisable)
            {
                disableAnchors = overrideDisable;
            }

            irlAnchorsInfoById[detectedAnchor.anchorId] = new IRLAnchorInfo(usePoseHistory, historyDuration) { 
                anchorId = detectedAnchor.anchorId, 
                detectedIrlAnchorTag = detectedAnchor, stabilizedIrlAnchorTag = matchingStabilizedAnchor, representationIrlAnchorTag = matchingRepresentationAnchor,
                usePoseHistory = usePoseHistory, historyDuration = historyDuration, 
                expectedDetectedAnchorsStabilityDuration = expectedDetectedAnchorsStabilityDuration,
                maxDetectedToStabilizedDistanceForLongStability = maxDetectedToStabilizedDistanceForLongStability
            };
            irlAnchorsInfo.Add(irlAnchorsInfoById[detectedAnchor.anchorId]);

            detectedAnchor.gameObject.SetActive(disableAnchors == false);
            if(matchingStabilizedAnchor) matchingStabilizedAnchor.gameObject.SetActive(disableAnchors == false);
            if(matchingRepresentationAnchor) matchingRepresentationAnchor.gameObject.SetActive(disableAnchors == false);
                        
            foreach (var listener in listeners)
            {
                listener.OnIRLAnchorSpawn(this, detectedAnchor.anchorId);
            }
        }

        /// <summary>
        /// Launch IRL anchors presence check.
        /// Should be called manually (when relevent, aka after activating/desactivating anchor and positioning the active ones) if detectionTriggerMode is set to DetectionTriggerMode.OnDemand
        /// </summary>
        public void DetectValidAnchors()
        {
            if (enabled == false) return;

            if (invalidateStabilityHistoryOnRigMove && hardwareRig != null)
            {
                if (Vector3.Distance(hardwareRig.transform.position, lastRigPosition) > 0.01f || Quaternion.Angle(hardwareRig.transform.rotation, lastRigRotation) > 1f)
                {
                    // Rig moved, we can't trust anchor history (in world space) anymore
                    foreach (var irlAnchor in irlAnchorsInfoById.Values)
                    {
                        irlAnchor.InvalidatePoseHistory();
                    }
                }
            }
            if (hardwareRig == null)
            {
                hardwareRig = HardwareRigsRegistry.GetHardwareRig();
            }
            if (hardwareRig == null)
            {
                Debug.LogError("No hardwareRig");
                return;
            }
            lastRigPosition = hardwareRig.transform.position;
            lastRigRotation = hardwareRig.transform.rotation;

            previousDetectedAnchorsCount = detectedAnchorsCount;
            detectedAnchorsCount = 0;

            foreach (var l in listeners) l.OnDetectionStarted(this);

            foreach (var tagId in irlAnchorsInfoById.Keys)
            {
                var irlAnchor = irlAnchorsInfoById[tagId];

                irlAnchor.StorePreviousStates();

                if (irlAnchor.shouldIgnore)
                {
                    irlAnchor.StopBeingStable();
                    continue;
                }

                // An anchor is detected when its detectedIRLAnchorTag gameobject is active during DetectValidAnchors
                irlAnchor.wasDetectedThisFrame = irlAnchor.detectedIrlAnchorTag.IsDetected;

                if (irlAnchor.wasDetectedThisFrame)
                {
                    detectedAnchorsCount++;
                    // Determine relative position to the use (can be use to evaluate anchor "quality")
                    irlAnchor.UpdateRelativePositionToTheUser(hardwareRig.Headset.transform);

                    // Valid pose determination
                    irlAnchor.StoreDetectedPosition();

                    // Determination of the anchor pose that should be used for this frame
                    irlAnchor.DetermineStablePose(positionStabilityThreshold, rotationStabilityThreshold, averageSpeedLimitForStability);
                }
                else
                {
                    // Not detected
                    irlAnchor.StopBeingStable();
                }

                UpdateAnchorDisplay(irlAnchor);

                if (irlAnchor.wasDetectedThisFrame)
                {
                    // Warn listener that this anchor was detected this frame
                    foreach (var l in listeners) l.OnIRLAnchorDetectedThisFrame(this, irlAnchor);
                }
            }

            foreach (var l in listeners) l.OnDetectionFinished(this);

            if (previousDetectedAnchorsCount != detectedAnchorsCount)
            {
                Debug.Log("Nb of tag detected change");
                if (OnDetectedAnchorsCountChanged != null) OnDetectedAnchorsCountChanged.Invoke(detectedAnchorsCount);

            }
        }

        protected virtual void UpdateAnchorDisplay(IRLAnchorInfo anchorInfo)
        {
            anchorInfo.detectedIrlAnchorTag.SetDetailText("");
            anchorInfo.stabilizedIrlAnchorTag?.SetDetailText("");
            anchorInfo.representationIrlAnchorTag?.SetDetailText("");
            anchorInfo.detectedIrlAnchorTag.ResetDetailTextColor();
            anchorInfo.stabilizedIrlAnchorTag?.ResetDetailTextColor();
            anchorInfo.representationIrlAnchorTag?.ResetDetailTextColor();

            bool hasBeenDetected = anchorInfo.lastDetectionTime > 0;
            float recentlyDetectedThresholdDuration = usePoseHistory ? historyDuration : 2;
            bool recentlyDetected = hasBeenDetected && ((Time.time - anchorInfo.lastDetectionTime) < recentlyDetectedThresholdDuration);

            (var lastPositionDeviation, var lastAngleDeviation) = anchorInfo.lastDeviation;

            float positionStability = 1;
            float rotationStability = 1;
            float stability = 1;
            if (anchorInfo.isPoseStable == false)
            {
                stabilitydetectabilityRangeScale = Math.Max(stabilitydetectabilityRangeScale, 2);
                if (positionStabilityThreshold != -1)
                {
                    positionStability = Mathf.Clamp01((((stabilitydetectabilityRangeScale - 1f) * positionStabilityThreshold - (lastPositionDeviation.magnitude - positionStabilityThreshold))) / ((stabilitydetectabilityRangeScale - 1f) * positionStabilityThreshold));
                }
                if (rotationStabilityThreshold != -1)
                {
                    rotationStability = Mathf.Clamp01((((stabilitydetectabilityRangeScale - 1f) * rotationStabilityThreshold - (lastAngleDeviation - rotationStabilityThreshold))) / ((stabilitydetectabilityRangeScale - 1f) * rotationStabilityThreshold));
                }
                stability = Mathf.Min(positionStability, rotationStability);
                if (averageSpeedLimitForStability != -1)
                {
                    var averageSpeedStability = Mathf.Clamp01((((stabilitydetectabilityRangeScale - 1f) * averageSpeedLimitForStability - (anchorInfo.stablePoseVariationSpeed - averageSpeedLimitForStability))) / ((stabilitydetectabilityRangeScale - 1f) * averageSpeedLimitForStability));
                    var baseStability = stability;
                    stability = 0.95f * baseStability + 0.05f * averageSpeedStability;
                }
            }

            if (anchorInfo.wasDetectedThisFrame)
            {
                anchorInfo.stabilizedIrlAnchorTag?.gameObject.SetActive(displayStabilizedAnchor);
                anchorInfo.representationIrlAnchorTag?.gameObject.SetActive(displayRepresentationAnchor);

                if (anchorInfo.isPoseStable)
                {
                    anchorInfo.representationIrlAnchorTag?.SetDetailText($"Stable");

                    if(anchorInfo.hasLongStability)
                    {
                        anchorInfo.representationIrlAnchorTag?.SetDetailText($"Long stability");
                    }
                    
                    if (anchorInfo.previousLastDetectionTime == -1 || Vector3.Distance(anchorInfo.representationPose.position, anchorInfo.stablePose.position) > representationTeleport)
                    {
                        // First detection or too far away from previous representation position
                        anchorInfo.representationPose = anchorInfo.stablePose;
                    }
                    else
                    {
                        anchorInfo.representationPose = new Pose(
                            Vector3.Lerp(anchorInfo.representationPose.position, anchorInfo.stablePose.position, representationConvergenceToStablePoseFactor),
                            Quaternion.Slerp(anchorInfo.representationPose.rotation, anchorInfo.stablePose.rotation, representationConvergenceToStablePoseFactor)
                        );
                    }

                }
                else
                {
                    anchorInfo.representationIrlAnchorTag?.SetDetailText($"Wait for average position stability {(stability * 100f):00}%...");

                    if (anchorInfo.previousLastDetectionTime == -1 || Vector3.Distance(anchorInfo.representationPose.position, anchorInfo.lastValidPosition) > representationTeleport)
                    {
                        // First detection or too far away from previous representation position
                        anchorInfo.representationPose = new Pose(anchorInfo.lastValidPosition, anchorInfo.lastValidRotation);
                    }
                    else
                    {

                        anchorInfo.representationPose = new Pose(
                            Vector3.Lerp(anchorInfo.representationPose.position, anchorInfo.lastValidPosition, representationConvergenceToRealtimePoseFactor),
                            Quaternion.Slerp(anchorInfo.representationPose.rotation, anchorInfo.lastValidRotation, representationConvergenceToRealtimePoseFactor)
                        );
                    }
                }

                anchorInfo.stabilizedIrlAnchorTag?.transform.SetPositionAndRotation(anchorInfo.stablePose.position, anchorInfo.stablePose.rotation);
                anchorInfo.representationIrlAnchorTag?.transform.SetPositionAndRotation(anchorInfo.representationPose.position, anchorInfo.representationPose.rotation);

            }
            else
            {
                anchorInfo.representationIrlAnchorTag?.SetDetailText("Tag not detected");
                anchorInfo.representationIrlAnchorTag?.SetDetailText($"Tag not detected");
                anchorInfo.representationIrlAnchorTag?.SetDetailText($"Tag not detected");

                anchorInfo.stabilizedIrlAnchorTag?.gameObject.SetActive(displayStabilizedAnchor && recentlyDetected);
                anchorInfo.representationIrlAnchorTag?.gameObject.SetActive(displayRepresentationAnchor && recentlyDetected);
            }

            // Detailed text
            if (anchorInfo.shouldIgnore)
            {
                anchorInfo.detectedIrlAnchorTag.SetDetailText("Ignored");
                anchorInfo.stabilizedIrlAnchorTag?.gameObject.SetActive(false);
                anchorInfo.representationIrlAnchorTag?.gameObject.SetActive(false);
            }
            else if (anchorInfo.shouldFreezeValidPositioning)
            {
                anchorInfo.detectedIrlAnchorTag.SetDetailText("Frozen");
                anchorInfo.representationIrlAnchorTag.SetDetailText("Frozen");
                anchorInfo.stabilizedIrlAnchorTag?.gameObject.SetActive(displayStabilizedAnchor && recentlyDetected);
                anchorInfo.representationIrlAnchorTag?.gameObject.SetActive(displayRepresentationAnchor && recentlyDetected);
            }

            // Stability progress
            anchorInfo.representationIrlAnchorTag?.SetAnchorSecondaryProgress(stability);

            anchorInfo.representationIrlAnchorTag?.SetAnchorProgress(anchorInfo.longStabilityProgress);
        }

        // Upate all anchors info based on inspector changes (for debugging purposes)
        void ApplySettingsToAnchorsInfo()
        {
            bool changeAnchorsHistory = false;
            if (_lastUsePoseHistory != usePoseHistory)
            {
                changeAnchorsHistory = true;
                _lastUsePoseHistory = usePoseHistory;
            }

            if (_lastHistoryDuration != historyDuration)
            {
                changeAnchorsHistory = true;

                _lastHistoryDuration = historyDuration;
            }

            if (_lastMaxFps != maxFps)
            {
                changeAnchorsHistory = true;

                _lastMaxFps = maxFps;
            }

            if (_lastStabilityDuration != expectedDetectedAnchorsStabilityDuration || _lastMaxLongStabilityDistanceToDetected != maxDetectedToStabilizedDistanceForLongStability)
            {
                _lastStabilityDuration = expectedDetectedAnchorsStabilityDuration;
                _lastMaxLongStabilityDistanceToDetected = maxDetectedToStabilizedDistanceForLongStability;
                foreach (var w in irlAnchorsInfo)
                {
                    w.ChangeLongStabilityCriterias(expectedDetectedAnchorsStabilityDuration, maxDetectedToStabilizedDistanceForLongStability);
                }
            }

            if (changeAnchorsHistory)
            {
                foreach (var w in irlAnchorsInfo)
                {
                    w.ChangePoseHistoryDuration(usePoseHistory, historyDuration, maxFps);
                }
            }
        }
        
        private void Update()
        {
            ApplySettingsToAnchorsInfo();
        }

        private void LateUpdate()
        {
            if(detectionTriggerMode == DetectionTriggerMode.DuringLateUpdate)
            {
                DetectValidAnchors();
            }
        }
    }
}


