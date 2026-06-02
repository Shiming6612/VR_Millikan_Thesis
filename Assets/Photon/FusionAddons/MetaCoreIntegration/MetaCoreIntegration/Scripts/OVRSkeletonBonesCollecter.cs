
using Fusion.Addons.HandsSync;
using Fusion.XR.Shared.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion.Addons.XRHandsSync;

namespace Fusion.Addons.Meta.HandsSync
{

#if OCULUS_SDK_AVAILABLE
    public class OVRSkeletonBonesCollecter : MonoBehaviour, IBonesCollecter
    {
        IHardwareHand hardwareHand;

        [Header("Adapt hand location")]
        public Vector3 handTrackingWristPositionOffset = new Vector3(0, 0.005f, 0);
        public Vector3 handTrackingWristRotationOffset = new Vector3(0, 0, 0);
        public Vector3 handTrackingIndexTipPositionOffset = new Vector3(0, 0, 0);
        public Vector3 handTrackingIndexTipRotationOffset = new Vector3(0, 0, 0);


        public OVRHand ovrHand;
        OVRSkeleton _skeleton;

        #region OVR info
        public OVRSkeleton.SkeletonType OVRSkeletonType => ((OVRSkeleton.IOVRSkeletonDataProvider)ovrHand).GetSkeletonType();
        #endregion

        #region IBonesCollecter
        public HandTrackingMode CurrentHandTrackingMode {
            get
            {
                return (ovrHand.IsTracked && SkeletonData.IsDataValid) ? HandTrackingMode.FingerTracking : HandTrackingMode.NotTracked;
            }
        } 

        public Dictionary<HandSynchronizationBoneId, Pose> CurrentBonesPoses => null;

        public Dictionary<HandSynchronizationBoneId, Quaternion> CurrentBoneRotations {
            get
            {
                Dictionary<HandSynchronizationBoneId, Quaternion> boneRotations = new Dictionary<HandSynchronizationBoneId, Quaternion>();

                if (useOpenXRSkeleton)
                {
                    return CurrentBoneRotationsForOpenXRSkeleton;
                }
                else
                {
                    return CurrentBoneRotationsForOVRSkeleton;
                }
            }
        }

        public Dictionary<HandSynchronizationBoneId, Quaternion> CurrentBoneRotationsForOVRSkeleton
        {
            get
            {
                Dictionary<HandSynchronizationBoneId, Quaternion> boneRotations = new Dictionary<HandSynchronizationBoneId, Quaternion>();
                
                Quaternion thumb0 = Quaternion.identity;
                Quaternion thumb1 = Quaternion.identity;

                if (SkeletonData.IsDataValid)
                {
                    int i = 0;
                    foreach (var r in SkeletonData.BoneRotations)
                    {
                        var boneId = (OVRSkeleton.BoneId)i;
                        var xrHandBoneId = boneId.AsHandSynchronizationBoneId();
                        if (boneId == OVRSkeleton.BoneId.Hand_Thumb0) thumb0 = r.ToXRHandsRot(OVRSkeletonType, boneId);
                        if (boneId == OVRSkeleton.BoneId.Hand_Thumb1) thumb1 = r.ToXRHandsRot(OVRSkeletonType, boneId);
                        if (xrHandBoneId != HandSynchronizationBoneId.Invalid)
                        {
                            var rot = r.ToXRHandsRot(OVRSkeletonType, boneId);
                            boneRotations[xrHandBoneId] = rot;
                        }
                        i++;
                    }
                }
                var thumbRot = thumb0 * thumb1;
                boneRotations[HandSynchronizationBoneId.Hand_Thumb0] = thumbRot;
                boneRotations[HandSynchronizationBoneId.Hand_Index0] = Quaternion.identity;
                boneRotations[HandSynchronizationBoneId.Hand_Middle0] = Quaternion.identity;
                boneRotations[HandSynchronizationBoneId.Hand_Ring0] = Quaternion.identity;
                boneRotations[HandSynchronizationBoneId.Hand_Palm] = Quaternion.identity;
                return boneRotations;
            }
        }

