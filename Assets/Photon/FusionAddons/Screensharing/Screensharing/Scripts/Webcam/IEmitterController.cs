#if PHOTON_VOICE_AVAILABLE
using Photon.Voice;
#endif
using System.Threading.Tasks;
using UnityEngine;


namespace Fusion.Addons.ScreenSharing
{
    public interface IEmitterController
    {
        public Task WaitForWebcamAvailability();
#if PHOTON_VOICE_AVAILABLE
        public DeviceInfo? WebcamDeviceInfo();
#endif
        public bool IsPlatformController();
        public bool ShouldForceEmissionResolution(out Vector2Int resolution);
        public void OnStopEmitting();
        public void OnStartEmitting();
    }
}
