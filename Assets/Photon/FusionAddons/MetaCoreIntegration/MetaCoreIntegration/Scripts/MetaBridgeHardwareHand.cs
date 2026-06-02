using Fusion.Addons.Meta.HandsSync;
using Fusion.XR.Shared.Base;
using Fusion.XR.Shared.Core;
using UnityEngine;

namespace Fusion.Addons.Meta
{
#if OCULUS_SDK_AVAILABLE
    [RequireComponent(typeof(OVRSkeleton))]
    public class MetaBridgeHardwareHand : HardwareHand, IGrabbingProvider
    {
        [HideInInspector]
        public OVRSkeletonBonesCollecter ovrSkeletonBonesCollecter;
        [HideInInspector]
        public OVRSkeleton ovrSkeleton;
        [HideInInspector]
        public OVRHand ovrHand;

        public override Pose WorldIndexTipPose => ovrSkeletonBonesCollecter.IndexTipPose;
        public override Pose WorldWristPose => ovrSkeletonBonesCollecter.WristPose;

        #region IGrabbingProvider
        public bool IsGrabbing => ovrHand != null ? ovrHand.GetFingerIsPinching(OVRHand.HandFinger.Index) : false;
    #endregion

        public override Pose RigPartPose => ovrSkeletonBonesCollecter?.WristPose ?? base.RigPartPose;

        public override void UpdateTrackingStatus()
        {
            base.UpdateTrackingStatus();
            TrackingStatus = ovrSkeletonBonesCollecter?.CurrentHandTrackingMode == Addons.HandsSync.HandTrackingMode.FingerTracking ? RigPartTrackingstatus.Tracked : RigPartTrackingstatus.NotTracked;
        }

        protected override void Awake()
        {
            base.Awake();
            // We let the meta rig deal with gameobject status
            disabledGameObjectWhenNotTracked = false;

            ovrSkeletonBonesCollecter = GetComponent<OVRSkeletonBonesCollecter>();
            ovrHand = GetComponent<OVRHand>();
            ovrSkeleton = GetComponent<OVRSkeleton>(); 

            switch (ovrSkeleton.GetSkeletonType())
            {
                case OVRSkeleton.SkeletonType.HandLeft:
                case OVRSkeleton.SkeletonType.XRHandLeft:
                    Side = RigPartSide.Left;
                    break;
                case OVRSkeleton.SkeletonType.HandRight:
                case OVRSkeleton.SkeletonType.XRHandRight:
                    Side = RigPartSide.Right;
                    break;
            }

            if (ovrSkeletonBonesCollecter == null)
            {
                ovrSkeletonBonesCollecter = gameObject.AddComponent<OVRSkeletonBonesCollecter>();
            }
        }

        protected override void Update()
        {
            base.Update();
            PositionHandBoneFollowers();
        }
    }
#else
    public class MetaBridgeHardwareHand : HardwareHand { }
#endif
}
