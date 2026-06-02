using UnityEngine;

namespace Fusion.XR.Shared.Core
{
    public interface IGrabbingProvider : IHardwareRigPart
    {
        public bool IsGrabbing { get; }
    }

    public interface IOverridableGrabbingProvider : IGrabbingProvider
    {
        public void OverrideGrabbing(bool isGrabbing);
    }
}
