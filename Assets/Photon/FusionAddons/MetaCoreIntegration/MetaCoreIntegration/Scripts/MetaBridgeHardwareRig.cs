using Fusion.XR.Shared.Base;
using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Core.Tools;
using Fusion.XR.Shared.XRHands;
using UnityEngine;

namespace Fusion.Addons.Meta
{
#if OCULUS_SDK_AVAILABLE
    [RequireComponent(typeof(OVRCameraRig))]
#endif
    public class MetaBridgeHardwareRig : HardwareRig
    {
#if OCULUS_SDK_AVAILABLE
        public bool autoInstallRig = true;
        OVRCameraRig ovrCameraRig;

        [Header("Visual adaptation")]
        public RigPartVisualizer.Mode hardwareHandRenderersVisualizationMode = RigPartVisualizer.Mode.DisplayWhileOffline;
        public RigPartVisualizer.Mode hardwareControllersRenderersVisualizationMode = RigPartVisualizer.Mode.DisplayWhileOffline;

        [Header("Hand tracking follower")]
        public Transform leftIndexTipFollower = null;
        public Transform rightIndexTipFollower = null;
        public Transform leftWristFollower = null;
        public Transform rightWristFollower = null;
        public bool attachFollowerToRigPartHierarchy = true;

        [Header("Hand tracking follower - automatic generation")]
        public bool generateIndexTipFollowers = true;
        public Vector3 leftIndexMarkerEulerRotation = new Vector3(0, -90, 180);
        public Vector3 rightIndexMarkerEulerRotation = new Vector3(0, 90, 0);

        [Header("Advanced - OpenXR hands handling")]
        [Tooltip("[Should be set to false in most cases] If true and the OVRManager is set to use OpenXR hand skeletons, XRHandsHardwareHand will be used for hand tracking, otherwise MetaBridgeHardwareHand (their bone collecter can also adapt to OpenXR)")]
        public bool useXRHandsHardwareHand = false;

        bool openXRHandsDetected = false;
        bool didSetupOpenXRHands = false;

        bool didInitializeSetupHands = false;

        protected virtual void Awake()
        {
            if (generateIndexTipFollowers)
            {
                GenerateIndexTipFollowers();
            }

            SetupControllers();

            SetupHands();

            SetupHeadset();
        }

        private void Update()
        {
            SetupHands();
            if (didInitializeSetupHands && openXRHandsDetected && didSetupOpenXRHands == false && useXRHandsHardwareHand)
            {
                SetupXRHandsHands();
            }
        }

        void SetupHands()
        {
            if (didInitializeSetupHands) return;
            var ovrRuntimeSettings = OVRRuntimeSettings.Instance;
            if (ovrRuntimeSettings == null)
            {
                Debug.LogError("OVRManager.runtimeSettings not yet available. Waiting ...");
                return;
            }
            didInitializeSetupHands = true;

            if (ovrRuntimeSettings.HandSkeletonVersion == OVRHandSkeletonVersion.OpenXR)
            {
#if !XRHANDS_AVAILABLE
                Debug.LogError("XRhands not installed while OpenXR is used: not supported by XRShared hands synchronization");
#else
                openXRHandsDetected = true;
#endif
            }
            if (ovrRuntimeSettings.HandSkeletonVersion == OVRHandSkeletonVersion.OVR || useXRHandsHardwareHand == false)
            {
                SetupOVRSkeletonHands();
            }
        }

        void GenerateIndexTipFollowers()
        {
            if (leftIndexTipFollower == null)
            {
                var follower = new GameObject("LeftHandTrackingIndextip");
                var marker = new GameObject("Marker");
                marker.AddComponent<IndexTipMarker>();
                marker.transform.parent = follower.transform;
                marker.transform.localPosition = Vector3.zero;
                marker.transform.localRotation = Quaternion.Euler(leftIndexMarkerEulerRotation);
                leftIndexTipFollower = follower.transform;
            }
            if (rightIndexTipFollower == null)
            {
                var follower = new GameObject("RightHandTrackingIndextip");
                var marker = new GameObject("Marker");
                marker.AddComponent<IndexTipMarker>();
                marker.transform.parent = follower.transform;
                marker.transform.localPosition = Vector3.zero;
                marker.transform.localRotation = Quaternion.Euler(rightIndexMarkerEulerRotation);
                rightIndexTipFollower = follower.transform;
            }
        }

        void SetupControllers()
        {
            foreach (var ovrControllerHelper in GetComponentsInChildren<OVRControllerHelper>())
            {
                if (ovrControllerHelper.GetComponent<MetaBridgeHardwareController>() == null)
                {
                    var controller = ovrControllerHelper.gameObject.AddComponent<MetaBridgeHardwareController>();
                    // Set default controller offsets to match your NetworkRig models. If this default implementation does not match your need, simply add manually the MetaBridgeHardwareHand and OVRSkeletonBonesCollecter to set values
                    controller.positionOffset = new Vector3(0, -0.02f, 0.04f);

                    var rigPartVisualizer = controller.GetComponent<RigPartVisualizer>();
                    if (rigPartVisualizer == null)
                    {
                        rigPartVisualizer = controller.gameObject.AddComponent<RigPartVisualizer>();
                        rigPartVisualizer.mode = hardwareControllersRenderersVisualizationMode;
                    }
                }
                if (ovrControllerHelper.GetComponent<HardwareControllerCommand>() == null)
                {
                    ovrControllerHelper.gameObject.AddComponent<HardwareControllerCommand>();
                }
            }
        }

