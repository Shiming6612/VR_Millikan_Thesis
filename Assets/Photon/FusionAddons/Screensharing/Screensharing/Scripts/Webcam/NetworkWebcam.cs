using UnityEngine;
#if PHOTON_VOICE_VIDEO_ENABLE
using Photon.Voice;
#endif

namespace Fusion.Addons.ScreenSharing
{
#if PHOTON_VOICE_VIDEO_ENABLE
    public class NetworkWebcam : NetworkBehaviour, ScreenSharingScreen.IScreenSharingScreenListener
    {
        ScreenSharingScreen screen;
        public WebcamEmitter emitter;
        [SerializeField] bool startEmittingAtStart = true;

        public enum ScreenMode
        {
            DisplayPreviewOnLocalScreen,
            HideLocalScreen,
            UseEmitterDefault
        }

        public ScreenMode screenMode = ScreenMode.DisplayPreviewOnLocalScreen;

        private void Awake()
        {
            screen = GetComponentInChildren<ScreenSharingScreen>();
            if (screen && screen.listeners.Contains(this) == false)
            {
                screen.listeners.Add(this);
            }
            emitter = FindAnyObjectByType<WebcamEmitter>(FindObjectsInactive.Include);
        }

        private void OnDestroy()
        {
            if (screen) screen.listeners.Remove(this);
        }

        public override void Spawned()
        {
            base.Spawned();
            if (Object.HasStateAuthority && emitter)
            {
                UpdateLocalScreenDisplay();
                emitter.networkScreenContainer = Object;

                if (startEmittingAtStart)
                {
                    emitter.StartEmitting();
                }
            }
        }

        public void ChangeScreenMode(ScreenMode mode)
        {
            screenMode = mode;
            UpdateLocalScreenDisplay();
        }

        public void UpdateLocalScreenDisplay()
        {
            if (screen && screen.screenRenderer)
            {
                if (screenMode == ScreenMode.DisplayPreviewOnLocalScreen)
                {
                    emitter.SetPreviewScreen(screen);
                }
                if (screenMode == ScreenMode.HideLocalScreen)
                {
                    emitter.SetPreviewScreen(null);
                    screen.screenRenderer.enabled = false;
                }
            }
        }

        public void PlaybackEnabled(Material videoMaterial, IVideoPlayer videoPlayer, int playerId, object userData)
        {
        }

        public void PlaybackDisabled(IVideoPlayer videoPlayer)
        {
        }

        
        public void ToggleEmitting()
        {
            Debug.Log("NetworkWebcam Toggle Emitting");
            if (emitter == null) return;

            emitter.ToggleEmitting();
        }

    }
#else
    public class NetworkWebcam : NetworkBehaviour { }
#endif

}
