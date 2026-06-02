using UnityEngine;
using UnityEngine.Events;

namespace Fusion.XR.Shared.Core
{
    public interface IGrabbable : IUnityBehaviour
    {
        bool IsGrabbed { get; }
        Vector3 LocalPositionOffset { get; }
        Quaternion LocalRotationOffset { get; }
        UnityEvent OnGrab { get; }
        UnityEvent OnUngrab { get; }
        // Can occur before actual network grabbing, as a state authority trnasfer might be needed. The gameobject passed is the grabbing event source (a grabber component usually)
        UnityEvent<GameObject> OnLocalUserGrab { get; }
    }

    public interface INetworkGrabbable : IUnityBehaviour, IGrabbable
    {
        public const int EXECUTION_ORDER = ILateralizedNetworkRigPart.EXECUTION_ORDER + 10;
        INetworkGrabber CurrentGrabber { get; }
        bool IsReceivingAuthority { get; }
    }

    public interface INetworkGrabber : INetworkObject
    {
        INetworkRigPart RigPart { get; }
        // The INetworkGrabbable is responsible to set its CurrentGrabber's GrabbedObject (mostly used in position modifier logic)
        INetworkGrabbable GrabbedObject { get; }
    }

    public static class GrabbableExtensions
    {
        public static bool IsGrabbedByLocalPlayer(this INetworkGrabbable g)
        {
            if (g != null && g.IsGrabbed)
            {
                return g.CurrentLocalPlayerGrabberHardwareRigPart() != null;
            }
            return false;
        }

        public static IHardwareRigPart CurrentLocalPlayerGrabberHardwareRigPart(this INetworkGrabbable g)
        {
            if (g != null && g.CurrentGrabber != null && g.CurrentGrabber.RigPart != null)
            {
                return g.CurrentGrabber.RigPart.LocalHardwareRigPart;
            }
            return null;
        }

        public static ILateralizedRigPart CurrentLateralizedGraber(this INetworkGrabbable g)
        {
            if (g != null && g.CurrentGrabber != null && g.CurrentGrabber.RigPart is ILateralizedRigPart lateralized)
            {
                return lateralized;
            }
            return null;
        }

        public static ILateralizedRigPart CurrentLateralizedtLocalPlayerGrabberHardwareRigPart(this INetworkGrabbable g)
        {
            if (g != null && g.CurrentLocalPlayerGrabberHardwareRigPart() is ILateralizedRigPart lateralized)
            {
                return lateralized;
            }
            return null;
        }

        public static RigPartSide CurrentGrabberSide(this INetworkGrabbable g)
        {
            var lateralizedGrabber = g.CurrentLateralizedGraber();
            if (lateralizedGrabber != null)
            {
                return lateralizedGrabber.Side;
            }
            return RigPartSide.Undefined;
        }

        public static RigPartSide CurrentLocalPlayerHardwareGrabberRigPartSide(this INetworkGrabbable g)
        {
            var lateralizedGrabber = g.CurrentLateralizedtLocalPlayerGrabberHardwareRigPart();
            if (lateralizedGrabber != null)
            {
                return lateralizedGrabber.Side;
            }
            return RigPartSide.Undefined;
        }

        public static IHapticFeedbackProviderRigPart CurrentLocalGrabberHapticFeedbackProvider(this INetworkGrabbable g)
        {
            var hardwareGrabberRigPart = g.CurrentLocalPlayerGrabberHardwareRigPart();
            if (hardwareGrabberRigPart is IHapticFeedbackProviderRigPart feedbackProvider)
            {
                return feedbackProvider;
            }
            return null;
        }

        public static Pose LocalOffsetToGrabber(this INetworkGrabbable grabbable, INetworkGrabber newGrabber)
        {
            // Find grabbable position/rotation in grabber referential
            var localPositionOffset = newGrabber.transform.InverseTransformPoint(grabbable.transform.position);
            var localRotationOffset = Quaternion.Inverse(newGrabber.transform.rotation) * grabbable.transform.rotation;
            return new Pose(position: localPositionOffset, rotation: localRotationOffset);
        }
    }
}

