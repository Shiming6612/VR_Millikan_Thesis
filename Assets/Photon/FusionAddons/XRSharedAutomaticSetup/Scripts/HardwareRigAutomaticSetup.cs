using Fusion.XR.Shared.Base;
using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Rig;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEditor;
using Fusion.XR.Shared.XRHands;
using Fusion.Addons.XRHandsSync;
using Fusion.XR.Shared.Locomotion;
using Fusion.XR.Shared.Core.HardwareBasedGrabbing;
#if XRHANDS_AVAILABLE
using UnityEngine.XR.Hands;
#endif

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.XR;
using UnityEngine.InputSystem;
using UnityEngine.XR;
#endif

namespace Fusion.XR.Shared.Automatization.Rig
{

    public class HardwareRigAutomaticSetup : MonoBehaviour
    {
        [Header("Automatic setup - setup options")]
        public bool logEdits = true;


        [System.Serializable]
        public class SetupConfig
        {
            [Header("Automatic setup config - rig creation")]
            public bool addXROrigin = true;
            public bool addControllers = true;
            public bool addControllerSimulatedHands = true;
            public bool addXRHandsHands = true;

            [Header("Automatic setup config - Hardware rig parts")]
            public bool addHardwareRig = true;
            public bool addHardwareHeadset = true;
            public bool addHardwareControllers = true;
            public bool addXRHandsHardwareHands = true;

            [Header("Automatic setup config - Rig part options")]
            public bool hideControllersWhenOnline = true;
            public bool hideHandsWhenOnline = true;

            [Header("Automatic setup config - Interaction")]
            public bool addGrabbing = false;
            public bool addLocomotion = false;
        }

        [Header("Automatic setup - setup config")]
        public SetupConfig setupConfig = new SetupConfig();

        const float cameraNearClipPlane = 0.01f;
        const string simulatedHandLeftPrefabName = "LeftControllerSimulatedHand";
        const string simulatedHandRightPrefabName = "RightControllerSimulatedHand";
        const string transparentMaterialForHardwareHandsMaterialName = "TransparentMaterialForHardwareHands";
        const string xrHandLeftPrefabName = "LeftHandTracking";
        const string xrHandRightPrefabName = "RightHandTracking";
        const string beamerPrefabName = "Beamer";

#if UNITY_EDITOR
#if ENABLE_INPUT_SYSTEM
        public void AutomaticRigsetup() {
            XROrigin xrOrigin = null;
            if (setupConfig.addXROrigin)
            {
                CheckXROrigin();
                xrOrigin = GetComponent<XROrigin>(); 
                CheckCameraOffset(xrOrigin);
                CheckCamera(xrOrigin);
            }

            if(xrOrigin == null) xrOrigin = GetComponent<XROrigin>();

            if (xrOrigin == null)
            {
                Debug.LogError("Missing XROrigin.");
                return;
            }

            if (setupConfig.addControllers)
            {
                CheckControllers(xrOrigin);
            }

            if (setupConfig.addControllerSimulatedHands)
            {
                CheckSimulatedHands();
            }

            if (setupConfig.addXRHandsHands)
            {
                CheckXRHandsHands(xrOrigin);
            }

            if (setupConfig.addHardwareRig)
            {
                CheckHardwareRig(xrOrigin);
            }

            if (setupConfig.addHardwareHeadset)
            {
                CheckHardwareHeadset();
            }

            if (setupConfig.addHardwareControllers)
            {
                CheckHardwareControllers();
            }

            if (setupConfig.addXRHandsHardwareHands)
            {
                CheckXRHandsHardwareHand(xrOrigin);
            }

            if (setupConfig.addGrabbing)
            {
                CheckGrabbing();
            }

            if (setupConfig.addLocomotion)
            {
                CheckLocomotion(xrOrigin);
            }
        }

