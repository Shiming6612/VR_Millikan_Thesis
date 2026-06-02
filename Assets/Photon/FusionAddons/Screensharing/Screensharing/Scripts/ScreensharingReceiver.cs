#if PHOTON_VOICE_VIDEO_ENABLE
using Photon.Voice;
using Photon.Voice.Fusion;
using System;
using System.Collections.Generic;
using UnityEngine;
using IVoiceLogger = Photon.Voice.ILogger;
using VoiceUnityLogger = Photon.Voice.Unity.Logger;
using VoiceClientState = Photon.Realtime.ClientState;
using VoiceVideoTextureShader3D = Photon.Voice.Unity.VideoTexture.Shader3D;
#endif

namespace Fusion.Addons.ScreenSharing
{
#if PHOTON_VOICE_VIDEO_ENABLE

    [System.Serializable]
    public struct VideoEmissionUserData
    {
        public int rawPlayerId;
        public RuntimePlatform platform;
        public string deviceName;
        public string deviceModel;
        public NetworkId networkScreenContainerId;

        // Encore to json, with escaped braces
        public string EncodedStr()
        {
            var json = JsonUtility.ToJson(this);
            // Escaped to prevent issue with Unity format in Voice core logging
            var escapedJson = json.Replace("{", "{{").Replace("}", "}}");
            return escapedJson;
        }

        // Unencode from encoded format
        public static VideoEmissionUserData FromEncodedString(string str)
        {
            // Unescape json representation
            var userDataStr = str.Replace("{{", "{").Replace("}}", "}");
            var userData = JsonUtility.FromJson<VideoEmissionUserData>(userDataStr);
            return userData;

        }
    }

    /***
     * 
     * ScreensharingReceiver manages the reception of screen sharing streams.
     * It watchs for new voice connections, with the VoiceClient.OnRemoteVoiceInfoAction callback.
     * Upon such a connection, it creates a video player with Platform.CreateVideoPlayerUnityTexture.
     * Then, when this video player is ready (OnVideoPlayerReady), it creates a material containing the video player texture, 
     * and pass it to the ScreenSharingScreen with EnablePlayback: the screen will then change its renderer material to this new one.
     * 
     ***/
    public class ScreensharingReceiver : MonoBehaviour
    {
        public FusionVoiceClient fusionVoiceClient;

        public ScreenSharingScreen defaultRemoteScreen;
        Dictionary<int, IVideoPlayer> videoPlayerByPlayerIds = new Dictionary<int, IVideoPlayer>();
        public Dictionary<int, ScreenSharingScreen> screenByPlayerIds = new Dictionary<int, ScreenSharingScreen>();
        public Dictionary<IVideoPlayer, object> userDataForPlayer = new Dictionary<IVideoPlayer, object>();
        private IVoiceLogger logger;

        public GameObject screenPrefab;

        List<FusionVoiceClient> registeredVoiceClients = new List<FusionVoiceClient>();

        private void Start()
        {
            RegisterVoiceClient();

            logger = new VoiceUnityLogger();
            if (defaultRemoteScreen) defaultRemoteScreen.ToggleScreenVisibility(false);
        }

