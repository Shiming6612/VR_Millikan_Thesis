#if FUSION_WEAVER 
#if PHOTON_VOICE_AVAILABLE
using Photon.Voice.Fusion;
#endif
#endif
using UnityEngine;

namespace Fusion.Addons.VisionOsHelpers
{
    /**
    * 
    * Script to trigger volume changes, and disconnect voice before changing volume mode (cause audio disconnection, disconnecting it manually allows to have a cleaner result)
    * GoingToBoundedMode and GoingToUnboundedMode have to called by the script wanting to trigger a volume change,
    *  and VolumeCameraConfigurationSelector components are require to store and describe the target volume configurations
    **/
    public class VoiceModeFollower : MonoBehaviour
    {
#if UNITY_VISIONOS && POLYSPATIAL_SDK_AVAILABLE && FUSION_WEAVER && PHOTON_VOICE_AVAILABLE
    public FusionVoiceClient voiceClient;
    public VolumeCameraConfigurationSelector boundedVolumeConf;
    public VolumeCameraConfigurationSelector unboundedVolumeConf;

    private void Awake()
    {
        if (voiceClient == null) voiceClient = FindAnyObjectByType<FusionVoiceClient>(FindObjectsInactive.Include);
    }

    private void Start()
    {
        if (boundedVolumeConf == null || unboundedVolumeConf == null)
        {
            foreach(var conf in FindObjectsByType<VolumeCameraConfigurationSelector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (boundedVolumeConf == null && conf.volumeConfiguration.Mode == Unity.PolySpatial.VolumeCamera.PolySpatialVolumeCameraMode.Bounded)
                {
                    boundedVolumeConf = conf;
                }
                if (unboundedVolumeConf == null && conf.volumeConfiguration.Mode == Unity.PolySpatial.VolumeCamera.PolySpatialVolumeCameraMode.Unbounded)
                {
                    unboundedVolumeConf = conf;
                }
            }
        }
    }

    public enum Status
    {
        NoTask,
        Disconnecting
    }

    public Status status;
    public float transitionDuration = 0.5f;

    float lastStatusChange = -1;

    [ContextMenu("GoingToBoundedMode")]
    public void GoingToBoundedMode()
    {
        ResetVoiceConnection();

        if (unboundedVolumeConf) unboundedVolumeConf.gameObject.SetActive(false);
        if (boundedVolumeConf) boundedVolumeConf.gameObject.SetActive(true);
    }

    [ContextMenu("GoingToUnboundedMode")]
    public void GoingToUnboundedMode()
    {
        ResetVoiceConnection();

        if (boundedVolumeConf) boundedVolumeConf.gameObject.SetActive(false);
        if (unboundedVolumeConf) unboundedVolumeConf.gameObject.SetActive(true);
    }

    [ContextMenu("ResetVoiceConnection")]
    void ResetVoiceConnection()
    {
        if (voiceClient == null) return;
        voiceClient.Disconnect();
        status = Status.Disconnecting;
        lastStatusChange = Time.time;
    }

    private void Update()
    {
        if(status != Status.NoTask && Time.time > (lastStatusChange + transitionDuration))
        {
            if(voiceClient) voiceClient.ConnectAndJoinRoom();
            status = Status.NoTask;
        }
    }
#endif
    }
}