        public Dictionary<HandSynchronizationBoneId, Quaternion> CurrentBoneRotationsForOpenXRSkeleton
        {
            get
            {
                Dictionary<HandSynchronizationBoneId, Quaternion> boneRotations = new Dictionary<HandSynchronizationBoneId, Quaternion>();
                if (SkeletonData.IsDataValid)
                {
                    int i = 0;
                    foreach (var r in SkeletonData.BoneRotations)
                    {
                        var boneId = (OVRSkeleton.BoneId)i;
                        boneRotations[boneId.AsHandSynchronizationOpenXRBoneId()] = _skeleton.Bones[i].Transform.localRotation;
                        i++;
                    }
                }
                return boneRotations;
            }
        }
        #endregion

        OVRSkeleton.SkeletonPoseData _skeletonData;
        bool skeletonDataCollectedForThisFrame = false;
        bool useOpenXRSkeleton = false;
        private void Awake()
        {
            if (ovrHand == null)
            {
                ovrHand = GetComponent<OVRHand>();

            }
            if (ovrHand == null) 
                Debug.LogError("OVR hand not set");
            else 
                _skeleton = ovrHand.GetComponent<OVRSkeleton>();
            hardwareHand = GetComponentInParent<IHardwareHand>();
            var ovrRuntimeSettings = OVRRuntimeSettings.Instance;
            useOpenXRSkeleton = ovrRuntimeSettings.HandSkeletonVersion == OVRHandSkeletonVersion.OpenXR;
        }

        public OVRSkeleton.SkeletonPoseData SkeletonData
        {
            get
            {
                RefreshSkeletonData();
                return _skeletonData;
            }
        }

        public Pose WristPose
        {
            get
            {
                var pose = new Pose(transform.position, transform.rotation);
                foreach (var bone in _skeleton.Bones)
                {
                    var isWristBone = (useOpenXRSkeleton == false && bone.Id == OVRSkeleton.BoneId.Hand_WristRoot) || (useOpenXRSkeleton && bone.Id == OVRSkeleton.BoneId.XRHand_Wrist);
                    if (isWristBone)
                    {
                        pose.position = bone.Transform.position + handTrackingWristPositionOffset;
                        pose.rotation = bone.Transform.rotation * Quaternion.Euler(handTrackingWristRotationOffset);
                        break;
                    }
                }
                return pose;
            }
        }

        public Pose IndexTipPose
        {
            get
            {
                var pose = new Pose(transform.position, transform.rotation);
                foreach (var bone in _skeleton.Bones)
                {
                    var isIndexBone = (useOpenXRSkeleton == false && bone.Id == OVRSkeleton.BoneId.Hand_IndexTip) || (useOpenXRSkeleton && bone.Id == OVRSkeleton.BoneId.XRHand_IndexTip);
                    if (isIndexBone)
                    {
                        pose.position = bone.Transform.position;
                        pose.rotation = bone.Transform.rotation;
                        if (handTrackingIndexTipPositionOffset != Vector3.zero) pose.position += handTrackingIndexTipPositionOffset;
                        if(handTrackingIndexTipRotationOffset != Vector3.zero) pose.rotation *= Quaternion.Euler(handTrackingIndexTipRotationOffset);
                        break;
                    }
                }
                return pose;
            }
        }

        void RefreshSkeletonData(bool forceRefresh = false)
        {
            if (skeletonDataCollectedForThisFrame == false || forceRefresh || ovrHand.IsDataValid != _skeletonData.IsDataValid)
            {
                var skeletonDataProvider = ((OVRSkeleton.IOVRSkeletonDataProvider)ovrHand);
                _skeletonData = skeletonDataProvider.GetSkeletonPoseData();
                skeletonDataCollectedForThisFrame = true;
            }
        }
        private void LateUpdate()
        {
            skeletonDataCollectedForThisFrame = false;
        }
    }

    public static class QuatFConversion
    {
        public static Quaternion ToXRHandsRot(this OVRPlugin.Quatf q, OVRSkeleton.SkeletonType handType, OVRSkeleton.BoneId boneId)
        {
            bool isThumbBone = boneId == OVRSkeleton.BoneId.Hand_Thumb3 || boneId == OVRSkeleton.BoneId.Hand_Thumb2 || boneId == OVRSkeleton.BoneId.Hand_Thumb1 || boneId == OVRSkeleton.BoneId.Hand_Thumb0;

            if (isThumbBone)
            {
                if (handType == OVRSkeleton.SkeletonType.HandRight || handType == OVRSkeleton.SkeletonType.XRHandRight)
                {
                    return new Quaternion() { x = q.z, y = -q.y, z = q.x, w = q.w };
                }
                return new Quaternion() { x = q.z, y = q.y, z = -q.x, w = q.w };
            }
            return q.ToXRHandsRot(handType);
        } 
        
