// Uncomment nextr line if a Photon video SDK version earlier than 2.52 is used
//#define VIDEOSDK_251  

// Uncomment nextr line if a Photon video SDK version earlier than 2.59 is used (and above or equal to 2.52)
//#define VIDEOSDK_258 

#if U_WINDOW_CAPTURE_RECORDER_ENABLE
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
#define UWC_EMITTER_ENABLED
#endif
#endif

#if PHOTON_VOICE_VIDEO_ENABLE

using Photon.Voice;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using UnityEngine;
#if UWC_EMITTER_ENABLED
using uWindowCapture;
#endif

/***
 * 
 * ScreenSharingEmitter uses Photon Voice channel to stream screen sharing images.
 * 
 * When the user wants to start a screen sharing using the UI buttons (see EmitterMenu), the ConnectScreenSharing() method is called.
 *  - ScreenSharingEmitter will first wait for the voice session initialization to be finished,
 *  - Then, it will also wait for the uWindowCapture initialization to be finished (the uWindowCaptureRecorder class implementing the IVideoRecorderPusher interface allows to collect frames for the PhotonVideo SDK),
 *  - When everything is ready, a transmission channel (a "voice") is created by calling VoiceClient.CreateLocalVoiceVideo on the FusionVoiceClient: from now on, the recorder will stream the desktop capture.
 *  - If an IEmitterListener is provided, it will be warn of the emission start with the OnStartEmitting callback.
 * 
 * The DisconnectScreenSharing() method is called when the user stops the screensharing. If an IEmitterListener is provided, it will be warn of the emission end with the OnStopEmitting callback.
 * 
 * Note: if not present in the scene, uWindowCaptureRecorder will create a uWindowCaptureHost (allowing to configure uWindowsCapture integration) that will be automatically configured by the ScreenSharingEmitter
 ***/
public class ScreenSharingEmitter : MonoBehaviour
{
    public interface IEmitterListener
    {
        public void OnStartEmitting(ScreenSharingEmitter emitter);
        public void OnStopEmitting(ScreenSharingEmitter emitter);

    }

    public bool startSharingOnVoiceConnectionAvailable = false;
#if UWC_EMITTER_ENABLED
    [HideInInspector]
    public uWindowCaptureRecorder screenRecorder;
    [HideInInspector]
    public uWindowCaptureHost captureHost;
#endif
    public UnityEngine.UI.Image previewImage;
    public Renderer previewRenderer;
    [Tooltip("A gameobject to display when not offline (could containt a UWC texture to display preview before emitting for instance)")]
#if UWC_EMITTER_ENABLED
    public UwcWindowTexture offlinePreview;
#endif
    public IEmitterListener listener;

#if VIDEOSDK_251
#else
    // Separate media in channels for better Photon transport performance
    public int videoChannel = 3;
#endif

    public enum Status
    {
        NotEmitting,
        WaitingVoiceConnection,
        WaitingScreenCaptureAvailability,
        Emitting,

    }
    public Status status = Status.NotEmitting;

    [System.Serializable]
    public struct ScreenSharingSettings
    {
        public Photon.Voice.Codec VideoCodec;
        public bool UseScreenshareResolution;
        public int VideoWidth;
        public int VideoHeight;
        public int VideoBitrate;
        public int AudioBitrate;
        public int VideoFPS;
        public int CaptureFPS;
        public int VideoKeyFrameInt;
        public int videoDelayFrames;
        // Split frames into fragments according to the size provided by the Transport
        public bool fragment;
        // Send data reliable
        public bool reliable;
#if VIDEOSDK_251
#elif VIDEOSDK_258
#else
        // Default value is used when eventBufSize = 0  (this default is 256)
        public int eventBufSize;
#endif
    }
    [SerializeField]
    ScreenSharingSettings settings = new ScreenSharingSettings
    {
        VideoCodec = Photon.Voice.Codec.VideoVP8,
        UseScreenshareResolution = true,
        VideoWidth = 1920,
        VideoHeight = 1080,
        VideoBitrate = 10000000,
        AudioBitrate = 30000,
        VideoFPS = 3,
        CaptureFPS = 3,
        VideoKeyFrameInt = 180,
        videoDelayFrames = 0,
        reliable = false,
#if VIDEOSDK_251
#else
        fragment = false,
#endif

#if VIDEOSDK_251
#elif VIDEOSDK_258
#else
        eventBufSize = 4 * 256, 
#endif
    };
    private Photon.Voice.ILogger logger;
    public FusionVoiceClient fusionVoiceClient;

