using Fusion.XR.Shared.Core;
#if PHOTON_VOICE_AVAILABLE
using Photon.Voice;
#endif
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Fusion.Addons.ScreenSharing
{
    /**
     * Generic webcam controller (including authorization handling)
     */
    public class WebcamController : MonoBehaviour, IEmitterController
    {
        public int webcamIndex = 0;

        public enum Status
        {
            AccessNotRequested,
            AccessRequested,
            AccessAuthorized,
            AccessRejected
        }
        public Status status = Status.AccessNotRequested;

        public List<RuntimePlatform> controllerOnPlatforms = new List<RuntimePlatform> {
            RuntimePlatform.WebGLPlayer,
        };

        public bool IsPlatformController()
        {
            return controllerOnPlatforms.Contains(Application.platform);
        }

        IEnumerator RequestWebcamAccess()
        {
            if (IsPlatformController())
            {
                status = Status.AccessRequested;
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
                if (Application.HasUserAuthorization(UserAuthorization.WebCam))
                {
                    status = Status.AccessAuthorized;
                }
                else
                {
                    status = Status.AccessRejected;
                }
            }
        }

        public async Task WaitForWebcamAvailability()
        {
            if (IsPlatformController() && status == Status.AccessNotRequested)
            {
                StartCoroutine(RequestWebcamAccess());
                while (status == Status.AccessRequested)
                {
                    await AsyncTask.Delay(100);
                }
            }
        }

#if PHOTON_VOICE_AVAILABLE
        public DeviceInfo? WebcamDeviceInfo()
        {
            var devices = WebCamTexture.devices;
            DeviceInfo device = default;
            int i = 0;
            foreach (var d in devices)
            {
                if(i== webcamIndex)
                {
                    device = new DeviceInfo(d.name);
                    break;
                }
                i++;
            }
            return device;
        }
#endif

        public bool ShouldForceEmissionResolution(out Vector2Int resolution)
        {
            resolution = default;
            return false;
        }

        public void OnStopEmitting()
        {
        }

        public void OnStartEmitting()
        {
        }
    }
}

