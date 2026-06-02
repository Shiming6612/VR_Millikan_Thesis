using System.Collections.Generic;
using UnityEngine;

namespace Fusion.XR.Shared.Core
{
    // When enabled, when using Fusion.XR.Shared.Core's NetworkRig and NetworkRigPart subclasses,
    //  an IHardwareRig is expected to register with HardwareRigsRegistry.RegisterAvailableHardwareRig(this), and unregister when not with HardwareRigsRegistry.UnregisterAvailableHardwareRig(this)
    public interface IHardwareRig : IRig
    {
        public void RegisterHardwareRigPart(IHardwareRigPart rigPart);
        public void UnregisterHardwareRigPart(IHardwareRigPart rigPart);
        public List<IHardwareRigPart> RigParts { get; }
        public void SetRunner(NetworkRunner runner);
        public INetworkRig LocalUserNetworkRig { get; }
        // should be called by the local user network rig
        public void RegisterLocalUserNetworkRig(INetworkRig localUserNetworkRig);
    }

    public interface IMovableHardwareRig : IHardwareRig
    {
        public void Rotate(float angle, bool addSnapMovementVisualProtection);
        public void Teleport(Vector3 position, bool addSnapMovementVisualProtection);
        // If a Teleport or rotate requires time to execute due to a addSnapMovementVisualProtection=true parameter, during this time, SnapMovementInProgress should be set to true (to allow preventing parralel snap movement in locomotion logic)
        public bool SnapMovementInProgress { get; }
    }

    // An hardware component composing the hardware rig
    public interface IHardwareRigPart : IRigPart
    {
        public RigPartTrackingstatus TrackingStatus { get; }
        public void UpdateTrackingStatus();
        public Pose RigPartPose { get; }
        public INetworkRigPart LocalUserNetworkRigPart { get; }
        // should be called by the local user network rig
        public void RegisterLocalUserNetworkRigPart(INetworkRigPart localUserNetworkRigPart);
    }

    public interface ILateralizedHardwareRigPart : IHardwareRigPart, ILateralizedRigPart { }

    public interface IHardwareHand : ILateralizedHardwareRigPart, IHand {
        // Optional transform that should follow the index tip bone position
        public Transform IndexTipFollowerTransform { get; set; }
        // Optional transform that should follow the wrist bone position
        public Transform WristFollowerTransform { get; set; }
    }

    public interface IHardwareController : ILateralizedHardwareRigPart, IController { }

    public interface IHardwareHeadset : IHardwareRigPart, IHeadset { }

    public static class HardwareRigExtensions
    {
        public static IMovableHardwareRig HardwareRig(this IHardwareRigPart rigPart)
        {
            if (rigPart != null && rigPart.Rig is IMovableHardwareRig hardwareRig)
            {
                return hardwareRig;
            }
            return null;
        }

        // Update the hardware rig rotation. 
        public static void RotateAroundHeadset(this IMovableHardwareRig rig, float angle)
        {
            rig.transform.RotateAround(rig.Headset.transform.position, rig.transform.up, angle);
        }

        // Update the hardware rig position. The position given is the incoming headset ground projection 
        public static void TeleportHeadsetGroundProjection(this IMovableHardwareRig rig, Vector3 position)
        {
            Vector3 headsetOffet = rig.Headset.transform.position - rig.transform.position;
            headsetOffet.y = 0;
            rig.transform.position = position - headsetOffet;
        }
    }
}
