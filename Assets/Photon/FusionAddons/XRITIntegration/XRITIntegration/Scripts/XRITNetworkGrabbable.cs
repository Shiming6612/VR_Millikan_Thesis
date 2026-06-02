using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.Events;

#if XRIT_ENABLED
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit;
#endif
using Fusion.XR.Shared.Core;

namespace Fusion.XR.Shared.XRIT

{
    /// <summary>
    /// Bridge that make a XRGrabInteractable compatible with the INetworkGrabbable interface (for compatibility with some Fusion XR add-ons)
    /// </summary>
    [DefaultExecutionOrder(XRITNetworkGrabbable.EXECUTION_ORDER)]
    public class XRITNetworkGrabbable : NetworkBehaviour, IStateAuthorityChanged, INetworkGrabbable
    {
        public const int EXECUTION_ORDER = INetworkHand.EXECUTION_ORDER + 10;

        [Networked]
        public NetworkBool OriginalIsKinematic { get; set; } = false;
        Rigidbody rb;
        NetworkTransform networkTransform;

        [Networked, OnChangedRender(nameof(OnIsGrabbedChange))]
        public NetworkBool IsGrabbed { get; set; }
        [Networked]
        public NetworkGrabber CurrentGrabber { get; set; }

        #region INetworkGrabbable
        bool IGrabbable.IsGrabbed => IsGrabbed;
        INetworkGrabber INetworkGrabbable.CurrentGrabber => (Object != null && Object.IsValid) ? CurrentGrabber : null;

        public Vector3 LocalPositionOffset { get; set; }

        public Quaternion LocalRotationOffset { get; set; }

        public UnityEvent OnGrab => onGrab;

        public UnityEvent OnUngrab => onUngrab;
        public UnityEvent<GameObject> OnLocalUserGrab => onLocalUserGrab;

        public bool IsReceivingAuthority => isReceivingAuthority;

        #endregion

        bool isReceivingAuthority = false;

        public enum ParentingMode
        {
            // Will disable NetworkTransform SyncParent and unparent the grabbable if stored under a non-networked object
            UnparentAtStart,
            // Will disable NetworkTransform SyncParent and fix parenting on authority changes
            // AutoFixparenting mode will ensure that grabbed objects have no parent, and ungrabbed object restore their initial parent (as detected during awake)
            // This mode is mostly reelvant for scene network objects, that are stored under non-networked objects
            AutoFixparenting,
            // Use with care, not fully tested due to XRIT internal logic
            NetworkSync,
        }

        public ParentingMode parentingMode = ParentingMode.UnparentAtStart;

        // Extrapolation is required, as XRIT is changing parenting while grabbing. So during the transition time while taking authority, if the object was initially parented, it would appear with its relative position used as world position, leading to th eobject blinking
        const bool ExtrapolateWhiletakingAuthority = true;
        // Some version of XRIT unparent a grabbed object
        const bool ParentingRemovedWhileGrabbing = true;


#if XRIT_ENABLED
        bool isKinematicDuringAwake = false;
        // Store the detected grabber while waiting for FUN timing
        NetworkGrabber detectedGrabber = null;
        Transform originalParent = null;
        XRGrabInteractable grabInteractable;
        [SerializeField] bool debugPositions = false;
#endif
        Vector3 transferingPosition;
        Quaternion transferingRotation;
        Vector3 transferingVelocity;
        Vector3 transferingAngularVelocity;


        [Header("Events")]
        [Tooltip("Called on ungrab")]
        public UnityEvent onUngrab = new UnityEvent();
        [Tooltip("Called on grab")]
        public UnityEvent onGrab = new UnityEvent();
        [Tooltip("Called on grab by the local user (even before state authority change)")]
        public UnityEvent<GameObject> onLocalUserGrab = new UnityEvent<GameObject>();

        [Header("Advanced options")]
        [Tooltip("If true, no check on the state authority options will be done")]
        public bool allowNonTransferableObject = false;

#if XRIT_ENABLED

        protected virtual void Awake()
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
            if (grabInteractable == null) throw new System.Exception("Should be placed next to a XRGrabInteractable component");
            grabInteractable.selectEntered.AddListener(OnSelectEnter);
            grabInteractable.selectExited.AddListener(OnSelectExit);
            rb = GetComponent<Rigidbody>();
            networkTransform = GetComponent<NetworkTransform>();
            // XRIT will update the position during Update: we disable the interpolation to prevent any issue (the position won't be reset before FUN)
#if FUSION_2_1_OR_NEWER
            networkTransform.ConfigFlags = NetworkTransform.NetworkTransformFlags.DisableSharedModeInterpolation;
#else
            networkTransform.DisableSharedModeInterpolation = true;
#endif
            if (rb)
            {
                isKinematicDuringAwake = rb.isKinematic;
            }

