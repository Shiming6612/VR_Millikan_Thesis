using Fusion.XR.Shared.Core;
using UnityEngine;

namespace Fusion.XR.Shared.Base
{

    public abstract class BaseHardwareRigPart : MonoBehaviour, IHardwareRigPart
    {
        [Header("Tracking")]
        [SerializeField] RigPartTrackingstatus _trackingStatus = RigPartTrackingstatus.NotTracked;
        public bool disabledGameObjectWhenNotTracked = true;

        public IMovableHardwareRig hardwareRig;

        #region IHardwareRigPart
        public virtual Pose RigPartPose => new Pose(transform.position, transform.rotation);

        public RigPartTrackingstatus TrackingStatus { get => _trackingStatus; set => _trackingStatus = value; }

        public abstract RigPartKind Kind { get; }

        public virtual void UpdateTrackingStatus()
        {
            DoUpdateTrackingStatus();
            ReflectTrackingStatus();
        }

        public virtual void DoUpdateTrackingStatus()
        {
            // Default implementation does not deal with tracking (just enabled or not): should be overriden
            TrackingStatus = (gameObject.activeInHierarchy && enabled) ? RigPartTrackingstatus.Tracked : RigPartTrackingstatus.NotTracked;
        }

        protected void ReflectTrackingStatus()
        {
            if (disabledGameObjectWhenNotTracked)
            {
                if (TrackingStatus == RigPartTrackingstatus.NotTracked && gameObject.activeSelf == true)
                {
                    gameObject.SetActive(false);
                }
                else if (TrackingStatus == RigPartTrackingstatus.Tracked && gameObject.activeSelf == false)
                {
                    gameObject.SetActive(true);
                }
            }
        }

        protected void HandleTrackingStatus()
        {
            UpdateTrackingStatus();
            ReflectTrackingStatus();
        }

        public IRig Rig => hardwareRig;

        INetworkRigPart _localUserNetworkRigPart = null;
        public INetworkRigPart LocalUserNetworkRigPart => _localUserNetworkRigPart;

        // should be called by the local user network rig
        public void RegisterLocalUserNetworkRigPart(INetworkRigPart localUserNetworkRigPart)
        {
            _localUserNetworkRigPart = localUserNetworkRigPart;
        }
        #endregion

        #region Monobehaviour
        protected virtual void Awake()
        {
            RegisterToHardwareRig();
        }

        protected virtual void OnDestroy()
        {
            if (hardwareRig != null)
            {
                hardwareRig.UnregisterHardwareRigPart(this);
            }
        }
        #endregion

        #region Rig detection
        protected virtual void DetectHardwareRig()
        {
            if (hardwareRig != null) return;
            hardwareRig = GetComponentInParent<IMovableHardwareRig>(true);
        }

        protected virtual bool CanRegisterToHardwareRig => Kind != RigPartKind.Undefined;

        protected virtual void RegisterToHardwareRig()
        {
            if (CanRegisterToHardwareRig)
            {
                DetectHardwareRig();
                if (hardwareRig != null)
                {
                    hardwareRig.RegisterHardwareRigPart((IHardwareRigPart)this);
                }
                UpdateTrackingStatus();
            }
        }

        protected virtual void Update()
        {
            UpdateTrackingStatus();
        }
        #endregion

        protected virtual void LateUpdate() { }
    }

    // For rig parts with a defined side
    public abstract class BaseLateralizedHardwareRigPart : BaseHardwareRigPart, ILateralizedRigPart
    {
        public RigPartSide _side;

        #region ILateralizedRigPart
        public RigPartSide Side
        {
            get
            {
                return _side;
            }
            set
            {
                _side = value;
                if (Application.isPlaying)
                {
                    RegisterToHardwareRig();
                }
            }
        }
        #endregion

        #region Rig detection (add leteralization definition requirement)
        protected override bool CanRegisterToHardwareRig => base.CanRegisterToHardwareRig && Side != RigPartSide.Undefined;
        #endregion
    }
}


