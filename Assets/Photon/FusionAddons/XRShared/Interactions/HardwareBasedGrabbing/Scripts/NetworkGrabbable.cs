//#define USE_PHYSICSADDON
#if USE_PHYSICSADDON
using Fusion.Addons.Physics;
#endif
using Fusion.XR.Shared.Core;
using UnityEngine;
using UnityEngine.Events;

namespace Fusion.XR.Shared.Core.HardwareBasedGrabbing
{
    /**
     * 
     * Declare that this game object can be grabbed by a NetworkGrabber
     * 
     * Handle following the grabbing NetworkGrabber
     * 
     **/
    [DefaultExecutionOrder(NetworkGrabbable.EXECUTION_ORDER)]
    public class NetworkGrabbable : NetworkBehaviour, INetworkGrabbable
    {
        public const int EXECUTION_ORDER = INetworkGrabbable.EXECUTION_ORDER;
        [HideInInspector]
        public NetworkTRSP networkTRSP;
#if USE_PHYSICSADDON
        public NetworkRigidbody3D networkRigidbody;
#endif
        [Networked]
        public NetworkBool InitialIsKinematicState { get; set; }
        [Networked]
        public NetworkGrabber CurrentGrabber { get; set; }
        [Networked]
        public Vector3 LocalPositionOffset { get; set; }
        [Networked]
        public Quaternion LocalRotationOffset { get; set; }

        #region INetworkGrabbable
        public virtual bool IsGrabbed => Object != null && CurrentGrabber != null; // We make sure that we are online before accessing [Networked] var

        INetworkGrabber INetworkGrabbable.CurrentGrabber => CurrentGrabber;

        public UnityEvent OnGrab => onDidGrabWithoutDetails;

        public UnityEvent OnUngrab => onDidUngrab;

        public UnityEvent<GameObject> OnLocalUserGrab => grabbable?.OnLocalUserGrab;

        public bool IsReceivingAuthority => isTakingAuthority;

        #endregion

        [Header("Events")]
        public UnityEvent onDidUngrab = new UnityEvent();
        public UnityEvent<NetworkGrabber> onDidGrab = new UnityEvent<NetworkGrabber>();
        // For IGrabbable interface compatibility
        UnityEvent onDidGrabWithoutDetails = new UnityEvent();

        [Header("Advanced options")]
        public bool extrapolateWhileTakingAuthority = true;
        public bool isTakingAuthority = false;
        [Tooltip("If true, no check on the state authority options will be done")]
        public bool allowNonTransferableObject = false;


        [HideInInspector]
        public Grabbable grabbable;
        ChangeDetector funChangeDetector;
        ChangeDetector renderChangeDetector;

        public bool PauseGrabbability { get {
                return grabbable.pauseGrabbability;
            } set {
                grabbable.pauseGrabbability = value;
            }
        }

        bool TryDetectGrabberChange(ChangeDetector changeDetector, out NetworkGrabber previousGrabber, out NetworkGrabber currentGrabber)
        {
            previousGrabber = null;
            currentGrabber = null;
            foreach (var changedNetworkedVarName in changeDetector.DetectChanges(this, out var previous, out var current))
            {
                if (changedNetworkedVarName == nameof(CurrentGrabber))
                {
                    var grabberReader = GetBehaviourReader<NetworkGrabber>(changedNetworkedVarName);
                    previousGrabber = grabberReader.Read(previous);
                    currentGrabber = grabberReader.Read(current);
                    return true;
                }
            }
            return false;
        }

        protected virtual void Awake()
        {
            networkTRSP = GetComponent<NetworkTRSP>();
            if (networkTRSP == null)
            {
                Debug.LogError("A NetworkTransform or a NetworkRigidbody is required next to NetworkGrabbable");
            }
#if USE_PHYSICSADDON
            networkRigidbody = GetComponent<NetworkRigidbody3D>();
#endif
            FindGrabbable();
        }

        protected virtual void FindGrabbable()
        {
            grabbable = GetComponent<Grabbable>();
            if (grabbable == null)
            {
                // We do not use requireComponent as this classes can be subclassed
                grabbable = gameObject.AddComponent<Grabbable>();
            }
        }

        #region Interface for local Grabbable (when the local user grab/ungrab this object)
        [SerializeField] bool deepDebug = false;
        public virtual void LocalUngrab()
        {
            if (deepDebug) Debug.LogError("NG.LocalUngrab");
            if (Object)
            {
                CurrentGrabber = null;
            }
        }

        public virtual void LocalGrab()
        {
            if (Object == null || Object.IsValid == false) return;

            // Ask and wait to receive the stateAuthority to move the object
            isTakingAuthority = true;
            if (Object.HasStateAuthority == false)
            {
                Object.RequestStateAuthority();
            }
            else
            {
                LocalGrabWithAuthority();
            }

        }

        void LocalGrabWithAuthority()
        {
            isTakingAuthority = false;

            // We waited to have the state authority before setting Networked vars
            LocalPositionOffset = grabbable.localPositionOffset;
            LocalRotationOffset = grabbable.localRotationOffset;

            if (grabbable.currentGrabber == null)
            {
                // The grabbable has already been ungrabbed
                if (deepDebug) Debug.LogError("The grabbable has already been ungrabbed");
                return;
            }
            // Update the CurrentGrabber in order to start following position in the FixedUpdateNetwork
            CurrentGrabber = grabbable.currentGrabber.NetworkGrabber;
        }
        #endregion