            switch (parentingMode)
            {
                case ParentingMode.UnparentAtStart:
                    networkTransform.SyncParent = false;
                    transform.parent = null;
                    break;
                case ParentingMode.AutoFixparenting:
                    networkTransform.SyncParent = false;
                    originalParent = transform.parent;
                    break;
                case ParentingMode.NetworkSync:
                    networkTransform.SyncParent = true;
                    break;
            }
        }

        #region XRGrabInteractable
        protected void OnSelectEnter(SelectEnterEventArgs selectEventArgs)
        {
            var interactor = selectEventArgs.interactorObject;
            IHardwareRigPart hardwareRigPart = null;
            if (interactor != null)
            {
                 hardwareRigPart = interactor.transform.GetComponentInParent<IHardwareRigPart>();
            }
            detectedGrabber = null;
            if (hardwareRigPart?.LocalUserNetworkRigPart != null)
            {
                detectedGrabber = hardwareRigPart.LocalUserNetworkRigPart.gameObject.GetComponentInChildren<NetworkGrabber>();
            }

            if (detectedGrabber == null)
            {
                Debug.LogError($"Unable to detect NetworkGrabber. Missing on the network rig part ({hardwareRigPart?.LocalUserNetworkRigPart}) ?");
            }

            if (debugPositions)
            {
                Debug.LogError($"[OnSelectEnter] {transform.position} (Object.HasStateAuthority: {Object.HasStateAuthority}) (parent: {transform.parent})");
            }

            // Setting CurrentGrabber should be also done in FUN, as we might not have the authority => we store it in detectedGrabber to apply it on FUN timing. 
            CurrentGrabber = detectedGrabber;

            if (Object && Object.HasStateAuthority == false)
            {
                Object.RequestStateAuthority();
                isReceivingAuthority = true; 
                StoreEngineState();
            }

            var pose = this.LocalOffsetToGrabber(detectedGrabber);
            LocalPositionOffset = pose.position;
            LocalRotationOffset = pose.rotation;

            if (onLocalUserGrab != null)
            {
                onLocalUserGrab.Invoke(interactor.transform.gameObject);
            }
        }

        protected void OnSelectExit(SelectExitEventArgs arg0)
        {
            if (Object && Object.HasStateAuthority && rb)
            {
                rb.isKinematic = OriginalIsKinematic;
            }

            if (networkTransform != null)
            {
                // TODO Check if required
                //networkTransform.Teleport(transform.position, transform.rotation);
            }

            detectedGrabber = null;
            CurrentGrabber = null;
        }
        #endregion

        public override void Spawned()
        {
            base.Spawned();

            if (Object.HasStateAuthority && rb)
            {
                OriginalIsKinematic = isKinematicDuringAwake;
            }
        }


        private void FixedUpdate()
        {
            if (Object != null && Object.IsValid && Object.HasStateAuthority == false && isReceivingAuthority)
            {
                StoreEngineState();                
            }
        }

        // Store the position at the end of fixed update, to memorize the state as it is changed by XRIT, in case we don't have yet the authority and the state authority data would erase this
        void StoreEngineState()
        {
            if (rb != null)
            {
                transferingPosition = rb.position;
                transferingRotation = rb.rotation;
                transferingVelocity = rb.linearVelocity;
                transferingAngularVelocity = rb.angularVelocity;
            }
            else
            {
                transferingPosition = transform.position;
                transferingRotation = transform.rotation;
                transferingVelocity = Vector3.zero;
                transferingAngularVelocity = Vector3.zero;
            }
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();
            var isGrabbedForXRIT = grabInteractable.isSelected;
            if (IsGrabbed != grabInteractable.isSelected)
            {
                // The parent has probably changed due to XRIT logic (grabbing unparents). Due to that, the localPosition will snap (from localPOsition to world position- having no parent), and we should not inteprolate at this point
                networkTransform.Teleport();
            }
            if (isGrabbedForXRIT)
            {
                CurrentGrabber = detectedGrabber;
            }
            else
            {
                CurrentGrabber = null;
            }
            IsGrabbed = isGrabbedForXRIT;
            if (Object.HasStateAuthority && isReceivingAuthority && ExtrapolateWhiletakingAuthority)
            {
                // Received authority: we ensure that the preview velocity we had is used 
                if (rb != null)
                {
                    rb.position = transferingPosition;
                    rb.rotation = transferingRotation;
                    rb.linearVelocity = transferingVelocity;
                    rb.angularVelocity = transferingAngularVelocity;
                }
                else
                {
                    transform.position = transferingPosition;
                    transform.rotation = transferingRotation;
                }
                isReceivingAuthority = false;
            }

            if (parentingMode == ParentingMode.AutoFixparenting)
            {
                AutoFixParentingForStateAuthority();
            }
        }