        private void Update()
        {
            RegisterVoiceClient();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        void Cleanup()
        {
            foreach (var v in videoPlayerByPlayerIds)
            {
                if (v.Value != null) v.Value.Dispose();
            }
            videoPlayerByPlayerIds.Clear();
        }

        private void RegisterVoiceClient()
        {
            bool registrationFinished = fusionVoiceClient != null;
            if (registrationFinished == false)
            {
                foreach (var f in FindObjectsByType<FusionVoiceClient>(FindObjectsSortMode.None))
                {
                    bool voiceClientAvailable = f.Client != null && f.VoiceClient != null;
                    if (voiceClientAvailable && registeredVoiceClients.Contains(f) == false)
                    {
                        registeredVoiceClients.Add(f);
                        Debug.Log($"[ScreenSharingReceiver] Fusion Voice client found, registering to its VoiceClient {f.name}");
                        f.VoiceClient.OnRemoteVoiceInfoAction += OnRemoteVoiceInfoAction;
                    }
                    if (voiceClientAvailable && f.ClientState == VoiceClientState.Joined)
                    {
                        // Main Fusion Voice client
                        Debug.Log($"[ScreenSharingReceiver] Main Fusion Voice client found {f.name}");
                        fusionVoiceClient = f;
                        break;
                    }
                }
            }
        }

        ScreenSharingScreen ScreenForVideoPlayer(IVideoPlayer videoPlayer)
        {
            return ScreenForVideoPlayer(videoPlayer, out _, out _);
        }

        ScreenSharingScreen ScreenForVideoPlayer(IVideoPlayer videoPlayer, out int playerId, out object userData)
        {
            Debug.Log($"[ScreenSharingReceiver] ScreenForVideoPlayer videoPlayer");
            ScreenSharingScreen screen = null;
            userData = null;
            playerId = -1;

            if (userDataForPlayer.ContainsKey(videoPlayer))
            {
                userData = userDataForPlayer[videoPlayer];
            }

            foreach (var entry in videoPlayerByPlayerIds)
            {
                if (videoPlayer == entry.Value)
                {
                    playerId = entry.Key;
                }
            }

            if (screenByPlayerIds.ContainsKey(playerId))
            {
                screen = screenByPlayerIds[playerId];
            }

            if (screen == null && defaultRemoteScreen)
            {
                screen = defaultRemoteScreen;
            }

            if (screen == null && userData != null && userData is string userDataStr)
            {
                var parsedUserData = VideoEmissionUserData.FromEncodedString(userDataStr);
                var runner = fusionVoiceClient.GetComponent<NetworkRunner>();
                if (parsedUserData.networkScreenContainerId != default && runner != null)
                {
                    if (runner.TryFindObject(parsedUserData.networkScreenContainerId, out var networkScreenContainer))
                    {
                        screen = networkScreenContainer.GetComponentInChildren<ScreenSharingScreen>();
                    }
                }
            }
            if (screen == null && screenPrefab != null)
            {
                var screenGO = GameObject.Instantiate(screenPrefab);
                screen = screenGO.GetComponentInChildren<ScreenSharingScreen>();
            }

            return screen;
        }

        // Called when a video playing stream is detected
        private void OnRemoteVoiceInfoAction(int channelId, int playerId, byte voiceId, VoiceInfo voiceInfo, ref RemoteVoiceOptions options)
        {
            Debug.Log($"[ScreenSharingReceiver] OnRemoteVoiceInfoAction {channelId} {playerId} {voiceId}");
            switch (voiceInfo.Codec)
            {
                case Codec.VideoVP8:
                case Codec.VideoVP9:
                case Codec.VideoH264:
                    if (videoPlayerByPlayerIds.ContainsKey(playerId))
                    {
                        Debug.LogError($"[ScreenSharingReceiver] Error: This player {playerId} is already sending a stream");
                        return;
                    }
                    IVideoPlayer videoPlayer = Platform.CreateVideoPlayerUnityTexture(logger, voiceInfo, (player) => {
                        videoPlayerByPlayerIds.Add(playerId, player);
                        userDataForPlayer[player] = voiceInfo.UserData;
                        
                        OnVideoPlayerReady(player, voiceInfo);
                    });

                    Debug.Log($"[ScreenSharingReceiver] ScreenSharingReceiver.OnRemoteVoiceInfoAction: Decoder: {videoPlayer.Decoder} / UserData: {voiceInfo.UserData} / playerId: {playerId}");
                    options.Decoder = videoPlayer.Decoder;


                    options.OnRemoteVoiceRemoveAction += () =>
                        {
                            Debug.Log($"[ScreenSharingReceiver] OnRemoteVoiceRemoveAction playerId:{playerId} videoPlayer:{videoPlayer}");
                            var screen = ScreenForVideoPlayer(videoPlayer);
                            if (screen)
                            {
                                screen.DisablePlayback(videoPlayer);
                            }
                            videoPlayer.Dispose();
                            videoPlayerByPlayerIds.Remove(playerId);
                            userDataForPlayer.Remove(videoPlayer);
                        };

                    break;
                default:
                    Debug.Log($"[ScreenSharingReceiver] Voice Info: {voiceInfo.Codec} {voiceInfo}");
                    break;
            }
        }

        private void OnApplicationQuit()
        {
            Cleanup();
        }

        private void OnVideoPlayerReady(IVideoPlayer videoPlayer, VoiceInfo voiceInfo)
        {
            Debug.Log($"[ScreenSharingReceiver] OnVideoPlayerReady videoPlayer");
            ScreenSharingScreen screen = ScreenForVideoPlayer(videoPlayer, out int playerId, out object userData);

            var projection = screen.GetComponent<ScreenSharingScreenTextureProjection>();
            if (projection)
            {
                projection.lowerResFPS = voiceInfo.FPS;
            }

            if (videoPlayer.PlatformView is Texture)
            {
                if (screen != null)
                {
                    try
                    {
                        screen.EnablePlayback(videoPlayer, playerId, userData, new Vector2Int(voiceInfo.Width, voiceInfo.Height), voiceInfo.FPS);
                    }
                    catch (Exception e)
                    {
                        Debug.LogErrorFormat("[ScreenSharingReceiver] Error while creating video material: " + e.Message);
                    }
                }
                else
                {
                    Debug.LogError($"[ScreenSharingReceiver] No screen for video player {videoPlayer} / playerId: {playerId} / userData: {userData}");
                }
            }
        }
    }
#else
public class ScreensharingReceiver : UnityEngine.MonoBehaviour
{
#warning Screensharing add-on installed, but PHOTON_VOICE_VIDEO_ENABLE not set in project defines: select Window -> Photon Voice -> Enable Video from the Editor menu (or manually add PHOTON_VOICE_VIDEO_ENABLE scripting define symbol for each platform)
}
#endif
}

