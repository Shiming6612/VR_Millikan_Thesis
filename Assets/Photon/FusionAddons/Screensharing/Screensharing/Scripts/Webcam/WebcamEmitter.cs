// Uncomment nextr line if a Photon video SDK version earlier than 2.59 is used (and above or equal to 2.52)
#define VIDEOSDK_258 

#if PHOTON_VOICE_VIDEO_ENABLE
using Photon.Voice;
using Photon.Voice.Fusion;
using UnityEngine;
using VoiceILogger = Photon.Voice.ILogger;
using UnityLogger = Photon.Voice.Unity.Logger;
using VoiceClientstate = Photon.Realtime.ClientState;
using VideoTextureShader3D = Photon.Voice.Unity.VideoTexture.Shader3D;
using Fusion.XR.Shared.Core;
using UnityEngine.Events;
#endif

namespace Fusion.Addons.ScreenSharing
{
#if PHOTON_VOICE_VIDEO_ENABLE
    public class WebcamEmitter : MonoBehaviour
    {
        public interface IEmitterListener
        {
            public void OnStartEmitting(WebcamEmitter emitter);
            public void OnStopEmitting(WebcamEmitter emitter);
        }

        public UnityEvent onStartEmitting;
        public UnityEvent onStopEmitting;

        public bool startEmittingOnVoiceConnectionAvailable = false;


        public IEmitterListener listener;
        VoiceInfo info;

        // Separate media in channels for better Photon transport performance
        public int videoChannel = 3;

        public enum Status
        {
            NotEmitting,
            WaitingVoiceConnection,
            WaitingRecorderTextureAvailability,
            Emitting,

        }
        public Status status = Status.NotEmitting;

        [System.Serializable]
        public struct Settings
        {
            public Codec VideoCodec;
            public bool UseRecorderResolution;
            public int VideoWidth;
            public int VideoHeight;
            public int VideoBitrate;
            public int AudioBitrate;
            public int VideoFPS;
            public int VideoKeyFrameInt;
            public int videoDelayFrames;
            // Split frames into fragments according to the size provided by the Transport
            public bool fragment;
            // Send data reliable
            public bool reliable;
        }
        
        [SerializeField]
        Settings settings = new Settings
        {
            VideoCodec = Codec.VideoH264,
            UseRecorderResolution = true,
            VideoWidth = 1024,
            VideoHeight = 768,
            VideoBitrate = 400000,
            AudioBitrate = 30000,
            VideoFPS = 3,
            VideoKeyFrameInt = 20,
            videoDelayFrames = 0,
            reliable = true,
            fragment = true,
        };

        private VoiceILogger logger;
        public FusionVoiceClient fusionVoiceClient;

        bool didVoiceConnectionJoined = false;
        LocalVoiceVideo localVoiceVideo;

        public bool emissionInProgress = false;

        object emitterUserData = null;

        public IVideoRecorder recorder;
        public IEmitterController emitterController;

        [Header("Preview")]
        public ScreenSharingScreen previewScreen;

        public enum UserDataContent
        {
            PlayerDetails,
            Custom
        }

        public UserDataContent userDataContent = WebcamEmitter.UserDataContent.PlayerDetails;
        public NetworkObject networkScreenContainer = null;

        float lastToggle = 0f;
        float bouncePreventionDelay = 0.3f;

        protected virtual void Awake()
        {
            if (emitterController == null)
            {
                foreach (var c in GetComponentsInChildren<IEmitterController>())
                {
                    if (c.IsPlatformController())
                    {
                        emitterController = c;
                        break;
                    }
                }
            }
        }

        private void Start()
        {
            logger = new UnityLogger();
        }

        private void Update()
        {
            if (fusionVoiceClient == null)
            {
                foreach(var f in FindObjectsByType<FusionVoiceClient>(FindObjectsSortMode.None))
                {
                    if (f.ClientState == VoiceClientstate.Joined)
                    {
                        fusionVoiceClient = f;
                        break;
                    }
                }
            }
            if (!didVoiceConnectionJoined && fusionVoiceClient && fusionVoiceClient.ClientState == VoiceClientstate.Joined)
            {
                didVoiceConnectionJoined = true;
                OnVoiceJoined();
            }
        }

        public void OnVoiceJoined()
        {
            if (!enabled) return;
            if (startEmittingOnVoiceConnectionAvailable) StartEmitting();
        }
        
        [ContextMenu("Start Emitting")]
        public async void StartEmitting()
        {
            Debug.Log("StartEmitting ...");
            status = Status.WaitingVoiceConnection;
            while (this != null && didVoiceConnectionJoined == false)
            {
                Debug.Log($"Texture emission connection requested. Waiting for Photon voice connection ({(fusionVoiceClient ? fusionVoiceClient.ClientState : "")}) ...");
                await AsyncTask.Delay(1000);
            }
            emissionInProgress = true;
            status = Status.WaitingRecorderTextureAvailability;

            if (userDataContent == UserDataContent.PlayerDetails)
            {
                var runner = fusionVoiceClient.GetComponent<NetworkRunner>();
                if (runner)
                {
                    var userData = new VideoEmissionUserData
                    {
                        rawPlayerId = runner.LocalPlayer.RawEncoded,
                        platform = Application.platform,
                        deviceName = SystemInfo.deviceName,
                        deviceModel = SystemInfo.deviceModel,
                    };
                    if (networkScreenContainer)
                    {
                        userData.networkScreenContainerId = networkScreenContainer.Id;
                    }
                    emitterUserData = userData.EncodedStr();
                }
            }

            InitializeRecorder();
        }