        public override void Render()
        {
            if (debugPositions)
            {
                Debug.LogError($"[Render] {transform.position} (isReceivingAuthority: {isReceivingAuthority})");
            }

            // For incoming state authority
            if (Object.HasStateAuthority == false && isReceivingAuthority && ExtrapolateWhiletakingAuthority)
            {
                transform.position = transferingPosition;
                transform.rotation = transferingRotation;
            }

            if (parentingMode == ParentingMode.AutoFixparenting)
            {
                AutoFixParentingForProxies();
            }

            base.Render();
        }
              

        #region Autofix parenting
        void AutoFixParentingForStateAuthority()
        {
            var iSGrabbedForXRIT = grabInteractable.isSelected;
            if (Object.HasStateAuthority && parentingMode == ParentingMode.AutoFixparenting)
            {
                if (iSGrabbedForXRIT && transform.parent != null)
                {
                    transform.parent = null;
                }
                if (iSGrabbedForXRIT == false && transform.parent != originalParent)
                {
                    transform.parent = originalParent;
                }
            }
        }

        void AutoFixParentingForProxies()
        {
            // For proxies (unless they are currently grabbing and taking authority
            if (ParentingRemovedWhileGrabbing && parentingMode == ParentingMode.AutoFixparenting && Object.HasStateAuthority == false && isReceivingAuthority == false)
            {
                var isGrabbed = IsGrabbed;
                // Interpolate isgrabbed value
                if (TryGetSnapshotsBuffers(out var from, out var to, out var alpha))
                {
                    var reader = GetPropertyReader<NetworkBool>(nameof(IsGrabbed));
                    var isGrabbedFrom = reader.Read(from);
                    var isGrabbedTo = reader.Read(to);
                    isGrabbed = alpha < 0.5f ? isGrabbedFrom : isGrabbedTo;
                }

                // XRIT might have changed parenting on the state authority. We adapt here to use 
                if (isGrabbed && transform.parent != null)
                {
                    transform.parent = null;
                    RerunInterpolationAfterFixingParent();
                }
                if (isGrabbed == false && transform.parent != originalParent)
                {
                    transform.parent = originalParent;
                    RerunInterpolationAfterFixingParent();
                }
            }
        }

        // XRIT changed parenting, but the NetworkTransform had already interpolated with a erroneous parent: rerunning interpolation after the parent fix
        void RerunInterpolationAfterFixingParent()
        {
            if (networkTransform.TryGetSnapshotsBuffers(out var from, out var to, out var alpha))
            {
                var fromData = from.ReinterpretState<NetworkTRSPData>();
                var toData = to.ReinterpretState<NetworkTRSPData>();
                // the state authority teleported during FUN, so the regular interpolation is disabled: the NetworkTrasnform snapped to one or the other position
                if (alpha < 0.5)
                {
                    transform.localPosition = fromData.Position;
                    transform.localRotation = fromData.Rotation;
                }
                else
                {
                    transform.localPosition = toData.Position;
                    transform.localRotation = toData.Rotation;
                }
            }
        }

        #endregion
#endif

        #region IStateAuthorityChanged
        public void StateAuthorityChanged()
        {
#if XRIT_ENABLED
            // Ungrab if someone else is taking the object
            var iSGrabbedForXRIT = grabInteractable.isSelected;
            if (Object.HasStateAuthority == false && iSGrabbedForXRIT)
            {
                var interactors = new List<IXRSelectInteractor>(grabInteractable.interactorsSelecting);
                foreach (var interactor in interactors)
                {
                    grabInteractable.interactionManager.SelectCancel(interactor, grabInteractable);
                }
            }
#endif
        }
        #endregion

        void OnIsGrabbedChange()
        {
#if XRIT_ENABLED

            if (IsGrabbed)
            {
                if (onGrab != null) onGrab.Invoke();
            }
            else
            {
                if (onUngrab != null) onUngrab.Invoke();
            }
#endif
        }

        #region Validation
        void CheckTransferableAuthority(NetworkObject no = null)
        {
            if (allowNonTransferableObject) return;
            if (no == null) no = Object;
            if (no != null && no.IsObjectWithTransferableAuthority() == false)
            {
                Debug.LogError($"[NetworkGrabbable] {name}'s NetworkObject does not have a proper configuration to allow users to change authority on this:" +
                    " check AllowStateAuthorityOverride, uncheck DestroyOnStateAuthorityLeaves, uncheck IsMasterClientObject." +
                    " If you want other settings, check allowNonTransferableObject on the NetworkGrabbable");
            }
        }


        private void OnValidate()
        {
            if (allowNonTransferableObject) return;
            ValidationUtils.SceneEditionValidate(gameObject, () => {
                CheckTransferableAuthority(GetComponentInParent<NetworkObject>());
            });
        }
        #endregion
    }
}