        #region Rig setup
        public void CheckXROrigin()
        {
            var xrOrigin = GetComponent<XROrigin>();
            if (xrOrigin == null)
            {
                LogEdit("Add <b>XROrigin</b> and camera offset");
                xrOrigin = gameObject.AddComponent<XROrigin>();
                xrOrigin.Origin = gameObject;
            }
        }

        public void CheckCameraOffset(XROrigin xrOrigin) {
            if (xrOrigin != null && xrOrigin.CameraFloorOffsetObject == null)
            {
                LogEdit("Add <b>camera offset</b> gameobject, set it on XROrigin");
                var cameraOffsetGO = new GameObject("Camera Offset");
                cameraOffsetGO.transform.parent = transform;
                cameraOffsetGO.transform.localPosition = new Vector3(0, xrOrigin.CameraYOffset, 0);
                cameraOffsetGO.transform.localRotation = Quaternion.identity;
                xrOrigin.CameraFloorOffsetObject = cameraOffsetGO;
            }
        }

        public void CheckCamera(XROrigin xrOrigin) {

            if (xrOrigin != null && xrOrigin.Camera == null)
            {
                LogEdit($"Add <b>camera</b>:<i>\n- near clip plane set at {cameraNearClipPlane}\n- set tag as MainCamera\n- add TrackedPoseDriver</i>");
                var cameraGO = new GameObject("Headset");
                cameraGO.transform.parent = xrOrigin.CameraFloorOffsetObject.transform;
                cameraGO.transform.localPosition = Vector3.zero;
                cameraGO.transform.localRotation = Quaternion.identity;
                var camera = cameraGO.AddComponent<Camera>();
                camera.nearClipPlane = cameraNearClipPlane;
                cameraGO.tag = "MainCamera";
                xrOrigin.Camera = camera;
                cameraGO.AddComponent<AudioListener>();

                var trackedPoseDriver = cameraGO.AddComponent<TrackedPoseDriver>();
                trackedPoseDriver.ignoreTrackingState = true;
                trackedPoseDriver.positionInput.action.AddBinding("<XRHMD>/centerEyePosition");
                trackedPoseDriver.positionInput.action.AddBinding("<HandheldARInputDevice>/devicePosition");
                trackedPoseDriver.rotationInput.action.AddBinding("<XRHMD>/centerEyeRotation");
                trackedPoseDriver.rotationInput.action.AddBinding("<HandheldARInputDevice>/deviceRotation");
                trackedPoseDriver.trackingStateInput.action.AddBinding("<XRHMD>/trackingState");
            }
        }

        public void CheckControllers(XROrigin xrOrigin)
        {
            var controllers = GetComponentsInChildren<XRControllerInputDevice>(true);
            XRControllerInputDevice leftController = null;
            XRControllerInputDevice rightController = null;
            foreach (var controller in controllers)
            {
                if (controller.side == XRControllerInputDevice.ControllerSide.Left) leftController = controller;
                if (controller.side == XRControllerInputDevice.ControllerSide.Right) rightController = controller;
            }
            if (leftController == null) leftController = AddControllerCore(xrOrigin, RigPartSide.Left);
            if (rightController == null) rightController = AddControllerCore(xrOrigin, RigPartSide.Right);
        }

        XRControllerInputDevice AddControllerCore(XROrigin xrOrigin, RigPartSide side)
        {
            var root = transform;
            if (xrOrigin?.CameraFloorOffsetObject != null)
            {
                root = xrOrigin.CameraFloorOffsetObject.transform;
            }

            var controllerSideText = side == RigPartSide.Left ? "Left" : "Right";
            var controllerName = $"{controllerSideText}Controller";
            var controllerGO = new GameObject(controllerName);
            controllerGO.transform.parent = root;
            controllerGO.transform.localPosition = Vector3.zero;
            controllerGO.transform.localRotation = Quaternion.identity;

            var controllerInputDevice = controllerGO.AddComponent<XRControllerInputDevice>();
            controllerInputDevice.side = side == RigPartSide.Left ? XRControllerInputDevice.ControllerSide.Left : XRControllerInputDevice.ControllerSide.Right;
            controllerInputDevice.detectionMode = XRInputDevice.DetectionMode.Pointer;

            LogEdit($"Add <b>{controllerSideText.ToLower()} controller</b> {controllerInputDevice.name}:<i>\n- add XRControllerInputDevice</i>");

            return controllerInputDevice;
        }