    bool didVoiceConnectionJoined = false;
    LocalVoiceVideo localVoiceVideo;

    public bool screenSharingInProgress = false;

    bool desktopIndexInitialized = false;

    [SerializeField] int _desktopIndex = 0;

    // Used when the index is changed in the editor
    int lastDesktopIndex = 0;

    public int DesktopIndex
    {
        get { return _desktopIndex; }
        set { 
                _desktopIndex = value;
#if UWC_EMITTER_ENABLED
                if(captureHost)
                {
                    captureHost.DesktopIndex = _desktopIndex;
                }
#endif
            }
    }

    private void Awake()
    {
        if (fusionVoiceClient == null)
        {
            Debug.LogError("ScreenSharingEmitter videoConnection not set: searching it");
            fusionVoiceClient = FindAnyObjectByType<FusionVoiceClient>(FindObjectsInactive.Include);
        }

        if (previewRenderer)
            previewRenderer.enabled = false;

        if (previewImage)
            previewImage.enabled = false;

#if UWC_EMITTER_ENABLED
        if (offlinePreview)
            offlinePreview.gameObject.SetActive(true);
#endif
        SelectDesktop(DesktopIndex);
    }

    private void Start()
    {
        logger = new Photon.Voice.Unity.Logger();
    }

    private void Update()
    {
        if (!didVoiceConnectionJoined && fusionVoiceClient && fusionVoiceClient.ClientState == Photon.Realtime.ClientState.Joined)
        {
            didVoiceConnectionJoined = true;
            OnVoiceJoined();
        }

        if (lastDesktopIndex != DesktopIndex)
        {
            SelectDesktop(DesktopIndex);
        }

#if UWC_EMITTER_ENABLED
        if (offlinePreview && offlinePreview.desktopIndex != DesktopIndex && UwcManager.desktopCount > DesktopIndex)
        {
            // Setting the offline previexw desktop index was not possible before (UwcManager was not yet setup probably), but it is now
            offlinePreview.desktopIndex = DesktopIndex;
        }
#endif
    }

    public void OnVoiceJoined()
    {
        if (!enabled) return;
        if (startSharingOnVoiceConnectionAvailable) ConnectScreenSharing();
    }

#if UWC_EMITTER_ENABLED

    object emitterUserData = null;

    void AddCameraScreensharing(object userData = null)
    {
        status = Status.WaitingScreenCaptureAvailability;
        emitterUserData = userData;

        if (screenRecorder == null)
        {
#if VIDEOSDK_258
            screenRecorder = new Photon.Voice.Unity.uWindowCaptureRecorder(gameObject);
#else
            screenRecorder = new Photon.Voice.Unity.uWindowCaptureRecorder(gameObject, fps: settings.CaptureFPS);
#endif
        }
        if (captureHost == null)
        {
            // If not present in the scene, uWindowCaptureRecorder will create a uWindowCaptureHost
            captureHost = GameObject.FindObjectOfType<Photon.Voice.Unity.uWindowCaptureHost>();
            captureHost.Type = global::uWindowCapture.WindowTextureType.Desktop;
        }
        if(captureHost != null)
        {
            captureHost.DesktopIndex = DesktopIndex;
        }
        if (screenRecorder != null)
        {
            screenRecorder.OnReady += UWCRecorderReady;
        }
    }

