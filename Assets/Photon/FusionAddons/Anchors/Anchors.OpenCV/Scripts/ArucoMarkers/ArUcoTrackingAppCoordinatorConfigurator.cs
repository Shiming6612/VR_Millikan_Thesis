using Fusion.Addons.AnchorsAddon;
using Fusion.XR.Shared.Core;
using PassthroughCameraSamples;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.AnchorsAddon.ArucoMarkersTracking{


    /// <summary>
    /// Configure ArUcoTrackingAppCoordinator to look for the AnchorTags included in the gameObjectsRoot
    /// Set the gameObjectRoot with a parent's WorldAnchorTracking.spawnedWorldAnchorRoot if not defined
    /// 
    /// Also configure ArucoMarkerTracking rigOrigin if not set
    /// </summary>
    [RequireComponent(typeof(TryAR.MarkerTracking.ArUcoTrackingAppCoordinator))]
    public class ArUcoTrackingAppCoordinatorConfigurator : MonoBehaviour
    {
        public Transform gameObjectsRoot;
        public List<string> namePartsToRemove = new List<string> { "Tag", "(", ")" };
        TryAR.MarkerTracking.ArUcoTrackingAppCoordinator coordinator;
        IRLAnchorTracking worldAnchorTracking;
        public bool disableCoordinatorInEditor = true;
        public bool forceLateUpdateTrackingInEditor = true;

        private void Start()
        {
            if (worldAnchorTracking == null)
            {
                worldAnchorTracking = GetComponentInParent<IRLAnchorTracking>();
            }
            if(coordinator == null)
            {
                coordinator = GetComponent<TryAR.MarkerTracking.ArUcoTrackingAppCoordinator>();
            }

            FillMarkerGameObjects();

            // Configure xrOrigin
            if (coordinator != null && coordinator.ArucoMarkerTracking != null)
            {
                if (coordinator.ArucoMarkerTracking.rigOrigin == null)
                {
                    var hardwareRig = HardwareRigsRegistry.GetHardwareRig();
                    if (hardwareRig != null)
                    {
                        coordinator.ArucoMarkerTracking.rigOrigin = hardwareRig.transform;
                    }
                }
            } 
            else
            {
                Debug.LogError("ArUcoTrackingAppCoordinator or ArUcoMarkerTracking nor properly configured");
            }

            // Map tracing to world anchor detection
            // TODO replace Trackingmode by on demand calls with predefined rate
            coordinator.trackingMode = TryAR.MarkerTracking.ArUcoTrackingAppCoordinator.Trackingmode.DuringUpdate;
            bool shouldChangeDetectionMode = true;
#if UNITY_EDITOR
            shouldChangeDetectionMode = disableCoordinatorInEditor == false;
            if (disableCoordinatorInEditor)
            {
                coordinator.enabled = false;
            }
#endif
            if (shouldChangeDetectionMode)
                worldAnchorTracking.detectionTriggerMode = IRLAnchorTracking.DetectionTriggerMode.OnDemand;

            coordinator.onTrackingComplete.AddListener(worldAnchorTracking.DetectValidAnchors);
        }

        public void FillMarkerGameObjects() { 
            if (gameObjectsRoot == null)
            {
                if (worldAnchorTracking)
                {
                    gameObjectsRoot = worldAnchorTracking.spawnedDetectedIrlAnchorRoot;
                }
            }
            if (gameObjectsRoot == null)
            {
                gameObjectsRoot = transform;
            }
            if(coordinator.WebCamTextureManager == null)
            {
                coordinator.WebCamTextureManager = FindAnyObjectByType<WebCamTextureManager>(FindObjectsInactive.Include);
            }
            coordinator.MarkerGameObjectPairs.Clear();
            foreach (Transform t in gameObjectsRoot)
            {
                var anchorTag = t.GetComponent<AnchorTag>();
                if (anchorTag)
                {
                    string cleanAnchorId = anchorTag.anchorId;
                    if (worldAnchorTracking && string.IsNullOrEmpty(worldAnchorTracking.predefinedIrlAnchorsPrefix) == false)
                    {
                        cleanAnchorId = cleanAnchorId.Replace(worldAnchorTracking.predefinedIrlAnchorsPrefix, "");
                    }
                    if (Int32.TryParse(cleanAnchorId, out var tagId))
                    {
                        coordinator.MarkerGameObjectPairs.Add(new TryAR.MarkerTracking.ArUcoTrackingAppCoordinator.MarkerGameObjectPair { markerId = tagId, gameObject = t.gameObject });
                    }
                }
            }
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (disableCoordinatorInEditor)
            {
                coordinator.enabled = false; 
            }
            if (forceLateUpdateTrackingInEditor)
            {
                coordinator.enabled = false;
                worldAnchorTracking.detectionTriggerMode = IRLAnchorTracking.DetectionTriggerMode.DuringLateUpdate;
            }
#endif
        }
    }

}
