using System;
using System.Collections.Generic;
#if POLYSPATIAL_SDK_AVAILABLE
using Unity.PolySpatial;
using Unity.PolySpatial.InputDevices;
using static Unity.PolySpatial.VolumeCamera;
#endif
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.LowLevel;
using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Core.Touch;
using Fusion.XR.Shared.Core.HardwareBasedGrabbing;

namespace Fusion.Addons.VisionOsHelpers
{
    public interface ISpatialTouchListener
    {
#if POLYSPATIAL_SDK_AVAILABLE

        void TouchStart(SpatialPointerKind interactionKind, Vector3 interactionPosition, SpatialPointerState primaryTouchData);
        void TouchEnd();
        void TouchStay(SpatialPointerKind interactionKind, Vector3 interactionPosition, SpatialPointerState primaryTouchData);
#endif
    }

    public interface ISpatialTouchTracker
    {
#if POLYSPATIAL_SDK_AVAILABLE

        bool IsUsed { get; set; }
        public GameObject LastSpatialTouchedObject { get; }
        public void OnTouchInactive();
        public void OnTouchUpdate(SpatialPointerState primaryTouchData, VolumeCamera.PolySpatialVolumeCameraMode currentMode, bool doNotUseContactSpatialGrabbingInUnboundedMode, bool preventGrabbingSpatialTouchListeners);
#endif
    }
    /**
    * 
    * SpatialTouchHandler class detects user's interactions (touch, pinch, indirect pinch) thanks to Unity Polyspatial.
    * Touch:
    * It raises TouchStart, TouchEnd & TouchStay events for ISpatialTouchListener.
    * Grabbing: 
    * The SpatialTouchTracker struct can keep track of up to 2 spatial touch event at the same time.
    * If present in the scene, up to 2 SpatialGrabber will be associated to the SpatialTouchTracker struct to handle spatial grabbing
    * In unbounded mode, if replaceContactGrabber is set to false, spatial touch for grabbing is not taken into account to avoid duplicate logic,
    *  as, grabbing while touching is already handled by the normal grabber logic. 
    * If replaceContactGrabber is set to true (default, as hand tracking is refreshed less often than spatial touches on visionOS), normal Grabber components on hands will be disabled, to avoid this duplicate logic
    *  
    **/

    public class SpatialTouchHandler : MonoBehaviour
    {
#if POLYSPATIAL_SDK_AVAILABLE

        public VolumeCamera volumeCamera;

        public List<ISpatialTouchTracker> trackers = new List<ISpatialTouchTracker>();
        [Header("Interaction configuration")]
        [Tooltip("If true, regular Grabber component on the HardwareRig will be disabled on visionOS")]
        public bool replaceContactGrabber = true;
        [Tooltip("If true, regular Toucher component on the HardwareRig will be disabled on visionOS")]
        public bool replaceTouchers = false;
        [Tooltip("If true, spatial touch interaction won't trigger grabbing on object having components implementing ISpatialTouchListener")]
        public bool preventGrabbingSpatialTouchListeners = true;

        VolumeCamera.PolySpatialVolumeCameraMode currentMode;
        bool doNotUseContactSpatialGrabbingInUnboundedMode = false;
        bool hardwareRigGrabberDisabled = false;
        bool hardwareRigTouchersDisabled = false;

        IHardwareRig hardwareRig;

        private void Awake()
        {
            hardwareRig = GetComponentInParent<IHardwareRig>(true);
            DetectSpatialTouchTrackers();
            DetectVolumeCamera();
        }

        void DetectSpatialTouchTrackers() {
            if (hardwareRig == null)
            {
                hardwareRig = HardwareRigsRegistry.GetHardwareRig();
            }
            if (trackers.Count == 0)
            {
                if (hardwareRig != null) {
                    trackers = new List<ISpatialTouchTracker>(hardwareRig.gameObject.GetComponentsInChildren<ISpatialTouchTracker>());
                }
            }
        }

        void DetectVolumeCamera()
        {
            if(volumeCamera == null)
            {
                volumeCamera = FindAnyObjectByType<VolumeCamera>(FindObjectsInactive.Include);
                if (volumeCamera)
                {

#if UNITY_6000_0_OR_NEWER
                    volumeCamera.WindowStateChanged.AddListener(OnWindowStateChanged);
#else
                    volumeCamera.OnWindowEvent.AddListener(OnVolumeCameraWindowEvent);
#endif

                }
            }
        }

