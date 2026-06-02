using System.Collections.Generic;
using UnityEngine;

namespace Fusion.XR.Shared.Core
{
    #region Fusion interface
    public interface IUnityBehaviour
    {
        Transform transform { get; }
        public GameObject gameObject { get; }
        public bool isActiveAndEnabled { get; }
    }

    public interface INetworkObject : IUnityBehaviour
    {
        NetworkObject Object { get; }
        NetworkRunner Runner { get; }
   }
    #endregion

    public enum RigPartKind
    {
        Undefined,
        Headset,
        Controller,
        Hand,
        Stylus,
        RigCenter,
        Pointer,
        // Prereserved kind, might be renamed in future version
        Other1,
        Other2,
        Other3,
        Other4,
        Other5,
        Other6,
        Other7,
        Other8,
        Other9,
        Other10,
    }

    public enum RigPartSide
    {
        Undefined, Left, Right
    }

    // Some rig parts (hands, controllers) have a side associated to them
    public interface ILateralizedRigPart : IRigPart
    {
        public RigPartSide Side { get; }
    }

    public enum RigPartTrackingstatus
    {
        NotTracked,
        Tracked
    }

    public interface IRigPart : IUnityBehaviour
    {
        public RigPartKind Kind { get; }
        public IRig Rig { get; }
    }

    public interface IHeadset : IRigPart { }
    public interface IController : ILateralizedRigPart { }
    public interface IHand : ILateralizedRigPart { }

    public interface IRig : IUnityBehaviour
    {
        public NetworkRunner Runner { get; }
        public IHeadset Headset { get; }
    }

    public static class RigExtensions
    {
        public static bool IsOnline(this IRig rig)
        {
            if (rig.Runner == null)
            {
                return false;
            }
            return rig.Runner.State == NetworkRunner.States.Running;
        }

        public static bool IsOnline(this IRigPart rigPart)
        {
            if (rigPart.Rig == null)
            {
                return false;
            }
            return rigPart.Rig.IsOnline();
        }
    }
}