    private void UWCRecorderReady(uWindowCaptureRecorder uwcRecorder)
    {

        if (status == Status.Emitting)
        {
            return;
        }
        if (captureHost)
        {
            captureHost.DesktopIndex = DesktopIndex;
        }
        status = Status.Emitting;
        Debug.Log("UWCRecorderReady");
        if (listener != null) listener.OnStartEmitting(this);

        // Prepare encoder
        int width = settings.UseScreenshareResolution ? uwcRecorder.Width : settings.VideoWidth;
        int height = settings.UseScreenshareResolution ? uwcRecorder.Height : settings.VideoHeight;

#if VIDEOSDK_258
        captureHost.encoderFPS = settings.CaptureFPS;
#endif

        var info = VoiceInfo.CreateVideo(settings.VideoCodec, settings.VideoBitrate, width, height, settings.VideoFPS, settings.VideoKeyFrameInt, emitterUserData);
        Debug.Log($"CreateVideo {settings.VideoCodec}, {settings.VideoBitrate}, {width}, {height}, {settings.VideoFPS}, {settings.VideoKeyFrameInt}");
        uwcRecorder.Encoder = Platform.CreateDefaultVideoEncoder(logger, info);

        // Prepare voice
#if VIDEOSDK_251
        localVoiceVideo = fusionVoiceClient.VoiceClient.CreateLocalVoiceVideo(info, uwcRecorder);
#elif VIDEOSDK_258
        localVoiceVideo = fusionVoiceClient.VoiceClient.CreateLocalVoiceVideo(info, uwcRecorder, videoChannel);
        localVoiceVideo.Fragment = settings.fragment;
#else
        localVoiceVideo = fusionVoiceClient.VoiceClient.CreateLocalVoiceVideo(info, uwcRecorder, videoChannel, new VoiceCreateOptions() { EventBufSize = settings.eventBufSize });
        localVoiceVideo.Fragment = settings.fragment;
#endif
        localVoiceVideo.Encrypt = false;
        localVoiceVideo.Reliable = settings.reliable;

        if (previewRenderer)
        {
            previewRenderer.enabled = true;
            previewRenderer.material = Photon.Voice.Unity.VideoTexture.Shader3D.MakeMaterial(uwcRecorder.PlatformView as Texture, Flip.Vertical);
        }
        if (previewImage)
        {
            previewImage.enabled = true;
            previewImage.material = Photon.Voice.Unity.VideoTexture.Shader3D.MakeMaterial(uwcRecorder.PlatformView as Texture, Flip.Vertical);
            previewImage.SetAllDirty();
        }
        if (offlinePreview != null)
        {
            offlinePreview.gameObject.SetActive(false);
        }

        fusionVoiceClient.VoiceClient.SetRemoteVoiceDelayFrames(settings.VideoCodec, settings.videoDelayFrames);
    }

    /// <summary>
    /// Change the screen captured by uWindowCapture. restart the connection if we were emitting
    /// </summary>
    /// <param name="desktopID">Screen id, starting at 0</param>
    public void SelectDesktop(int desktopID)
    {
        Debug.Log($"Desktop {desktopID} selected");
        bool shouldReconnect = false;
        if (status == Status.Emitting)
        {
            DisconnectScreenSharing();
            shouldReconnect = true;
        }
        lastDesktopIndex = desktopID;
        DesktopIndex = desktopID;
        if (offlinePreview)
        {
            offlinePreview.desktopIndex = desktopID;
        }
        if (shouldReconnect)
        {
            ConnectScreenSharing();
        }
    }

    [ContextMenu("ConnectScreenSharing")]
    public async void ConnectScreenSharing()
    {
        Debug.Log("ConnectScreenSharing...");
        status = Status.WaitingVoiceConnection;
        while (this != null && didVoiceConnectionJoined == false)
        {
            Debug.Log($"Screen sharing connection requested. Waiting for Photon voice connection ({(fusionVoiceClient ? fusionVoiceClient.ClientState : "")}) ...");
            await System.Threading.Tasks.Task.Delay(1000);
        }
        screenSharingInProgress = true;
        AddCameraScreensharing();
    }

    [ContextMenu("DisconnectScreenSharing")]
    public void DisconnectScreenSharing()
    {
        Debug.Log("DisconnectScreenSharing...");

        status = Status.NotEmitting;
        screenSharingInProgress = false;
        if (localVoiceVideo != null)
        {
            localVoiceVideo.RemoveSelf();
            localVoiceVideo = null;
        }

        if(screenRecorder?.OnReady != null)
            screenRecorder.OnReady -= UWCRecorderReady;

        if (listener != null) listener.OnStopEmitting(this);

        if (screenRecorder != null)
        {
            screenRecorder.Dispose();
            screenRecorder = null;
        }
        if (previewRenderer)
        {
            previewRenderer.enabled = false;
        }
        if (previewImage) previewImage.enabled = false;
        if (offlinePreview)
        {
            offlinePreview.gameObject.SetActive(true);
        }
    }
#else

    public void SelectDesktop(int desktopID)
    {
        Debug.LogError("Only compatible with Windows build with U_WINDOW_CAPTURE_RECORDER_ENABLE");
    }

    public void ConnectScreenSharing()
    {
        Debug.LogError("Only compatible with Windows build with U_WINDOW_CAPTURE_RECORDER_ENABLE");
    }

    public void DisconnectScreenSharing()
    {
        Debug.LogError("Only compatible with Windows build with U_WINDOW_CAPTURE_RECORDER_ENABLE");
    }
#endif
}
#else
    public class ScreenSharingEmitter : UnityEngine.MonoBehaviour 
    {
       
    }
#endif

