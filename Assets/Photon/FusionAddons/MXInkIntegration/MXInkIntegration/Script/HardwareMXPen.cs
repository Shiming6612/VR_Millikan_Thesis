using Fusion.XR.Shared.Base;
using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Locomotion;
using Fusion.XR.Shared.Rig;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace Fusion.Addons.MXPenIntegration
{
    public interface IMXInkStateProvider {
        public StylusInputs CurrentState { get;  }
    }

    public class HardwareMXPen : BaseHardwareRigPart, IMXInkStateProvider, IHapticFeedbackProviderRigPart, IGrabbingProvider
    {
        public override RigPartKind Kind => RigPartKind.Stylus;

        protected StylusInputs _stylus;
        InputDevice stylusDevice;

        LocalInputTracker backButtonTracker;
        LocalInputTracker frontButtonTracker;
        LocalInputTracker middleButtonTracker;
        LocalInputTracker tipTracker;

        [Header("Tracking")]
        public bool useXRControllerInputDeviceForTracking = true;

        XRControllerInputDevice deviceTracker;

        [Header("Pen renderers")]
        public Color active_color = Color.gray;
        public Color double_tap_active_color = Color.cyan;
        public Color default_color = Color.black;
        [SerializeField] private Renderer _tip;
        [SerializeField] private Renderer _cluster_front;
        [SerializeField] private Renderer _cluster_middle;
        [SerializeField] private Renderer _cluster_back;

        public StylusInputs CurrentState
        {
            get { return _stylus; }
        }

        public bool IsTracked
        {
            get
            {
                bool stylusFound = _stylus.isActive && stylusDevice.isValid;
                return stylusFound;
            }
        }

        #region IGrabbingProvider
        public virtual bool IsGrabbing => IsTracked && _stylus.cluster_front_value;
        #endregion

        public override void UpdateTrackingStatus()
        {
            base.UpdateTrackingStatus();
            TrackingStatus = IsTracked ? RigPartTrackingstatus.Tracked : RigPartTrackingstatus.NotTracked;
        }

        protected override void Awake()
        {
            base.Awake();
            RegisterDeviceDetection();
            // Detect pen meshes
            foreach(var r in GetComponentsInChildren<Renderer>(true))   
            {
                if (_tip == null && r.name.Contains(".tip")) _tip = r;
                if (_cluster_front == null && r.name.Contains(".cluster.front")) _cluster_front = r;
                if (_cluster_middle == null && r.name.Contains(".cluster.middle")) _cluster_middle = r;
                if (_cluster_back == null && r.name.Contains(".cluster.back")) _cluster_back = r;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            UnregisterDeviceDetection();
        }

        #region InputDevice detection
        protected virtual void RegisterDeviceDetection()
        {
            InputDevices.deviceConnected += DeviceConnected;
            InputDevices.deviceDisconnected += DeviceDisconnected;
            List<InputDevice> devices = new List<InputDevice>();
            InputDevices.GetDevices(devices);
            foreach (InputDevice device in devices)
            {
                DeviceConnected(device);
            }
        }

        protected virtual void UnregisterDeviceDetection()
        {
            InputDevices.deviceConnected -= DeviceConnected;
            InputDevices.deviceDisconnected -= DeviceDisconnected;
        }

        protected virtual void DeviceConnected(InputDevice device)
        {
            bool mxInkConnected = device.name.ToLower().Contains("logitech");
            if (mxInkConnected)
            {
                stylusDevice = device;
                _stylus.isActive = true;
                _stylus.isOnRightHand = (device.characteristics & InputDeviceCharacteristics.Right) != 0;
                tipTracker = new LocalInputTracker("<LogitechMxInkController>/tip");
                backButtonTracker = new LocalInputTracker("<LogitechMxInkController>/clusterBackButton", type: UnityEngine.InputSystem.InputActionType.Button);
                frontButtonTracker = new LocalInputTracker("<LogitechMxInkController>/clusterFrontButton", type: UnityEngine.InputSystem.InputActionType.Button);
                middleButtonTracker = new LocalInputTracker("<LogitechMxInkController>/clusterMiddleButton");
                if (useXRControllerInputDeviceForTracking)
                {
                    if(deviceTracker == null)
                    {
                        deviceTracker = gameObject.AddComponent<XRControllerInputDevice>();
                    }
                    deviceTracker.side = _stylus.isOnRightHand ? XRControllerInputDevice.ControllerSide.Right : XRControllerInputDevice.ControllerSide.Left;
                    deviceTracker.enabled = true;
                }
            }
        }

        protected virtual void DeviceDisconnected(InputDevice device)
        {
            bool mxInkDisconnected = device.name.ToLower().Contains("logitech");
            if (mxInkDisconnected)
            {
                _stylus.isActive = false;
                if (deviceTracker)
                {
                    deviceTracker.enabled = false;
                }
            }
        }
        #endregion

        protected override void Update()
        {
            base.Update();
            if (IsTracked)
            {
                _stylus.inkingPose.position = transform.position;
                _stylus.inkingPose.rotation = transform.rotation;
                _stylus.tip_value = tipTracker.Action.ReadValue<float>();
                _stylus.cluster_middle_value = middleButtonTracker.Action.ReadValue<float>();
                _stylus.cluster_front_value = frontButtonTracker.Action.IsPressed();
                _stylus.cluster_back_value = backButtonTracker.Action.IsPressed();
            } 
            else
            {
                _stylus.tip_value = 0;
                _stylus.cluster_middle_value = 0;
                _stylus.cluster_front_value = false;
                _stylus.cluster_back_value = false;
            }
            UpdatePenVisual();
        }

        protected virtual void UpdatePenVisual()
        {
            if (IsTracked)
            {
                if (_tip)
                {   
                    _tip.material.color = _stylus.tip_value > 0 ? active_color : default_color;
                }
                if (_cluster_front)
                {
                    _cluster_front.material.color = _stylus.cluster_front_value ? active_color : default_color;
                }
                if (_cluster_middle)
                {
                    _cluster_middle.material.color = _stylus.cluster_middle_value > 0 ? active_color : default_color;
                }
                if (_cluster_back)
                {
                    _cluster_back.material.color = _stylus.cluster_back_value ? active_color : default_color;
                }
            }
        }

        #region IHapticFeedbackProviderRigPart
        public void SendHapticImpulse(float amplitude = 0.3F, float duration = 0.3F, uint channel = 0)
        {
            if (stylusDevice == null) return;
            stylusDevice.SendHapticImpulse(0, amplitude, duration);
        }

        public void SendHapticBuffer(byte[] buffer, uint channel = 0)
        {
        }

        public void StopHaptics()
        {
            if (stylusDevice == null) return;
            stylusDevice.StopHaptics();
        }
        #endregion
    }
}