        [ContextMenu("Stop Emitting")]
        public void StopEmitting()
        {
            Debug.Log("StopEmitting...");

            status = Status.NotEmitting;
            emissionInProgress = false;
            if (localVoiceVideo != null)
            {
                localVoiceVideo.RemoveSelf();
                localVoiceVideo = null;
            }

            DesactivateRecorder();

            if (previewScreen) {
                previewScreen.ToggleScreenVisibility(false);
            }

            OnStopEmitting();
        }

        [ContextMenu("Toggle Emitting")]
        public void ToggleEmitting()
        {
            Debug.Log("Toggle Emitting");

            if (lastToggle + bouncePreventionDelay < Time.time)
            {
                if (status == WebcamEmitter.Status.NotEmitting)
                {
                    lastToggle = Time.time;
                    StartEmitting();

                }
                else if (status == WebcamEmitter.Status.Emitting)
                {
                    lastToggle = Time.time;
                    StopEmitting();
                }
            }
        }

        void OnStartEmitting()
        {
            if (listener != null) listener.OnStartEmitting(this);
            if (onStartEmitting != null) onStartEmitting.Invoke();
            if (emitterController != null)
            {
                emitterController.OnStartEmitting();
            }
        }

        void OnStopEmitting()
        {
            if (listener != null) listener.OnStopEmitting(this);
            if (onStopEmitting != null) onStopEmitting.Invoke();
            if (emitterController != null)
            {
                emitterController.OnStopEmitting();
            }
        }

        protected virtual async void InitializeRecorder()
        {
            Debug.Log("InitializeRecorder");

            if (emitterController != null)
            {
                await emitterController.WaitForWebcamAvailability();
            }

            if(emitterController != null && emitterController.ShouldForceEmissionResolution(out var forcedResolution))
            {
                settings.VideoWidth = forcedResolution.x;
                settings.VideoHeight = forcedResolution.y;
            }

            int width = settings.VideoWidth;
            int height = settings.VideoHeight;
            info = VoiceInfo.CreateVideo(settings.VideoCodec, settings.VideoBitrate, width, height, settings.VideoFPS, settings.VideoKeyFrameInt, emitterUserData);

            var device = DeviceInfo.Default;
            if (emitterController != null)
            {
                var emitterControllerDevice = emitterController.WebcamDeviceInfo();
                if (emitterControllerDevice is DeviceInfo d)
                {
                    device = d;
                }
            }

            recorder = Platform.CreateVideoRecorderUnityTexture(logger, info, device, VideoRecorderReady);
        }

        protected virtual void DesactivateRecorder()
        {
            if (recorder != null)
            {
                recorder.Dispose();
                recorder = null;
            }
        }

        protected virtual void VideoRecorderReady(IVideoRecorder readyRecorder)
        {
            status = Status.Emitting;
            OnStartEmitting();

            Debug.Log("TextureRecorderReady Encoder:" + readyRecorder.Encoder);
            if (recorder is IVideoRecorderPusher)
            {
                Debug.Log("TextureRecorderReady recorder IVideoRecorderPusher");
            }
            // Prepare voice




#if VIDEOSDK_258
            localVoiceVideo = fusionVoiceClient.VoiceClient.CreateLocalVoiceVideo(info, readyRecorder, videoChannel);
#else
            localVoiceVideo = fusionVoiceClient.VoiceClient.CreateLocalVoiceVideo(info, readyRecorder, videoChannel, new VoiceCreateOptions() { EventBufSize = 4 * 256 });
#endif
            localVoiceVideo.Fragment = settings.fragment;
            localVoiceVideo.Encrypt = false;
            localVoiceVideo.Reliable = settings.reliable;
            status = Status.Emitting;

            // Previews
            if (previewScreen) 
            {
                PreparePreviewScreen(previewScreen, readyRecorder);
            }

            fusionVoiceClient.VoiceClient.SetRemoteVoiceDelayFrames(settings.VideoCodec, settings.videoDelayFrames);
            Debug.Log("TextureRecorderReady end");
        }

        void PreparePreviewScreen(ScreenSharingScreen s, IVideoRecorder r)
        {
            if (s == null) return;
            var projection = s.GetComponent<ScreenSharingScreenTextureProjection>();
            if (projection)
            {
                projection.lowerResFPS = settings.VideoFPS;
            }
            s.ToggleScreenVisibility(true);
            if (s.screenRenderer)
            {
                if (status == Status.Emitting && s.screenRenderer && r != null)
                {
                    s.SetupMaterial(r.PlatformView as Texture, Flip.None, new Vector2Int(settings.VideoWidth, settings.VideoHeight), settings.VideoFPS);
                }
            }
        }

        public void SetPreviewScreen(ScreenSharingScreen s)
        {
            // Disable previous screen in case of change
            if (previewScreen && previewScreen != s)
            {
                previewScreen.ToggleScreenVisibility(false);
            }
            previewScreen = s;
            if (previewScreen)
            {
                PreparePreviewScreen(previewScreen, recorder);
            }
        }
    }
#else
    public class WebcamEmitter : UnityEngine.MonoBehaviour { }
#endif


}
