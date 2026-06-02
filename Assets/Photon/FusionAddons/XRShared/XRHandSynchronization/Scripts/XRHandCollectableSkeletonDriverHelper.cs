using UnityEngine;
using UnityEngine.XR.Hands;
using System.Collections.Generic;

namespace Fusion.Addons.XRHandsSync
{
    /// <summary>
    /// Fallback solution to provide the root trasnform to a XRHandCollectableSkeletonDriver
    /// (needed in some cases - if no skinned mesh renderer is used, or if it does not have a rootBone, due to OnEnable timing)
    /// </summary>
    public interface ISkeletonDriverLogicOverride
    {
        public Transform SkeletonDriverRootOverride { get; }
        public bool DontUpdateBonesTransforms { get; }
    }

    public static class XRHandCollectableSkeletonDriverHelper
    {
        public static XRHandCollectableSkeletonDriver SetupXRHandsboneCollector(GameObject handGameObject, bool leftHand, Transform overrideRoot = null)
        {
            return SetupXRHandsboneCollector(handGameObject, leftHand ? Handedness.Left : Handedness.Right, overrideRoot);
        }

        public static XRHandCollectableSkeletonDriver SetupXRHandsboneCollector(GameObject handGameObject, Handedness handedness, Transform overrideRoot = null)
        {
            XRHandCollectableSkeletonDriver collectableSkeletonDriver = null;

            var xrHandTrackingsEvents = new List<XRHandTrackingEvents>(handGameObject.GetComponentsInChildren<XRHandTrackingEvents>(true));
            if (xrHandTrackingsEvents.Count == 0)
            {
                var trackingEvents = handGameObject.AddComponent<XRHandTrackingEvents>();
                trackingEvents.handedness = handedness;
                xrHandTrackingsEvents.Add(trackingEvents);
            }

            foreach (var handTrackingEvent in handGameObject.GetComponentsInChildren<XRHandTrackingEvents>(true))
            {
                var skeletonDriver = handTrackingEvent.gameObject.GetComponent<XRHandCollectableSkeletonDriver>();
                if (skeletonDriver == null)
                {
                    skeletonDriver = handTrackingEvent.gameObject.AddComponent<XRHandCollectableSkeletonDriver>();
                    var handRenderer = handGameObject.GetComponentInChildren<SkinnedMeshRenderer>();

                    Transform root = null;
                    if(skeletonDriver.rootTransform != null)
                    {
                        root = skeletonDriver.rootTransform;
                    }
                    else if(overrideRoot != null)
                    {
                        root = overrideRoot;
                    }
                    else if (handRenderer != null)
                    {
                        root = handRenderer.rootBone;
                    }

                    if(root)
                    {
                        // Initialization sequence source: HandVisualizer from XRHands HandVisualizer sample
                        skeletonDriver.jointTransformReferences = new List<JointToTransformReference>();
                        skeletonDriver.rootTransform = root;
                        XRHandSkeletonDriverUtility.FindJointsFromRoot(skeletonDriver);
                        skeletonDriver.InitializeFromSerializedReferences();

                        skeletonDriver.handTrackingEvents = handTrackingEvent;

                        skeletonDriver.applyWristPoseToHandRoot = false;

                        collectableSkeletonDriver = skeletonDriver;
                    }
                    else
                    {
                        // Automatic setup failed: warn that a setup is required
                        Debug.LogError($"Place a XRHandCollectableSkeletonDriver on XRHandTrackingEvents game object to allow synchronization of finger tracking. Also, please:" +
                            $"- set root transform" +
                            $"- press find joints" +
                            $"- uncheck apply wist pose to hand root");
                    }
                }
                else
                {
                    collectableSkeletonDriver = skeletonDriver;
                    break;
                }
            }

            return collectableSkeletonDriver;
        }
    }
}