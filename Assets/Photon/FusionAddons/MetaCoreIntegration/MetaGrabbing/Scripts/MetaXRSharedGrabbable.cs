using Fusion.XR.Shared.Core;
#if META_INTERACTION_AVAILABLE
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;


#endif
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Fusion.Addons.Meta
{
#if META_INTERACTION_AVAILABLE
    /// <summary>
    /// Bridge that make a Oculus.Interaction.Grabbable compatible with the INetworkGrabbable interface (for compatibility with some Fusion XR add-ons)
    /// </summary>
    public class MetaXRSharedGrabbable : NetworkBehaviour, XR.Shared.Core.INetworkGrabbable, IStateAuthorityChanged
    {
        Grabbable grabbable;

        #region XR.Shared.Core.IGrabbable
        [Networked, OnChangedRender(nameof(OnIsGrabbedChange))]
        public bool IsGrabbed { get; set; } = false;
        [Networked]
        public NetworkGrabber CurrentGrabber { get; set; }

        public Vector3 LocalPositionOffset { get; set; }

        public Quaternion LocalRotationOffset { get; set; }

        public UnityEvent OnGrab => onGrab;

        public UnityEvent OnUngrab => onUngrab;

        public UnityEvent<GameObject> OnLocalUserGrab => onLocaluserGrab;
        #endregion

        #region XR.Shared.Core.INetworkGrabbable
        INetworkGrabber INetworkGrabbable.CurrentGrabber => (Object != null && Object.IsValid) ? CurrentGrabber : null;
        bool XR.Shared.Core.INetworkGrabbable.IsReceivingAuthority => isReceivingAuthority;
        #endregion

        [Header("Events")]
        public UnityEvent onGrab = new UnityEvent();
        public UnityEvent<GameObject> onLocaluserGrab = new UnityEvent<GameObject>();
        public UnityEvent onUngrab = new UnityEvent();
        public List<int> grabbingSelectorIds = new List<int>();

        bool isReceivingAuthority = false;
        // Store the detected grabber while waiting for FUN timing
        List<NetworkGrabber> detectedGrabbers = new List<NetworkGrabber>();

        const bool ExtrapolateWhiletakingAuthority = true;
        Rigidbody rb;
        NetworkTransform networkTransform;
        Vector3 transferingPosition;
        Quaternion transferingRotation;
        Vector3 transferingVelocity;
        Vector3 transferingAngularVelocity;

        [Header("Events")]
        [SerializeField] bool debugLog = false;

        public bool IsGrabbedForInteractionSDK => grabbable.SelectingPointsCount > 0 && grabbingSelectorIds.Count != 0;

        private void Awake()
        {
            grabbable = GetComponentInChildren<Grabbable>();
            grabbable.WhenPointerEventRaised += OnPointerEventRaised;
            rb = GetComponent<Rigidbody>();
            networkTransform = GetComponent<NetworkTransform>();
        }

        private void OnPointerEventRaised(PointerEvent pointerEvent)
        {
            var rig = HardwareRigsRegistry.GetHardwareRig();
            GameObject grabInteractor = null;
            IHardwareRigPart grabbingRigPart = null;
            if (pointerEvent.Data is GrabInteractor eventInteractor && rig != null)
            {
                grabInteractor = eventInteractor.gameObject;
                var controllerRef = eventInteractor.GetComponent<ControllerRef>();
                if (controllerRef)
                {
                    var side = controllerRef.Handedness;
                    foreach (var rigPart in rig.RigParts)
                    {
                        if (rigPart is IHardwareController && rigPart is ILateralizedRigPart lateralizedRigPart)
                        {
                            bool matchingSide = (lateralizedRigPart.Side == RigPartSide.Left && side == Oculus.Interaction.Input.Handedness.Left)
                                || (lateralizedRigPart.Side == RigPartSide.Right && side == Oculus.Interaction.Input.Handedness.Right);
                            if (matchingSide)
                            {
                                grabbingRigPart = rigPart;
                                break;
                            }

                        }
                    }
                }
            }

            if (pointerEvent.Data is HandGrabInteractor handEventInteractor && rig != null)
            {
                grabInteractor = handEventInteractor.gameObject;
                var side = handEventInteractor.Hand.Handedness;
                foreach (var rigPart in rig.RigParts)
                {
                    if (rigPart is IHardwareHand && rigPart is ILateralizedRigPart lateralizedRigPart)
                    {
                        bool matchingSide = (lateralizedRigPart.Side == RigPartSide.Left && side == Oculus.Interaction.Input.Handedness.Left)
                            || (lateralizedRigPart.Side == RigPartSide.Right && side == Oculus.Interaction.Input.Handedness.Right);
                        if (matchingSide)
                        {
                            grabbingRigPart = rigPart;
                            break;
                        }

                    }
                }
            }

            if (debugLog && pointerEvent.Type != PointerEventType.Move)
                Debug.LogError($"[grabbingRigPart:{grabbingRigPart} | grabInteractor: {grabInteractor} | pointerEvent: {pointerEvent.Type}|{grabbable.SelectingPointsCount}] {pointerEvent.Data} {pointerEvent.Data?.GetType()} | {pointerEvent.Identifier}");

            if (grabbable == null)
            {
                return;
            }
            if (pointerEvent.Type == PointerEventType.Select)
            {
                grabbingSelectorIds.Add(pointerEvent.Identifier);

                NetworkGrabber detectedGrabber = null;
                if (grabbingRigPart?.LocalUserNetworkRigPart != null)
                {
                    detectedGrabber = grabbingRigPart.LocalUserNetworkRigPart.gameObject.GetComponentInChildren<NetworkGrabber>();
                }

                if (detectedGrabber && detectedGrabbers.Contains(detectedGrabber) == false)
                {
                    detectedGrabbers.Add(detectedGrabber);
                }
                if (detectedGrabber == null)
                {
                    Debug.LogError($"Unable to detect NetworkGrabber for interactor {pointerEvent.Data}.");
                }
                if (grabbable.SelectingPointsCount == 1)
                {
                    DoGrab(detectedGrabber);
                }

            }
            if (pointerEvent.Type == PointerEventType.Unselect)
            {
                grabbingSelectorIds.Remove(pointerEvent.Identifier);

                NetworkGrabber detectedGrabber = null;
                if (grabbingRigPart?.LocalUserNetworkRigPart != null)
                {
                    detectedGrabber = grabbingRigPart.LocalUserNetworkRigPart.gameObject.GetComponentInChildren<NetworkGrabber>();
                }

                if (detectedGrabber && detectedGrabbers.Contains(detectedGrabber))
                {
                    detectedGrabbers.Remove(detectedGrabber);
                }
                if (detectedGrabber == null)
                {
                    Debug.LogError($"Unable to detect NetworkGrabber for interactor {pointerEvent.Data}.");
                }
                if (grabbable.SelectingPointsCount == 0)
                {
                    DoUngrab();
                }
            }
        }

        protected void DoUngrab()
        {
        }

        protected void DoGrab(NetworkGrabber detectedGrabber)
        {
            if (detectedGrabber)
            {
                var pose = this.LocalOffsetToGrabber(detectedGrabber);
                LocalPositionOffset = pose.position;
                LocalRotationOffset = pose.rotation;
            }

            if (Object && Object.HasStateAuthority == false)
            {
                Object.RequestStateAuthority();
                isReceivingAuthority = true;
            }

            if (onLocaluserGrab != null) onLocaluserGrab.Invoke(detectedGrabber?.gameObject);
        }

        private void FixedUpdate()
        {
            if (Object != null && Object.IsValid && Object.HasStateAuthority == false && isReceivingAuthority)
            {
                StoreEngineState();
            }
        }

        // Store the position at the end of fixed update, to memorize the state as it is changed by the Meta Interaction SDK, in case we don't have yet the authority and the state authority data would erase this
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

        #region NetworkBehaviour
        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();
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


            if (IsGrabbedForInteractionSDK)
            {
                CurrentGrabber = detectedGrabbers.Count > 0 ? detectedGrabbers[0] : null;
            }
            else
            {
                CurrentGrabber = null;
            }
            IsGrabbed = IsGrabbedForInteractionSDK;
        }

        public override void Render()
        {
            // For incoming state authority
            if (Object.HasStateAuthority == false && isReceivingAuthority && ExtrapolateWhiletakingAuthority)
            {
                transform.position = transferingPosition;
                transform.rotation = transferingRotation;
            }

            base.Render();
        }
        #endregion

        void OnIsGrabbedChange()
        {

            if (IsGrabbed)
            {
                if (onGrab != null) onGrab.Invoke();
            }
            else
            {
                if (onUngrab != null) onUngrab.Invoke();
            }
        }

        #region IStateAuthorityChanged
        public void StateAuthorityChanged()
        {
            if (Object.HasStateAuthority == false && IsGrabbedForInteractionSDK)
            {
                // Ungrab the object if we lost the state auth
                // TODO test
                foreach (var grabbingSelectorId in grabbingSelectorIds)
                {
                    grabbable.ProcessPointerEvent(new PointerEvent(grabbingSelectorId, PointerEventType.Cancel, default));
                }
            }
        }
        #endregion
    }
#else
    public class MetaGrabbable : MonoBehaviour {}
#endif
}
