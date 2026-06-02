using Fusion.XR.Shared.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.XR.Shared.Core.HardwareBasedGrabbing {

    /**
     * Detect the presence of a grabbable under the hand trigger collider
     * Trigger the grabbing/ungrabbing depending on the hand isGrabbing field
     */
    public class Grabber : MonoBehaviour
    {
        public IGrabbingProvider rigPart;

        Collider lastCheckedCollider;
        Grabbable lastCheckColliderGrabbable;
        public Grabbable grabbedObject;
        protected NetworkGrabber _networkGrabber;
        public NetworkGrabber NetworkGrabber
        {
            get
            {
                if (_networkGrabber == null)
                {
                    _networkGrabber = rigPart.LocalUserNetworkRigPart?.gameObject.GetComponentInChildren<NetworkGrabber>(true);
                }
                if (_networkGrabber == null)
                {
                    Debug.LogError($"No NetworkGrabber for {rigPart}");
                }
                return _networkGrabber;
            }
        }
        Dictionary<Collider, Grabbable> hoveredGrabbableByColliders = new Dictionary<Collider, Grabbable>();
        public List<Grabbable> hoveredGrabbables = new List<Grabbable>();

        protected virtual void Awake()
        {
            rigPart = GetComponentInParent<IGrabbingProvider>();
            if (rigPart == null) Debug.LogError("Grabber should be placed next to an IGrabbingProviderHardwareRigPart");
            if (GetComponentInParent<Rigidbody>() == null)
            {
                Debug.LogError("A rigid body (and a trigger collider) is required for the Grabber to be sure to trigger the OnTrigger callback");
            }
        }

        protected virtual bool IsGrabbing => rigPart != null && rigPart.IsGrabbing;

        private void OnTriggerStay(Collider other)
        {
            if (enabled == false) return;
            // Exit if an object is already grabbed
            if (grabbedObject != null)
            {
                // It is already the grabbed object or another, but we don't allow shared grabbing here
                return;
            }

            Grabbable grabbable;

            if (lastCheckedCollider == other)
            {
                grabbable = lastCheckColliderGrabbable;
            }
            else
            {
                grabbable = other.GetComponentInParent<Grabbable>();
            }
            // To limit the number of GetComponent calls, we cache the latest checked collider grabbable result
            lastCheckedCollider = other;
            lastCheckColliderGrabbable = grabbable;
            if (grabbable != null)
            {
                bool wasHovered = hoveredGrabbables.Contains(grabbable);

                if (IsGrabbing)
                {
                    if(wasHovered || grabbable.allowedClosedHandGrabing)
                    {
                        Grab(grabbable);
                    }
                } 
                else
                {
                    if (!hoveredGrabbables.Contains(grabbable))
                    {
                        hoveredGrabbableByColliders[other] = grabbable;
                        hoveredGrabbables.Add(grabbable);
                    }
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (hoveredGrabbableByColliders.ContainsKey(other))
            {
                if (hoveredGrabbables.Contains(hoveredGrabbableByColliders[other]))
                {
                    hoveredGrabbables.Remove(hoveredGrabbableByColliders[other]);
                }
                hoveredGrabbableByColliders.Remove(other);
            }
        }


        public virtual void Grab(Grabbable grabbable)
        {
            grabbable.Grab(this);
            grabbedObject = grabbable;
        }

        public virtual void Ungrab(Grabbable grabbable)
        {
            if (grabbedObject == null) return;
            grabbedObject.Ungrab();
            grabbedObject = null;
        }

        private void Update()
        {
            // Check if the local hand is still grabbing the object
            if (grabbedObject != null && IsGrabbing == false)
            {
                // Object released by this hand
                Ungrab(grabbedObject);
            }
            CheckHovered();
        }

        void CheckHovered()
        {
            // Hovered object may have been destroyed while being hovered. Destroyed gameobjects respond to "== null" while staying in collections
            foreach (var key in hoveredGrabbableByColliders.Keys)
            {
                if (key == null)
                {
                    hoveredGrabbableByColliders.Remove(key);
                    break;
                }
            }
            hoveredGrabbables.RemoveAll(g => g == null);
        }
    }

}
