using System.Collections.Generic;
using UnityEngine;

namespace Fusion.XR.Shared.Core
{
    /// <summary>
    /// An hardware rig part can optionally provide haptic feedback support
    /// </summary>
    public interface IHapticFeedbackProviderRigPart : IHardwareRigPart
    {
        // If a device supporting haptic feedback has been detected, send a vibration to it (here in the form of an impulse)
        public void SendHapticImpulse(float amplitude = 0.3f, float duration = 0.3f, uint channel = 0);
        // If a device supporting haptic feedback has been detected, send a vibration to it (here in the form of a buffer describing the vibration data)
        public void SendHapticBuffer(byte[] buffer, uint channel = 0);
        public void StopHaptics();
    }
}