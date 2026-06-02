using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Fusion.Addons.AnchorsAddon
{
    /// <summary>
    /// AnchorBasedObjectSynchronization is in charge to update an object position based on an detected anchor position.
    /// It implements the IIRLAnchorTrackingListener interface to be notified when an anchor status was updated.
    /// It provides the method to handles the calibration process (save, load & remove calibration data).
    /// 
    /// </summary>

    [RequireComponent(typeof(IRLAnchorTracking))]
    [DefaultExecutionOrder(10_000)]
    public class AnchorBasedObjectSynchronization : MonoBehaviour, IRLAnchorTracking.IIRLAnchorTrackingListener
    {
        [SerializeField] Transform transformToPosition;
        [SerializeField] ModelPositionChanger modelPositionChanger;

        [Header("Calibration")]
        public DetectionType calibrationType = DetectionType.MarkerDetection;
        public ModelCalibration calibration = new ModelCalibration { scale = Vector3.one };
        public bool loadCalibrationFromDisk = true;

        [Header("Repositionning options")]
        public float minPositionChangeForUpdate = 0.1f;
        public float minAngleChangeForUpdate = 5f;
        public bool useTripletAlgorithm = false;
        public float minimumDelayBeforeRepositionningUpdate = 5;

        public float lastRepositionning = -1;

        [Header("Reference anchors")]
        public GameObject referenceAnchorPrefab;

        [Header("Action")]
        public Action requestedAction = Action.None;
        public bool applyActionOnce = true;

        [Header("Found anchors")]
        public List<AnchorTag> referenceAnchors = new List<AnchorTag>();


        [Header("Debug")]
        public bool AreRepositioningAnchorsAvailable => transformToPosition != null && validIRLAnchors.Count >= 1;
        public Transform TransformToPosition => transformToPosition;
        public Transform commonReferenceAnchor;

        Dictionary<string, AnchorTag> referenceAnchorTagsById = new Dictionary<string, AnchorTag>();
        List<Transform> validReferenceAnchorTransforms = new List<Transform>();
        public List<IRLAnchorInfo> validIRLAnchors = new List<IRLAnchorInfo>();

        public UnityEvent onModelRepositioned = new UnityEvent();
        public enum Action
        {
            None,
            Repositioning,
            Save,
            RemoveSave
        }

        [HideInInspector]
        public IRLAnchorTracking irlAnchorTracking;

        public UnityEvent<IRLAnchorInfo> onIRLAnchorDetectedThisFrame = new UnityEvent<IRLAnchorInfo>();

        private void Awake()
        {
            irlAnchorTracking = GetComponent<IRLAnchorTracking>();
            ChangeObjectToPosition(transformToPosition);
        }

        public void ChangeObjectToPosition(Transform transformToPosition)
        {
            // Reference object to reposition
            Debug.Log($"[{name}] ChangeObjectToPosition " + transformToPosition);
            this.transformToPosition = transformToPosition;
            if (transformToPosition == null) return;

            // Load anchors from calibration file
            calibration.calibrationType = calibrationType;

            if (string.IsNullOrEmpty(calibration.calibrationName) && transformToPosition)
            {
                calibration.calibrationName = transformToPosition.name.Replace(" ", "").Replace("/", "").Replace("(", "").Replace(")", "");
            }
            bool loadedCalibration = false;
            if (loadCalibrationFromDisk && ModelCalibration.TryLoad(calibrationType, calibration.calibrationName, out var availableCalibration))
            {
                // spawn reference anchors found in calibration file
                loadedCalibration = true;
                calibration = availableCalibration;
                calibration.ApplyToObjectAnchorTags(transformToPosition.gameObject, destroyUnusedAnchors: true, referenceAnchorPrefab: referenceAnchorPrefab);
            } 
            else
            {
                // if no calibration file found, search predefined anchors in object to reposition and add them to calibration
                calibration.UpdateWithObjectAnchorTags(transformToPosition.gameObject);
            }

            // store reference anchors found in calibration file in referenceAnchors
            referenceAnchors = new List<AnchorTag>();
            foreach(var anchorTag in transformToPosition.GetComponentsInChildren<AnchorTag>())
            {
                if (loadedCalibration == false || calibration.TryGetCalibrationAnchorForId(anchorTag.anchorId, out _))
                {
                    referenceAnchors.Add(anchorTag);
                }
            }

            referenceAnchorTagsById.Clear();
            foreach (var a in referenceAnchors) referenceAnchorTagsById[a.anchorId] = a;
        }

        void ApplyAction() {
            if (transformToPosition == null) return;

            if (requestedAction == Action.Repositioning)
            {
                if (TryReposition())
                {
                    if (applyActionOnce) requestedAction = Action.None;
                }
            }

            if (requestedAction == Action.Save)
            {
                Save();
                if (applyActionOnce) requestedAction = Action.None;
            }

            if(requestedAction == Action.RemoveSave)
            {
                RemoveSave();
                if (applyActionOnce) requestedAction = Action.None;
            }
        }

        [ContextMenu("TryReposition")]
        public bool TryReposition()
        {
            if (AreRepositioningAnchorsAvailable == false) return false;
            
            List<Pose> irlAnchorPoses = new List<Pose>();
            bool isRepositioningPossible = true;


            // Check if at least one actual anchor is stable enough (hasLongStability) to trigger a reposition
            foreach (var validIRLAnchor in validIRLAnchors)
            {
                var hasLongStability = validIRLAnchor.hasLongStability;

                if (validIRLAnchor.hasLongStability == false)
                {
                    isRepositioningPossible = false;
                }

                irlAnchorPoses.Add(validIRLAnchor.stablePose);
            }

            // check the model has not been repositionned recently before triggering a new reposition
            if (isRepositioningPossible && validReferenceAnchorTransforms.Count > 0 && irlAnchorPoses.Count > 0)
            {
                isRepositioningPossible = minimumDelayBeforeRepositionningUpdate == -1 || ((Time.time - lastRepositionning) > minimumDelayBeforeRepositionningUpdate);
            }
            else
            {
                isRepositioningPossible = false;
            }

            if (isRepositioningPossible)
            {
                (var pos, var rot) = AnchorsManipulation.ObjectPositionToMoveRelativeReferenceTransformsToTargetAbsolutePoses(
                    transformToPosition,
                    validReferenceAnchorTransforms, irlAnchorPoses,
                    useTripletAlgorithm: false, commonReferenceAnchor: commonReferenceAnchor);

                if(lastRepositionning != -1)
                {
                    bool updatePosition = false;
                    var positionChange = Vector3.Distance(transformToPosition.position, pos);
                    var angleChange = Quaternion.Angle(transformToPosition.rotation, rot);

                    if (minPositionChangeForUpdate >= 0 && positionChange > minPositionChangeForUpdate)
                    {
                        updatePosition = true;
                    }
                    if (minAngleChangeForUpdate >= 0 && angleChange > minAngleChangeForUpdate)
                    {
                        updatePosition = true;
                    }

                    if (updatePosition == false) return false;
                }

                if(modelPositionChanger == null)
                    modelPositionChanger = transformToPosition.GetComponent<ModelPositionChanger>();
                if (modelPositionChanger == null)
                    Debug.LogError("Can not the model because ModelPositionChanger not found");

                modelPositionChanger.ChangeModelPosition(pos, rot);

                lastRepositionning = Time.time;
                Debug.Log($"Model repositionned to pos : {pos}");
                if(onModelRepositioned != null) onModelRepositioned.Invoke();
                return true;

            }
            else
            {
                //Debug.LogError($"No valid anchors (none detected, {localAnchorTagsById.Count} local defined, {irlAnchorTracking.irlAnchorsInfo.Count} world defined");
                return false;
            }
        }

        [ContextMenu("Save")]
        public void Save()
        {
            calibration.UpdateWithObjectAnchorTags(transformToPosition.gameObject, irlAnchorTracking.irlAnchorsInfo);
            Debug.Log($"Calibration result:\n{calibration.ToJson()}");
            if (loadCalibrationFromDisk)
            {
                calibration.Save();
            }
            // reload the reference anchor to apply this new calibration
            ChangeObjectToPosition(transformToPosition);
        }

        [ContextMenu("RemoveSave")]
        public void RemoveSave()
        {
            calibration.RemoveSave();
            // Clear local calibration
            calibration = new ModelCalibration("");
            calibration.calibrationType = calibrationType;
            // Remove anchors
            var existingAnchors = transformToPosition.GetComponentsInChildren<AnchorTag>();
            if(existingAnchors.Length > 0)
            {
                for (int i = existingAnchors.Length - 1; i >= 0; i--)
                {
                    existingAnchors[i].gameObject.SetActive(false);
                    GameObject.Destroy(existingAnchors[i].gameObject);
                }
            }
            // Reload the reference anchors (to purge them here)
            ChangeObjectToPosition(transformToPosition);
        }

        #region IRLAnchorTracking.IAnchorBasedObjectSynchronizationListener
        public void OnIRLAnchorSpawn(IRLAnchorTracking irlAnchorTracking, string anchorId) {}

        public void OnIRLAnchorDetectedThisFrame(IRLAnchorTracking irlAnchorTracking, IRLAnchorInfo anchorInfo)
        {
            if(onIRLAnchorDetectedThisFrame != null) onIRLAnchorDetectedThisFrame.Invoke(anchorInfo);
            
            if (referenceAnchorTagsById.ContainsKey(anchorInfo.anchorId) && referenceAnchorTagsById[anchorInfo.anchorId].enabled)
            {
                validIRLAnchors.Add(anchorInfo);
                validReferenceAnchorTransforms.Add(referenceAnchorTagsById[anchorInfo.anchorId].transform);
            }
        }

        public void OnDetectionStarted(IRLAnchorTracking irlAnchorTracking)
        {
            validReferenceAnchorTransforms.Clear();
            validIRLAnchors.Clear();
        }

        public void OnDetectionFinished(IRLAnchorTracking irlAnchorTracking)
        {
            if (calibration.calibrationName == "" && transformToPosition != null)
            {
                // transformToPosition has been set in the inspector
                ChangeObjectToPosition(transformToPosition);
            }

            ApplyAction();
        }
        #endregion
    }

    public static class ModelCalibrationExtension
    {
        public static void UpdateWithObjectAnchorTags(this ModelCalibration modelCalibration, GameObject positionReferenceObject, List<IRLAnchorInfo> anchorsInfo)
        {
            var objectTransform = positionReferenceObject.transform;
            modelCalibration.anchors.Clear();
            foreach (var anchorInfo in anchorsInfo)
            {
                if(anchorInfo.lastDetectionTime == 0 || anchorInfo.shouldIgnore)
                {
                    continue;
                }
                var calibrationAnchor = new CalibrationAnchor
                {
                    position = objectTransform.InverseTransformPoint(anchorInfo.lastValidPosition),
                    rotation = Quaternion.Inverse(objectTransform.rotation) * anchorInfo.lastValidRotation,
                    id = anchorInfo.anchorId
                };
                modelCalibration.anchors.Add(calibrationAnchor);
            }
        }
    }
}