        void SetupOVRSkeletonHands()
        {
            var ovrRuntimeSettings = OVRRuntimeSettings.Instance;
            foreach (var ovrSkeleton in GetComponentsInChildren<OVRSkeleton>())
            {
                if (ovrSkeleton.GetComponent<MetaBridgeHardwareHand>() == null)
                {
                    var hand = ovrSkeleton.gameObject.AddComponent<MetaBridgeHardwareHand>();

                    if (ovrRuntimeSettings.HandSkeletonVersion == OVRHandSkeletonVersion.OVR)
                    {
                        if (hand.Side == RigPartSide.Right)
                        {
                            hand.ovrSkeletonBonesCollecter.handTrackingWristRotationOffset = new Vector3(0, 90, 0);
                        }
                        else
                        {
                            hand.ovrSkeletonBonesCollecter.handTrackingWristRotationOffset = new Vector3(0, -90, -180);
                        }
                    }
                    if (ovrRuntimeSettings.HandSkeletonVersion == OVRHandSkeletonVersion.OpenXR)
                    {
                        hand.ovrSkeletonBonesCollecter.handTrackingWristPositionOffset = Vector3.zero;
                    }

                    SetupHandFollowersAndVisualizer(hand);
                }
            }
        }

        void SetupHandFollowersAndVisualizer(HardwareHand hand)
        {
            // Set default wrist offsets to match your NetworkRig models. If this default implementation does not match your need, simply add manually the MetaBridgeHardwareHand and OVRSkeletonBonesCollecter to set values
            if (hand.Side == RigPartSide.Right)
            {
                hand.indexTipFollowerTransform = rightIndexTipFollower;
                hand.wristFollowerTransform = rightWristFollower;
                if (attachFollowerToRigPartHierarchy && rightIndexTipFollower != null) rightIndexTipFollower.parent = hand.transform;
                if (attachFollowerToRigPartHierarchy && rightWristFollower != null) rightIndexTipFollower.parent = hand.transform;
            }
            else
            {
                hand.indexTipFollowerTransform = leftIndexTipFollower;
                hand.wristFollowerTransform = leftWristFollower;
                if (attachFollowerToRigPartHierarchy && leftIndexTipFollower != null) leftIndexTipFollower.parent = hand.transform;
                if (attachFollowerToRigPartHierarchy && leftWristFollower != null) leftWristFollower.parent = hand.transform;
            }

            var rigPartVisualizer = hand.GetComponent<RigPartVisualizer>();
            if (rigPartVisualizer == null)
            {
                rigPartVisualizer = hand.gameObject.AddComponent<RigPartVisualizer>();
                rigPartVisualizer.mode = hardwareHandRenderersVisualizationMode;
            }
        }

        void SetupXRHandsHands()
        {
            var configuredHands = 0;
            foreach (var ovrSkeleton in GetComponentsInChildren<OVRSkeleton>())
            {
                if (ovrSkeleton.IsInitialized)
                {
                    configuredHands++;
                    var hand = ovrSkeleton.gameObject.GetComponent<XRHandsHardwareHand>();
                    if (hand == null)
                    {
                        RigPartSide side = RigPartSide.Undefined;
                        switch (ovrSkeleton.GetSkeletonType())
                        {
                            case OVRSkeleton.SkeletonType.HandLeft:
                            case OVRSkeleton.SkeletonType.XRHandLeft:
                                side = RigPartSide.Left;
                                break;
                            case OVRSkeleton.SkeletonType.HandRight:
                            case OVRSkeleton.SkeletonType.XRHandRight:
                                side = RigPartSide.Right;
                                break;
                        }
                        Transform wristBone = null;
                        foreach (var bone in ovrSkeleton.Bones)
                        {
                            if(bone.Id == OVRSkeleton.BoneId.XRHand_Wrist)
                            {
                                wristBone = bone.Transform;
                                break;
                            }
                        }
                        if (wristBone)
                        {
                            hand = ovrSkeleton.gameObject.AddComponent<XRHandsHardwareHand>();
                            hand.handBonesRootOverride = wristBone;
                            hand.dontUpdateBonesTransforms = true;
                            hand.Side = side; 
                        }

                        SetupHandFollowersAndVisualizer(hand);
                    }
                }
            }

            if(configuredHands == 2)
            {
                didSetupOpenXRHands = true;
            }
        }

        void SetupHeadset()
        {
            if(ovrCameraRig == null)
            {
                ovrCameraRig = GetComponent<OVRCameraRig>();
            }
            var headset = ovrCameraRig.leftEyeCamera.GetComponent<MetaBridgeHardwareHeadset>();
            if (headset == null)
            {
                headset = ovrCameraRig.leftEyeCamera.gameObject.AddComponent<MetaBridgeHardwareHeadset>();
            }

        }
#endif
    }
}