        public static Quaternion ToXRHandsRot(this OVRPlugin.Quatf q, OVRSkeleton.SkeletonType handType)
        {
            if (handType == OVRSkeleton.SkeletonType.HandRight || handType == OVRSkeleton.SkeletonType.XRHandRight)
            {
                return new Quaternion() { x = q.z, y = -q.y, z = -q.x, w = q.w };
            }
            return new Quaternion() { x = q.z, y = q.y, z = q.x, w = q.w };
        }
    }

    public static class HandSStateMetaExtensions
    {
        // AsHandSynchronizationBoneId returns the HandSynchronizationBoneId corresponding to the OVRSkeleton.BoneId parameter
        public static HandSynchronizationBoneId AsHandSynchronizationBoneId(this OVRSkeleton.BoneId source)
        {
            switch (source)
            {
                case OVRSkeleton.BoneId.Hand_WristRoot: return HandSynchronizationBoneId.Hand_WristRoot;
                case OVRSkeleton.BoneId.Hand_ForearmStub: return HandSynchronizationBoneId.Hand_ForearmStub;
                case OVRSkeleton.BoneId.Hand_Thumb0: return HandSynchronizationBoneId.Invalid;
                case OVRSkeleton.BoneId.Hand_Thumb1: return HandSynchronizationBoneId.Invalid;
                case OVRSkeleton.BoneId.Hand_Thumb2: return HandSynchronizationBoneId.Hand_Thumb1;
                case OVRSkeleton.BoneId.Hand_Thumb3: return HandSynchronizationBoneId.Hand_Thumb2;
                case OVRSkeleton.BoneId.Hand_Index1: return HandSynchronizationBoneId.Hand_Index1;
                case OVRSkeleton.BoneId.Hand_Index2: return HandSynchronizationBoneId.Hand_Index2;
                case OVRSkeleton.BoneId.Hand_Index3: return HandSynchronizationBoneId.Hand_Index3;
                case OVRSkeleton.BoneId.Hand_Middle1: return HandSynchronizationBoneId.Hand_Middle1;
                case OVRSkeleton.BoneId.Hand_Middle2: return HandSynchronizationBoneId.Hand_Middle2;
                case OVRSkeleton.BoneId.Hand_Middle3: return HandSynchronizationBoneId.Hand_Middle3;
                case OVRSkeleton.BoneId.Hand_Ring1: return HandSynchronizationBoneId.Hand_Ring1;
                case OVRSkeleton.BoneId.Hand_Ring2: return HandSynchronizationBoneId.Hand_Ring2;
                case OVRSkeleton.BoneId.Hand_Ring3: return HandSynchronizationBoneId.Hand_Ring3;
                case OVRSkeleton.BoneId.Hand_Pinky0: return HandSynchronizationBoneId.Hand_Pinky0;
                case OVRSkeleton.BoneId.Hand_Pinky1: return HandSynchronizationBoneId.Hand_Pinky1;
                case OVRSkeleton.BoneId.Hand_Pinky2: return HandSynchronizationBoneId.Hand_Pinky2;
                case OVRSkeleton.BoneId.Hand_Pinky3: return HandSynchronizationBoneId.Hand_Pinky3;
                case OVRSkeleton.BoneId.Hand_ThumbTip: return HandSynchronizationBoneId.Hand_ThumbTip;
                case OVRSkeleton.BoneId.Hand_IndexTip: return HandSynchronizationBoneId.Hand_IndexTip;
                case OVRSkeleton.BoneId.Hand_MiddleTip: return HandSynchronizationBoneId.Hand_MiddleTip;
                case OVRSkeleton.BoneId.Hand_RingTip: return HandSynchronizationBoneId.Hand_RingTip;
                case OVRSkeleton.BoneId.Hand_PinkyTip: return HandSynchronizationBoneId.Hand_PinkyTip;
            }
            return HandSynchronizationBoneId.Invalid;
        }

        public static HandSynchronizationBoneId AsHandSynchronizationOpenXRBoneId(this OVRSkeleton.BoneId source)
        {
            var openXRBone = UnityEngine.XR.Hands.XRHandJointIDUtility.FromIndex((int)source);
            var syncbone = openXRBone.AsHandSynchronizationBoneId();
            return syncbone;
        }
}
#else
    public class OVRSkeletonBonesCollecter : MonoBehaviour
    {

    }
#endif
}

