using Fusion.XR.Shared.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Fusion.XR.Shared.Base
{
    public class HardwareController : BaseLateralizedHardwareRigPart, IHardwareController, IHapticFeedbackProviderRigPart, IOverridableGrabbingProvider
    {
        public override RigPartKind Kind => RigPartKind.Controller;

        [HideInInspector]
        public LocalGripTracker localGripTracker;

        [Tooltip("Useful for fake controllers: disable grabbing update, force tracked status")]
        public bool simulatedMode = false;
        #region Tracking status

        [HideInInspector]
        public LocalControllerPresenceTracker controllerPresenceTracker;

        protected override void Awake()
        {
            base.Awake();
            controllerPresenceTracker = new LocalControllerPresenceTracker(this);
            localGripTracker = new LocalGripTracker(this);
        }

        public override void DoUpdateTrackingStatus()
        {
            base.DoUpdateTrackingStatus();
            TrackingStatus = RigPartTrackingstatus.NotTracked;
            if (controllerPresenceTracker != null && controllerPresenceTracker.ReadValue<float>() is float tracked && tracked > 0)
            {
                TrackingStatus = RigPartTrackingstatus.Tracked;
            }
            if (simulatedMode)
            {
                TrackingStatus = RigPartTrackingstatus.Tracked;
            }
            // We do not update grabbing with input for this frame is an override of grabbing has been requested during it
            if (simulatedMode == false)
            {
                UpdateGrabbingStatus();
            }
        }
        #endregion

        #region IGrabbingProvider
        protected virtual void UpdateGrabbingStatus()
        {
            IsGrabbing = false;
            if (localGripTracker != null && localGripTracker.ReadValue<float>() is float gripValue && gripValue > 0.5f)
            {
                IsGrabbing = true;
            }
        }
        public bool IsGrabbing { get; set; } = false;
        #endregion

        #region IOverridableGrabbingProvider
        public void OverrideGrabbing(bool isGrabbing)
        {
            IsGrabbing = isGrabbing;
        }
        #endregion

        #region IHapticFeedbackProvider (vibrations handling)
        protected UnityEngine.XR.InputDevice? _device = null;
        protected bool supportImpulse = false;

        // Find the device associated to a VR controller, to be able to send it haptic feedback (vibrations)
        public virtual UnityEngine.XR.InputDevice? Device
        {
            get
            {
                if (_device == null)
                {
                    InputDeviceCharacteristics sideCharacteristics = Side == RigPartSide.Left ? InputDeviceCharacteristics.Left : InputDeviceCharacteristics.Right;
                    InputDeviceCharacteristics trackedControllerFilter = InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.TrackedDevice | sideCharacteristics;

                    List<UnityEngine.XR.InputDevice> foundControllers = new List<UnityEngine.XR.InputDevice>();
                    InputDevices.GetDevicesWithCharacteristics(trackedControllerFilter, foundControllers);

                    if (foundControllers.Count > 0)
                    {
                        var inputDevice = foundControllers[0];
                        _device = inputDevice;
                        if (inputDevice.TryGetHapticCapabilities(out var hapticCapabilities))
                        {
                            // We memorize if this device can support vibrations
                            supportImpulse = hapticCapabilities.supportsImpulse;
                        }
                    }
                }
                return _device;
            }
        }

        // If a device supporting haptic feedback has been detected, send a vibration to it (here in the form of an impulse)
        public virtual void SendHapticImpulse(float amplitude = 0.3f, float duration = 0.3f, uint channel = 0)
        {
            if (Device != null)
            {
                var inputDevice = Device.GetValueOrDefault();
                if (supportImpulse)
                {
                    inputDevice.SendHapticImpulse(channel, amplitude, duration);
                }
            }
        }

        // If a device supporting haptic feedback has been detected, send a vibration to it (here in the form of a buffer describing the vibration data)
        public virtual  void SendHapticBuffer(byte[] buffer, uint channel = 0)
        {
            if (Device != null)
            {
                var inputDevice = Device.GetValueOrDefault();
                if (supportImpulse)
                {
                    inputDevice.SendHapticBuffer(channel, buffer);
                }
            }
        }

        public virtual void StopHaptics()
        {
            if (Device != null)
            {
                var inputDevice = Device.GetValueOrDefault();
                if (supportImpulse)
                {
                    inputDevice.StopHaptics();
                }
            }
        }
        #endregion
    }
}
