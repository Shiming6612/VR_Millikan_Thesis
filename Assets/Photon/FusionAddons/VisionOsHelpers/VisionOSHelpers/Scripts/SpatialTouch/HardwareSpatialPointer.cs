using System.Collections.Generic;
using Fusion.XR.Shared.Base;
using Fusion.XR.Shared.Core;
#if POLYSPATIAL_SDK_AVAILABLE
using Unity.PolySpatial;
#endif
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace Fusion.Addons.VisionOsHelpers
{
    public enum SpatialPointerId
    {
        Undefined,
        Pointer1,
        Pointer2,
    }

    public class HardwareSpatialPointer : BaseHardwareRigPart, IOverridableGrabbingProvider, ISpatialTouchTracker
    {
        public override RigPartKind Kind => RigPartKind.Pointer;
        public SpatialPointerId pointerId = SpatialPointerId.Undefined;
        bool touchActive = false;

        protected override void Awake()
        {
            base.Awake();
            if (pointerId == SpatialPointerId.Undefined)
            {
                Debug.LogError("[HardwareSpatialPointer] pointerId should be set to a not undefined value");
            }
#if POLYSPATIAL_SDK_AVAILABLE
            grabber = GetComponentInChildren<SpatialGrabber>(true);
#endif
        }
        #region BaseHardwareRigPart

        public override void DoUpdateTrackingStatus()
        {
            TrackingStatus = touchActive ? RigPartTrackingstatus.Tracked : RigPartTrackingstatus.NotTracked;
        }
        #endregion

        #region ISpatialTouchTracker
#if POLYSPATIAL_SDK_AVAILABLE
        public bool IsUsed { get => _isUsed; set => _isUsed = value; }

        public GameObject LastSpatialTouchedObject => _lastSpatialTouchedObject;

        [Header("Debug")]
        [Tooltip("If not not, this object will be displayed at the interaction position, when it occurs only")]
        public GameObject debugRepresentation;
        public TMPro.TMP_Text debugText;
        public Vector3 debugTextOffset = new Vector3(0, 0.1f, 0);
        public Vector3 debugTextRotationOffset = new Vector3(0, -90, 90);


        [Header("Associated SpatialGrabber")]
        [Tooltip("Automatically detected in the scene at start")]
        public SpatialGrabber grabber;

        [Header("Grabbing and touch info")]
        // Used for a touch during the current update
        [SerializeField]
        bool _isUsed = false;
        [SerializeField]
        GameObject _lastSpatialTouchedObject = null;
        public GameObject previousObject;
        public SpatialPointerKind previousKind;
        public List<ISpatialTouchListener> lastSpatialTouchedListeners = new List<ISpatialTouchListener>();


        public void OnTouchUpdate(SpatialPointerState primaryTouchData, VolumeCamera.PolySpatialVolumeCameraMode currentMode, bool doNotUseContactSpatialGrabbingInUnboundedMode, bool preventGrabbingSpatialTouchListeners)
        {
            SpatialPointerKind interactionKind = primaryTouchData.Kind;
            GameObject objectBeingInteractedWith = primaryTouchData.targetObject;
            Vector3 interactionPosition = primaryTouchData.interactionPosition;

            if (previousObject != objectBeingInteractedWith || interactionKind != previousKind)
            {
                previousObject = objectBeingInteractedWith;
                previousKind = interactionKind;
            }

            if (objectBeingInteractedWith != _lastSpatialTouchedObject)
            {
                // TouchEnd callback in case of change on the interacted object (if it has spatial touch listeners)
                if (_lastSpatialTouchedObject != null)
                {
                    foreach (var listener in lastSpatialTouchedListeners)
                    {
                        listener.TouchEnd();
                    }
                }
                _lastSpatialTouchedObject = objectBeingInteractedWith;
                lastSpatialTouchedListeners = new List<ISpatialTouchListener>(objectBeingInteractedWith.GetComponentsInParent<ISpatialTouchListener>());
                foreach (var listener in lastSpatialTouchedListeners)
                {
                    listener.TouchStart(interactionKind, interactionPosition, primaryTouchData);
                }
            }
            else
            {
                // TouchStay callback in case of keeping the same interacted object (if it has spatial touch listeners)
                foreach (var listener in lastSpatialTouchedListeners)
                {
                    listener.TouchStay(interactionKind, interactionPosition, primaryTouchData);
                }
            }

            if ((preventGrabbingSpatialTouchListeners == false || lastSpatialTouchedListeners.Count == 0) && grabber)
            {
                if (interactionKind == SpatialPointerKind.Touch)
                {
                    // No grab while just touching
                    IsGrabbing = false;
                }
                else
                {
                    // Grabbing for direct
                    bool contactGrabbing = interactionKind == SpatialPointerKind.Touch || interactionKind == SpatialPointerKind.DirectPinch;
                    // In unbounded mode, grabbing while touching is already handled by normal grabber logic: skip spatial touch for grabbing to avoid duplicate logic
                    if (doNotUseContactSpatialGrabbingInUnboundedMode && currentMode == VolumeCamera.PolySpatialVolumeCameraMode.Unbounded && contactGrabbing)
                    {
                        IsGrabbing = false;
                    }
                    else
                    {
                        // Grabbing is possible, due to either:
                        // - indirect pinch
                        // - in bounded mode
                        // - or direct pinch with doNotUseContactSpatialGrabbingInUnboundedMode == false
                        IsGrabbing = true;
                    }

                }
            }
            transform.position = interactionPosition;
            transform.rotation = primaryTouchData.inputDeviceRotation;
            touchActive = true;

            DebugPositioning(primaryTouchData);
        }

        public void OnTouchInactive()
        {
            IsGrabbing = false;
            touchActive = false;
            DisableDebug();
            if (_lastSpatialTouchedObject != null)
            {
                foreach (var listener in lastSpatialTouchedListeners)
                {
                    listener.TouchEnd();
                }
            }
            _lastSpatialTouchedObject = null;
            lastSpatialTouchedListeners.Clear();
        }

        public void DebugPositioning(SpatialPointerState primaryTouchData)
        {
            if (debugRepresentation)
            {
                debugRepresentation.SetActive(true);
                debugRepresentation.transform.position = primaryTouchData.interactionPosition;
                debugRepresentation.transform.rotation = primaryTouchData.inputDeviceRotation;
            }
            if (debugText)
            {
                debugText.gameObject.SetActive(true);
                debugText.text = primaryTouchData.targetObject.name;
                debugText.transform.position = primaryTouchData.interactionPosition + debugTextOffset;
                debugText.transform.rotation = primaryTouchData.inputDeviceRotation * Quaternion.Euler(debugTextRotationOffset);
            }
        }

        public void DisableDebug()
        {
            if (debugRepresentation)
            {
                debugRepresentation.SetActive(false);
            }
            if (debugText)
            {
                debugText.gameObject.SetActive(false);
            }
        }
#endif
        #endregion

        #region IOverridableGrabbingProvider
        public bool IsGrabbing { get; set; } = false;

        public void OverrideGrabbing(bool isGrabbing)
        {
            IsGrabbing = isGrabbing;
        }
#endregion
    }

}
