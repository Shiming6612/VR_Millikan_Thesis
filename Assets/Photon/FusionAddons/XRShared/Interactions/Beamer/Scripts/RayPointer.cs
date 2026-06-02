using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Locomotion;
using UnityEngine;

namespace Fusion.XR.Shared
{
    /// <summary>
    /// Beamer used as a rig part (for network sync of target)
    /// Need to be placed on or under a hand or controller rig part hierarchy
    /// </summary>
    public class RayPointer : RayBeamer, ILateralizedHardwareRigPart
    {
        #region ILateralizedHardwareRigPart
        [HideInInspector]
        public RigPartSide Side { get; set; } = RigPartSide.Undefined;
        #endregion

        #region IHardwareRigPart
        public RigPartTrackingstatus TrackingStatus => (rigPart is IHardwareRigPart hrp && hrp.TrackingStatus == RigPartTrackingstatus.Tracked && ray.isRayEnabled) ? RigPartTrackingstatus.Tracked : RigPartTrackingstatus.NotTracked;

        public Pose RigPartPose => new Pose(ray.target, (ray.target != ray.origin)? Quaternion.LookRotation(ray.target, ray.origin) : Quaternion.identity);

        public RigPartKind Kind => RigPartKind.Pointer;

        public IRig Rig => rigPart.Rig;

        INetworkRigPart _localUserNetworkRigPart = null;
        public INetworkRigPart LocalUserNetworkRigPart => _localUserNetworkRigPart;

        // should be called by the local user network rig
        public void RegisterLocalUserNetworkRigPart(INetworkRigPart localUserNetworkRigPart)
        {
            _localUserNetworkRigPart = localUserNetworkRigPart;
        }

        public void UpdateTrackingStatus() { }
        #endregion

        bool registered = false;

        public override void Awake()
        {
            base.Awake();

            if(Side == RigPartSide.Undefined && rigPart != null)
            {
                Side = rigPart.Side;
            }
            else
            {
                Debug.LogError("Unable to determine beamer side");
            }
            TryRegister();
        }

        public override void Update()
        {
            base.Update();
            TryRegister();
        }

        void TryRegister()
        {
            if (registered == false && rigPart?.Rig is IHardwareRig hardwareRig)
            {
                registered = true;
                hardwareRig.RegisterHardwareRigPart(this);
            }
        }
    }

}
