using Fusion.XR.Shared.Base;
using Fusion.XR.Shared.Core;
using UnityEngine;

namespace Fusion.Addons.Meta
{
#if OCULUS_SDK_AVAILABLE
    [RequireComponent(typeof(OVRControllerHelper))]
#endif
    public class MetaBridgeHardwareController : HardwareController
    {
#if OCULUS_SDK_AVAILABLE
        OVRControllerHelper ovrControllerHelper;

        [Header("Position adaptation")]
        [Tooltip("Offset to RigPartPose")]
        public Vector3 positionOffset = Vector3.zero;

        public override Pose RigPartPose
        {
            get
            {
                var basePose = base.RigPartPose;
                basePose.position = transform.TransformPoint(positionOffset);
                return basePose;
            }
        }

        public OVRInput.ControllerInHandState ControllerInHandState
        {
            get
            {
                OVRInput.ControllerInHandState state = OVRInput.GetControllerIsInHandState(MetaSide);
                return state;
            }
        }

        public OVRInput.Hand MetaSide => (ovrControllerHelper.m_controller == OVRInput.Controller.LTouch) ? OVRInput.Hand.HandLeft : OVRInput.Hand.HandRight;

        public bool IsMetaControllerConnected => OVRInput.IsControllerConnected(ovrControllerHelper.m_controller);
        public bool IsMetaControllerActive => (OVRInput.GetActiveController() & ovrControllerHelper.m_controller) == ovrControllerHelper.m_controller;

        protected override void Awake()
        {
            base.Awake();
            // We let the meta rig deal with gameobject status
            disabledGameObjectWhenNotTracked = false;
            ovrControllerHelper = GetComponent<OVRControllerHelper>();
            Side = ovrControllerHelper.m_controller == OVRInput.Controller.LTouch ? RigPartSide.Left : RigPartSide.Right;
        }

        public override void UpdateTrackingStatus()
        {
            base.UpdateTrackingStatus();
            TrackingStatus = RigPartTrackingstatus.NotTracked;
            if (IsMetaControllerActive)
            {
                TrackingStatus = RigPartTrackingstatus.Tracked;
            }
        }
#endif
    }
}