        public void CheckSimulatedHands()
        {
            foreach (var controller in GetComponentsInChildren<XRControllerInputDevice>(true))
            {
                CheckSimulatedHands(controller.gameObject, controller.side == XRControllerInputDevice.ControllerSide.Left ? RigPartSide.Left : RigPartSide.Right);
            }
        }

        public void CheckSimulatedHands(GameObject controller, RigPartSide side)
        {
            var hardwareControllerCommand = controller.GetComponent<HardwareControllerCommand>();
            if (hardwareControllerCommand == null)
            {
                LogEdit($"Add <b>HardwareControllerCommand</b> on {controller.name} to collect controller input");
                hardwareControllerCommand = controller.gameObject.AddComponent<HardwareControllerCommand>();
            }
            var handCommandHandler = controller.GetComponentInChildren<IHandCommandHandler>();
            if (handCommandHandler == null)
            {
                var simulatedHandPrefabName = side == RigPartSide.Left ? simulatedHandLeftPrefabName : simulatedHandRightPrefabName;
                if (AutomatisationTools.TryFindAsset(simulatedHandPrefabName, out GameObject simulatedHandPrefab, requiredPathElement: "XRShared"))
                {
                    var simulatedHandGO = PrefabUtility.InstantiatePrefab(simulatedHandPrefab) as GameObject;
                    LogEdit($"Add {simulatedHandGO.name} under {controller.name} to display a <b>simulated hand for controller</b>");
                    simulatedHandGO.transform.parent = controller.transform;
                    simulatedHandGO.transform.localPosition = Vector3.zero;
                    simulatedHandGO.transform.localRotation = Quaternion.identity;
                }
            }
        }

        public void CheckXRHandsHands(XROrigin xrOrigin)
        {
#if XRHANDS_AVAILABLE
            var hands = GetComponentsInChildren<XRHandTrackingEvents>(true);
            XRHandTrackingEvents leftHand = null;
            XRHandTrackingEvents rightHand = null;
            foreach (var hand in hands)
            {
                if (hand.handedness == Handedness.Left) leftHand = hand;
                if (hand.handedness == Handedness.Right) rightHand = hand;
            }
            if (leftHand == null) AddXRhand(xrOrigin, RigPartSide.Left);
            if (rightHand == null) AddXRhand(xrOrigin, RigPartSide.Right);
#endif
        }

        public void AddXRhand(XROrigin xrOrigin, RigPartSide side)
        {
            var root = transform;
            if (xrOrigin?.CameraFloorOffsetObject != null)
            {
                root = xrOrigin.CameraFloorOffsetObject.transform;
            }

            var handSideText = side == RigPartSide.Left ? "Left" : "Right";
            var handName = $"{handSideText}Hand";
            var handGO = new GameObject(handName);
            handGO.transform.parent = root;
            handGO.transform.localPosition = Vector3.zero;
            handGO.transform.localRotation = Quaternion.identity;

            var xrHandPrefabName = side == RigPartSide.Left ? xrHandLeftPrefabName : xrHandRightPrefabName;
            if (AutomatisationTools.TryFindAsset(xrHandPrefabName, out GameObject xrHandPrefab, requiredPathElement: "XRShared"))
            {
                var xrHandGO = PrefabUtility.InstantiatePrefab(xrHandPrefab) as GameObject;
                LogEdit($"Add {xrHandGO.name} under {handGO.name} to display <b>hand tracking model</b>");
                xrHandGO.transform.parent = handGO.transform;
                xrHandGO.transform.localPosition = Vector3.zero;
                xrHandGO.transform.localRotation = Quaternion.identity;
            }
        }

