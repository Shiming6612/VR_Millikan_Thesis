using System.Collections.Generic;
using UnityEngine;

namespace Fusion.XR.Shared.Core.HardwareBasedGrabbing
{
    public class GrabbingSetup : MonoBehaviour
    {

        IHardwareRig hardwareRig;
        List <IRigPart> grabbingRigParts = new List<IRigPart> ();

        [Tooltip("If true, for IHardwareHand, the grabbing won't be place on the hand palm but on the index, if an index follower is set on the hand")]
        public bool useIndexFollowerWhenAvailableForGrabbing = true;

        [Tooltip("If true, don't display warning when automaticaly setting a collider is needed")]
        public bool removeColliderAutosetupWarning = false;
        private void Awake()
        {
            hardwareRig = GetComponent<IHardwareRig>();
        }

        // Update is called once per frame
        void Update()
        {
            GrabberVerification();
        }

        void GrabberVerification()
        {
            foreach (var rigPart in hardwareRig.RigParts)
            {
                if (rigPart is IHardwareController || rigPart is IHardwareHand)
                {
                    if(grabbingRigParts.Contains(rigPart)) continue;
                    grabbingRigParts.Add(rigPart);
                    var grabber = rigPart.gameObject.GetComponentInChildren<Grabber>();

                    if (grabber == null)
                    {
                        var rigidBody = rigPart.gameObject.AddComponent<Rigidbody>();
                        rigidBody.isKinematic = true;
                        grabber = rigPart.gameObject.AddComponent<Grabber>();
                    }
                    else
                    {
                        var rigidBody = grabber.GetComponent<Rigidbody>();
                        if (rigidBody == null)
                        {
                            rigidBody = grabber.gameObject.AddComponent<Rigidbody>();
                            rigidBody.isKinematic = true;
                        }
                    }

                    if (useIndexFollowerWhenAvailableForGrabbing && rigPart is IHardwareHand hand && hand.IndexTipFollowerTransform != null)
                    {
                        // Set an index collider
                        var collider = hand.IndexTipFollowerTransform.GetComponentInChildren<Collider>();
                        if (collider == null)
                        {
                            var sphereColliderGO = new GameObject("GrabbingCollider");
                            sphereColliderGO.transform.parent = hand.IndexTipFollowerTransform;
                            sphereColliderGO.transform.localPosition = Vector3.zero;
                            sphereColliderGO.transform.localRotation = Quaternion.identity;
                            var sphereCollider = sphereColliderGO.AddComponent<SphereCollider>();
                            sphereCollider.transform.localScale = Vector3.one * 0.04f;
                            sphereCollider.radius = 0.2f;
                            sphereCollider.isTrigger = true;
                            if (removeColliderAutosetupWarning == false)
                                Debug.LogWarning($"A default index collider has been added for grabbing under the indexTipFollowerTransform {hand.IndexTipFollowerTransform}. Please create on in the scene to have desired positionning, or set removeColliderAutosetupWarning to true.");
                        }
                    }
                    else
                    {
                        // Set a palm collider
                        var collider = rigPart.gameObject.GetComponentInChildren<Collider>();
                        if (collider == null)
                        {
                            var boxColliderGO = new GameObject("GrabbingCollider");
                            boxColliderGO.transform.parent = rigPart.transform;
                            boxColliderGO.transform.localPosition = Vector3.zero;
                            boxColliderGO.transform.localRotation = Quaternion.identity;
                            var boxCollider = boxColliderGO.AddComponent<BoxCollider>();
                            boxCollider.transform.localScale = Vector3.one * 0.1f;
                            boxCollider.isTrigger = true;
                            if (removeColliderAutosetupWarning == false)
                                Debug.LogWarning($"A default box collider has been added for grabbing under the palm {rigPart.transform}. Please create on in the scene to have desired positionning.");
                        }
                    }
                }
            }
        }
    }
}