        void AdaptHardwareRigFeatures() {
            doNotUseContactSpatialGrabbingInUnboundedMode = !replaceContactGrabber;
#if UNITY_VISIONOS && !UNITY_EDITOR
            if (replaceTouchers && hardwareRigTouchersDisabled == false)
            {
                var hardwareRig = HardwareRigsRegistry.GetHardwareRig();
                if (hardwareRig != null)
                {
                    var hardwareRigTouchers = hardwareRig.gameObject.GetComponentsInChildren<Toucher>();
                    foreach (var t in hardwareRigTouchers) t.enabled = false;
                    hardwareRigTouchersDisabled = true;
                }
            }
            if (replaceContactGrabber && hardwareRigGrabberDisabled == false)
            {
                var hardwareRig = HardwareRigsRegistry.GetHardwareRig();
                if (hardwareRig != null)
                {
                    var hardwareRigGrabbers = hardwareRig.gameObject.GetComponentsInChildren<Grabber>();
                    foreach (var g in hardwareRigGrabbers)
                    {
                        if (g is SpatialGrabber) continue;
                        g.enabled = false;
                    }
                    hardwareRigGrabberDisabled = true;
                }
            }
#endif
        }

#if UNITY_6000_0_OR_NEWER
        private void OnWindowStateChanged(VolumeCamera camera, VolumeCamera.WindowState windowState)
        {
            currentMode = windowState.Mode;
            Debug.Log("OnWindowStateChanged: VolumeCameraMode: " + currentMode);
        }
#else
        private void OnVolumeCameraWindowEvent(VolumeCamera.WindowState windowState)
        {
            currentMode = windowState.Mode;
            Debug.Log("OnVolumeCameraWindowEvent: VolumeCameraMode: " + currentMode);
        }
#endif

        void OnEnable()
        {
            EnhancedTouchSupport.Enable();
        }

        void Update()
        {
            DetectSpatialTouchTrackers();
            DetectVolumeCamera();

            AdaptHardwareRigFeatures();
#if UNITY_VISIONOS 

            // Reset IsUsed on trackers
            for (int i = 0; i < trackers.Count; i++) trackers[i].IsUsed = false;

            var activeTouches = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches;
            // You can determine the number of active inputs by checking the count of activeTouches
            foreach (var activeTouch in activeTouches)
            {
                // For getting access to PolySpatial (visionOS) specific data you can pass an active touch into the EnhancedSpatialPointerSupport()
                SpatialPointerState primaryTouchData = EnhancedSpatialPointerSupport.GetPointerState(activeTouch);

                GameObject objectBeingInteractedWith = primaryTouchData.targetObject;

                int freeTrackerIndex = -1;
                for (int i = 0; i < trackers.Count; i++)
                {
                    if (trackers[i].IsUsed == true)
                    {
                        // The tracker is already interacting, looking for another one to deal with this touch
                        continue;
                    }
                    else if (trackers[i].LastSpatialTouchedObject == objectBeingInteractedWith)
                    {
                        // This tracker was already interacting with this object: we select this one in priority to map this touch
                        freeTrackerIndex = i;
                        break;
                    }
                    else if (freeTrackerIndex == -1 && trackers[i].LastSpatialTouchedObject == null)
                    {
                        // This tracker was not interacting: if the other one is not more relevant, we select this one
                        freeTrackerIndex = i;
                    }
                }
                if (freeTrackerIndex != -1)
                {
                    trackers[freeTrackerIndex].OnTouchUpdate(primaryTouchData, currentMode, doNotUseContactSpatialGrabbingInUnboundedMode: replaceContactGrabber == false, preventGrabbingSpatialTouchListeners);
                    trackers[freeTrackerIndex].IsUsed = true;
                }
            }

            // We call OTouchInactive on all unused trackers
            for (int i = 0; i < trackers.Count; i++)
            {
                if (trackers[i].IsUsed == false)
                {
                    trackers[i].OnTouchInactive();
                }
            }
#endif
        }
#endif
    }
}