        #endregion

        public void CheckHardwareRig(XROrigin xrOrigin)
        {
            if (xrOrigin != null)
            {
                var hardwareRig = xrOrigin.GetComponent<HardwareRig>();
                if (hardwareRig == null)
                {
                    LogEdit("Add <b>HardwareRig</b> on " + xrOrigin.name);
                    xrOrigin.gameObject.AddComponent<HardwareRig>();
                }
            }
        }

        public void CheckHardwareHeadset()
        {
            var camera = GetComponentInChildren<Camera>(true);
            if (camera != null)
            {
                var headset = camera.GetComponent<HardwareHeadset>();
                if (headset == null)
                {
                    LogEdit("Add <b>HardwareHeadset</b> on camera " + camera);
                    camera.gameObject.AddComponent<HardwareHeadset>();
                }
            }
        }

        public void CheckHardwareControllers()
        {
            var controllers = GetComponentsInChildren<XRControllerInputDevice>(true);
            foreach (var controller in controllers)
            {
                CheckHarwareController(controller);
            }
        }

        void CheckHarwareController(XRControllerInputDevice controller)
        {
            if (controller == null) return;
            var hardwareController = controller.GetComponent<HardwareController>();
            if (hardwareController == null)
            {
                hardwareController = controller.gameObject.AddComponent<HardwareController>();
                hardwareController.Side = controller.side == XRControllerInputDevice.ControllerSide.Left ? RigPartSide.Left : RigPartSide.Right;
                LogEdit($"Add <b>HardwareController</b> on {hardwareController.name}");
            }
            CheckControllersVisibility(hardwareController);
        }

        public void CheckControllersVisibility(HardwareController controller) {
            if (setupConfig.hideControllersWhenOnline)
            {
                var rigPartVisualizer = controller.gameObject.GetComponent<RigPartVisualizer>();
                if (rigPartVisualizer == null)
                {
                    LogEdit($"Add <b>RigPartVisualizer</b> on {controller.name} to control visibility of the hardware controller while online");
                    rigPartVisualizer = controller.gameObject.AddComponent<RigPartVisualizer>();
                }
                rigPartVisualizer.mode = RigPartVisualizer.Mode.DisplayWhileOffline;
                if (rigPartVisualizer.materialWhileShouldNotDisplay == null)
                {
                    if (AutomatisationTools.TryFindAsset(transparentMaterialForHardwareHandsMaterialName, out Material transparentMaterial, requiredPathElement: "XRShared"))
                    {
                        LogEdit($"Set <b>transparent material on RigPartVisualizer while online</b> on {controller.name}:<i>" +
                            $"\n- a trnasparent material is used to let the simulated hand animation play on Android (an hidden renderer would not play the animation on Android)," +
                            $" so that the actual tip position is properly computed based on the animation. " +
                            $"Useful for touch interactions</i>");
                        rigPartVisualizer.materialWhileShouldNotDisplay = transparentMaterial;
                    }
                }
            }
        }

        public void CheckXRHandsHardwareHand(XROrigin xrOrigin) {
            var hands = GetComponentsInChildren<XRHandsHardwareHand>(true);
            XRHandsHardwareHand leftHand = null;
            XRHandsHardwareHand rightHand = null;
            foreach (var hand in hands)
            {
                if (hand.Side == RigPartSide.Left) leftHand = hand;
                if (hand.Side == RigPartSide.Right) rightHand = hand;
            }
            if (leftHand == null) leftHand = AddXRHandsHardwareHand(xrOrigin, RigPartSide.Left);
            if (rightHand == null) rightHand = AddXRHandsHardwareHand(xrOrigin, RigPartSide.Right);

            CheckHandVisibility(leftHand);
            CheckHandVisibility(rightHand);
        }

