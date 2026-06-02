using Fusion.XR.Shared.Locomotion;
#if FUSION_WEAVER
#if PHOTON_VOICE_AVAILABLE
using Photon.Voice.Fusion;
#endif
#endif
using UnityEngine;
using UnityEngine.Events;

/**
 * 
 * Enable the fade of the XR camera during voice connection (the Unity mic setup induce a slight unpleasant freeze)
 * 
 **/
public class VoiceConnectionFadeManager : MonoBehaviour
{
#if FUSION_WEAVER
#if PHOTON_VOICE_AVAILABLE
    public FusionVoiceClient fusionVoiceClient;
    [Header("Voice Callbacks")]
    public UnityEvent onVoiceConnectionJoined;

    bool didVoiceConnectionJoined = false;

    public bool autoRegisterFadeOutOnVoiceConnection = true;

    protected virtual void Awake()
    {
        // Find the VoiceConnection, if not defined
        if (fusionVoiceClient == null) fusionVoiceClient = GetComponent<FusionVoiceClient>();
        if (fusionVoiceClient == null)
        {
            Debug.LogError("Should be stored next to a FusionVoiceClient (or fusionVoiceClient should be set)");
            return;
        }

        if (autoRegisterFadeOutOnVoiceConnection)
        {
            if (fusionVoiceClient)
            {
                foreach (var fader in FindObjectsByType<Fader>(FindObjectsSortMode.None))
                {
                    fader.startFadeLevel = 1;
                }
            }
        }
    }

    void FadeOutOnVoiceConnection()
    {
        if (!autoRegisterFadeOutOnVoiceConnection) return;
        foreach (var fader in FindObjectsByType<Fader>(FindObjectsSortMode.None))
        {
            fader.AnimateFadeOut(1);
        }
    }

    protected virtual void Update()
    {
        if (!didVoiceConnectionJoined && fusionVoiceClient && fusionVoiceClient.ClientState == Photon.Realtime.ClientState.Joined)
        {
            didVoiceConnectionJoined = true;
            OnVoiceConnectionJoined();
        }
    }
    
    protected virtual void OnVoiceConnectionJoined()
    {
        FadeOutOnVoiceConnection();
        if (onVoiceConnectionJoined != null) onVoiceConnectionJoined.Invoke();
    }
#endif
#endif
}