        public override void Spawned()
        {
            base.Spawned();

            // Save initial kinematic state for later join player
            if (Object.HasStateAuthority && grabbable.rb)
            {
                InitialIsKinematicState = grabbable.rb.isKinematic;
            }


            // We store the default kinematic state, while it is not affected by NetworkRigidbody logic
            grabbable.expectedIsKinematic = InitialIsKinematicState;

            funChangeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            renderChangeDetector = GetChangeDetector(ChangeDetector.Source.SnapshotFrom);

            Application.onBeforeRender += OnBeforeRender;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            base.Despawned(runner, hasState);
            Application.onBeforeRender -= OnBeforeRender;
        }

        const bool forceContinousStateUpdate = false; // Rewrite the state every FUN, might be relevant if some scripts might be manipulating them from outside

        public override void FixedUpdateNetwork()
        {
            if (isTakingAuthority || forceContinousStateUpdate)
            {
                LocalGrabWithAuthority();
            }
            if (isTakingAuthority == false && CurrentGrabber && CurrentGrabber.Object.StateAuthority != Object.StateAuthority)
            {
                CurrentGrabber = null;
            }
            // Check if the grabber changed
            if (TryDetectGrabberChange(funChangeDetector, out var previousGrabber, out var currentGrabber))
            {
                if (previousGrabber)
                {
                    grabbable.UnlockObjectPhysics();
                }
                if (currentGrabber)
                {
                    grabbable.LockObjectPhysics();
                }
            }

            if (!IsGrabbed) return;
            // Follow grabber, adding position/rotation offsets
            grabbable.Follow(followedTransform: CurrentGrabber.transform, LocalPositionOffset, LocalRotationOffset);
        }

        public override void Render()
        {
            // Check if the grabber changed, to trigger callbacks only (actual grabbing logic in handled in FUN for the state authority)
            // Those callbacks can't be called in FUN, as FUN is not called on proxies, while render is called for everybody
            if (TryDetectGrabberChange(renderChangeDetector, out var previousGrabber, out var currentGrabber))
            {
                if (previousGrabber)
                {
                    if (previousGrabber.GrabbedObject == (INetworkGrabbable)this) previousGrabber.GrabbedObject = null;
                    if (onDidUngrab != null) onDidUngrab.Invoke();
                }
                if (currentGrabber)
                {
                    currentGrabber.GrabbedObject = this;
                    if (onDidGrab != null) onDidGrab.Invoke(currentGrabber);
                    if (onDidGrabWithoutDetails != null) onDidGrabWithoutDetails.Invoke();

                }
            }

            ExtrapolationHandling(ExtrapolationTiming.DuringFusionRender);
        }

        /// <summary>
        /// Determine if we should override the grabbable position, to better match the hand position
        /// </summary>
        /// <param name="currentTiming"></param>
        protected virtual void ExtrapolationHandling(ExtrapolationTiming currentTiming)
        {
            if (isTakingAuthority && extrapolateWhileTakingAuthority && grabbable.currentGrabber)
            {
                NetworkGrabber incomingNetworkGrabber = grabbable.currentGrabber.NetworkGrabber;
                if (incomingNetworkGrabber != null && incomingNetworkGrabber.RigPart.RequiredExtrapolationTiming() == currentTiming)
                {
                    // If we are currently taking the authority on the object due to a grab, the network info are still not set
                    //  but we will extrapolate anyway (if the option extrapolateWhileTakingAuthority is true) to avoid having the grabbed object staying still until we receive the authority
                    ExtrapolateWhileTakingAuthority();
                    return;
                }
            }

            if (CurrentGrabber != null && CurrentGrabber.RigPart.RequiredExtrapolationTiming() == currentTiming)
            {
                Extrapolate();
            }
        }

        [BeforeRenderOrder(NetworkGrabbable.EXECUTION_ORDER)]
        protected virtual void OnBeforeRender()
        {
            ExtrapolationHandling(ExtrapolationTiming.DuringUnityOnBeforeRender);
        }

        // Extrapolation: Make visual representation follow grabber, adding position/rotation offsets
        protected virtual void Extrapolate()
        {
            // No need to extrapolate if the object is not grabbed.
            // We do not extrapolate for proxies (might be relevant in some cases, but then the grabbing itself should be properly extrapolated, to avoid grabbing visually before the hand interpolation has reached the grabbing position)
            if (!IsGrabbed || Object.HasStateAuthority == false) return;
            var follwedGrabberRoot = CurrentGrabber != null ? CurrentGrabber.gameObject : null;
            grabbable.Follow(followedTransform: follwedGrabberRoot.transform, LocalPositionOffset, LocalRotationOffset);
        }

        protected virtual void ExtrapolateWhileTakingAuthority()
        {
            // No need to extrapolate if the object is not really grabbed
            if (grabbable.currentGrabber == null) return;
            NetworkGrabber networkGrabber = grabbable.currentGrabber.NetworkGrabber;

            // Extrapolation: Make visual representation follow grabber, adding position/rotation offsets
            // We are currently waiting for the authority transfer: the network vars are not already set, so we use the temporary versions
            var follwedGrabberRoot = networkGrabber != null ? networkGrabber.transform : null;
            grabbable.Follow(followedTransform: follwedGrabberRoot, grabbable.localPositionOffset, grabbable.localRotationOffset);
        }

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
    }
}