        public XRHandsHardwareHand AddXRHandsHardwareHand(XROrigin xrOrigin, RigPartSide side)
        {
            var handTrackers = GetComponentsInChildren<XRHandTrackingEvents>(true);
            foreach(var handTracker in handTrackers)
            {
                var handTrackerSide = handTracker.handedness == Handedness.Left ? RigPartSide.Left : RigPartSide.Right;
                if(handTrackerSide == side)
                {
                    // The XRHandsHardwareHand is placed on the empty gameobject parent of the XRHandTrackingEvents (which is under the camera offset gameobject)
                    if (handTracker.transform.parent != null && handTracker.transform.parent.parent == xrOrigin.CameraFloorOffsetObject.transform)
                    {
                        if (handTracker.transform.parent.parent != xrOrigin.CameraFloorOffsetObject.transform)
                        {
                            Debug.LogError("Unexpected rig organisation");
                        }
                        LogEdit($"Add <b>XRHandsHardwareHand</b> on {handTracker.transform.parent.name}");
                        var hardwareHand = handTracker.transform.parent.gameObject.AddComponent<XRHandsHardwareHand>();
                        hardwareHand.Side = side;
                        return hardwareHand;
                    }
                }
            }
            return null;
        }

        public void CheckHandVisibility(XRHandsHardwareHand hand)
        {
            if (hand == null) return;
            if (setupConfig.hideControllersWhenOnline)
            {
                var rigPartVisualizer = hand.gameObject.GetComponent<RigPartVisualizer>();
                if (rigPartVisualizer == null)
                {
                    LogEdit($"Add <b>RigPartVisualizer</b> on {hand.name} to control visibility of the hardware hands while online");
                    rigPartVisualizer = hand.gameObject.AddComponent<RigPartVisualizer>();
                }
                rigPartVisualizer.mode = RigPartVisualizer.Mode.DisplayWhileOffline;
            }
        }

        public void CheckGrabbing()
        {
            if (GetComponent<GrabbingSetup>() == null)
            {
                LogEdit($"Add <b>GrabbingSetup</b> to automatically add grabbing components on controller and hands at runtime");
                gameObject.AddComponent<GrabbingSetup>();
            }
        }

        public void CheckLocomotion(XROrigin xrOrigin)
        {
            if (GetComponent<RigLocomotion>() == null)
            {
                LogEdit($"Add <b>RigLocomotion</b> to offer the API to move the hardware rig");
                gameObject.AddComponent<RigLocomotion>();
            }
            var controllers = GetComponentsInChildren<HardwareController>(true);
            foreach (var controller in controllers)
            {
                if (controller.GetComponentInChildren<RayBeamer>(true) == null)
                {
                    if (AutomatisationTools.TryFindAsset(beamerPrefabName, out GameObject beamerPrefab, extension: "prefab", requiredPathElements: new string[]{ "XRShared", "Locomotion"}))
                    {
                        var beamerGO = PrefabUtility.InstantiatePrefab(beamerPrefab) as GameObject;
                        LogEdit($"Add <b>RayBeamer</b> {beamerGO.name} under {controller.name} to display a locomotion beam");
                        beamerGO.transform.parent = controller.transform;
                        beamerGO.transform.localPosition = Vector3.zero;
                        beamerGO.transform.localRotation = Quaternion.identity;
                    }
                }
            }
            if (xrOrigin?.Camera && xrOrigin.Camera.GetComponent<Fader>() == null)
            {
                LogEdit($"Add <b>Fader</b> on camera to fade on locomotion");
                xrOrigin.Camera.gameObject.AddComponent<Fader>();
            }
        }
#endif
#endif

        [HideInCallstack]
        public void LogEdit(string s)
        {
            if (logEdits) Debug.Log("[XRRigHardwareRig] " + s + "\n");
        }
    }
}


