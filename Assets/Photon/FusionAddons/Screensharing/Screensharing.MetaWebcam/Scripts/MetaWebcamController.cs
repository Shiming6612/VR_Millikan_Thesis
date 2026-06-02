using PassthroughCameraSamples;
using Photon.Voice;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;

namespace Fusion.Addons.ScreenSharing
{
#if PHOTON_VOICE_VIDEO_AVAILABLE && PHOTON_VOICE_VIDEO_ENABLE
    public class MetaWebcamController : MonoBehaviour, IEmitterController
    {
        [SerializeField]
        WebCamTextureManager webCamTextureManager;
        [SerializeField]
        bool useInEditor = true;
        protected virtual void Awake()
        {
            if (webCamTextureManager == null)
            {
                webCamTextureManager = FindAnyObjectByType<WebCamTextureManager>(FindObjectsInactive.Include);
            }
            if (webCamTextureManager == null)
            {
                Debug.LogError("Missing WebCamTextureManager");
                return;
            }
        }

        public bool IsPlatformController() {
#if UNITY_ANDROID && !UNITY_EDITOR
            return true;
#endif
            return useInEditor;
        }

        public async Task WaitForWebcamAvailability()
        {
            if (webCamTextureManager == null) return;

#if UNITY_ANDROID && !UNITY_EDITOR
            while(webCamTextureManager == null || webCamTextureManager.WebCamTexture == null)
            {
                await Task.Delay(1000);
            }
#else
            await Task.FromResult(false);
#endif
        }

        public void OnStopEmitting()
        {
            if (webCamTextureManager == null) return;
            StartCoroutine(WebcamReset());
        }

        public void OnStartEmitting()
        {
            if (webCamTextureManager == null) return;
            webCamTextureManager.enabled = false;
        }

        IEnumerator WebcamReset()
        {
            webCamTextureManager.enabled = false;
            Debug.LogError("Shuting down camera ...");
            yield return new WaitForSeconds(1);
            Debug.LogError("Reactivating camera");
            webCamTextureManager.enabled = true;
        }

        public bool ShouldForceEmissionResolution(out Vector2Int resolution)
        {
            resolution = Vector2Int.zero;
            Vector2Int nullResolution = new Vector2Int(0, 0);
            Vector2Int lowResolution = new Vector2Int(320, 240);
            Vector2Int mediumResolution = new Vector2Int(640, 480);
            Vector2Int highResolution = new Vector2Int(800, 600);
            Vector2Int maxResolution = new Vector2Int(1280, 960);

            resolution = new Vector2Int(1024, 768);

            if (webCamTextureManager == null) return false;


            if (webCamTextureManager.RequestedResolution == lowResolution || webCamTextureManager.RequestedResolution == mediumResolution || webCamTextureManager.RequestedResolution == highResolution)
            {
                resolution = webCamTextureManager.RequestedResolution;
            }
            else if (webCamTextureManager.RequestedResolution == maxResolution || webCamTextureManager.RequestedResolution == nullResolution)
            {
                resolution = maxResolution;
            }
            Debug.Log($"Configured Video Resolution {resolution.x}x{resolution.y}");
            return true;
        }

        public DeviceInfo? WebcamDeviceInfo()
        {
            if (webCamTextureManager == null) return null;

#if UNITY_ANDROID && !UNITY_EDITOR
        //TODO find appropriate eye camera
        var devices = WebCamTexture.devices;
        WebCamDevice actualDevice = default;
        int deviceIndex = -1;
        if (PassthroughCameraUtils.EnsureInitialized() && PassthroughCameraUtils.CameraEyeToCameraIdMap.TryGetValue(webCamTextureManager.Eye, out var cameraData))
        {
            if (cameraData.index < devices.Length)
            {
                actualDevice = devices[cameraData.index];
                deviceIndex = cameraData.index;
                Debug.LogError($"[WebcamDeviceInfo] Webcam: {actualDevice} name:{actualDevice.name} index:{cameraData.index} id:{cameraData.id}");
            }
        }

        Debug.LogError("[WebcamDeviceInfo] Camera: "+ webCamTextureManager.WebCamTexture.deviceName);
#endif
            return null;
        }
    }
#else
    public class MetaWebcamController : MonoBehaviour { }
#endif
}
