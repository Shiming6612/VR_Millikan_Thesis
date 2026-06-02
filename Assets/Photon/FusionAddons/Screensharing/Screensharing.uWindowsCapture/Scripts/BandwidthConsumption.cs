using ExitGames.Client.Photon;
#if FUSION_2_1
using Photon.Client;
#endif
using TMPro;
using UnityEngine;

#if PHOTON_VOICE_AVAILABLE
using Photon.Voice.Fusion;
#endif


namespace Fusion.Addons.ScreenSharing
{
    /***
     * 
     * BandwidthConsumption displays for the screensharing recorder UI:
     *  - the instant bandwidth consumption
     *  - the average bandwidth consumption
     *  - the session bandwidth consumption
     * 
     ***/
#if PHOTON_VOICE_VIDEO_ENABLE
    public class BandwidthConsumption : MonoBehaviour, IPhotonPeerListener
    {

        public TextMeshProUGUI instantBandwidthConsumptionTMP;
        public TextMeshProUGUI averageBandwidthConsumptionTMP;
        public TextMeshProUGUI sessionBandwidthConsumptionTMP;

        private float instantBandwidthConsumption = 0f;
        private float averageBandwidthConsumption = 0f;
        private float sessionBandwidthConsumptionkbits = 0f;
        private float counterWhenScreenShareStart;


        public float instantRefreshPeriod = 1f;
        public float averageRefreshPeriod = 2f;

        float nextInstantRefreshTime = 0f;
        float nextAverageRefreshTime = 0f;

        float currentTime;
        float startTime = -1;
        PhotonPeer photonPeer;

        private const string kbits = "kbits/s";

        public ScreenSharingEmitter screenSharingEmitter;

        // Start is called before the first frame update
        void Start()
        {
            screenSharingEmitter = FindAnyObjectByType<ScreenSharingEmitter>();
            if (!screenSharingEmitter)
                Debug.LogError("ScreenSharingEmitter not found for BandwidthConsumption !!");

            var fusionVoiceClient = FindAnyObjectByType<FusionVoiceClient>(FindObjectsInactive.Include);
            if (!fusionVoiceClient)
                Debug.LogError("VoiceConnection not found for BandwidthConsumption !!");

            photonPeer = fusionVoiceClient.Client.LoadBalancingPeer;
        }



        void Update()
        {
            currentTime = Time.time;

            if (currentTime > nextInstantRefreshTime)
            {
                UpdateBandwithDisplay();
                ComputeInstantBandwidth();
                nextInstantRefreshTime = currentTime + instantRefreshPeriod;
            }

            if (currentTime > nextAverageRefreshTime)
            {
                ComputeAverageBandwidth();
                nextAverageRefreshTime = currentTime + averageRefreshPeriod;
            }

            if (startTime == -1 & screenSharingEmitter.screenSharingInProgress)
                OnScreenShareStart();

            if (startTime != -1 & !screenSharingEmitter.screenSharingInProgress)
                OnScreenShareStop();

        }

        private void OnScreenShareStart()
        {
            startTime = Time.time;
            counterWhenScreenShareStart = photonPeer.BytesOut;
        }
        private void OnScreenShareStop()
        {
            startTime = -1;
        }

        private float previousInstantBandwidthConsumption = 0f;
        private float currentBandwidthConsumption = 0f;
        private void ComputeInstantBandwidth()
        {
            if (photonPeer == null) return;
            currentBandwidthConsumption = photonPeer.BytesOut / instantRefreshPeriod;
            instantBandwidthConsumption = currentBandwidthConsumption - previousInstantBandwidthConsumption;
            previousInstantBandwidthConsumption = currentBandwidthConsumption;
        }

        private void ComputeAverageBandwidth()
        {
            if (photonPeer == null) return;
            if (startTime != -1)
            {
                averageBandwidthConsumption = (((photonPeer.BytesOut - counterWhenScreenShareStart) / 1000) * 8) / (Time.time - startTime);
            }
        }

        private void UpdateBandwithDisplay()
        {
            if (photonPeer == null) return;

            if (instantBandwidthConsumptionTMP)
            {
                var instantBandwidthConsumptionkbits = (instantBandwidthConsumption / 1000) * 8;
                if (instantBandwidthConsumptionkbits < 1)
                    instantBandwidthConsumptionTMP.text = "<1 " + kbits;
                else
                    instantBandwidthConsumptionTMP.text = instantBandwidthConsumptionkbits.ToString("# ") + kbits;
            }

            if (averageBandwidthConsumptionTMP & startTime != -1)
            {
                if (averageBandwidthConsumption < 1)
                    averageBandwidthConsumptionTMP.text = "<1 " + kbits;
                else
                    averageBandwidthConsumptionTMP.text = averageBandwidthConsumption.ToString("# ") + kbits;
            }

            if (sessionBandwidthConsumptionTMP)
            {
                sessionBandwidthConsumptionkbits = (photonPeer.BytesOut / 1000) * 8;
                if (sessionBandwidthConsumptionkbits < 1)
                    sessionBandwidthConsumptionTMP.text = "<1 " + kbits;
                else
                    sessionBandwidthConsumptionTMP.text = sessionBandwidthConsumptionkbits.ToString("# ") + "kbits";
            }
        }

#if FUSION_2_1
        public void DebugReturn(global::Photon.Client.LogLevel level, string message)
        {
            Debug.LogError($"BandwidthConsumption DebugReturn : Level:{level} Message:{message}");
        }

        public void OnMessage(bool isRawMessage, object message)
        {
        }

        public void OnDisconnectMessage(DisconnectMessage dm)
        {
        }
#else
        public void DebugReturn(DebugLevel level, string message)
        {
            Debug.LogError($"BandwidthConsumption DebugReturn : Level:{level} Message:{ message}");
        }
#endif

        public void OnEvent(EventData eventData)
        {
            Debug.LogError($"BandwidthConsumption OnEvent : eventData:{eventData}");
        }

        public void OnOperationResponse(OperationResponse operationResponse)
        {
            Debug.LogError($"BandwidthConsumption OperationResponse :{operationResponse}");
        }

        public void OnStatusChanged(StatusCode statusCode)
        {
            Debug.LogError($"BandwidthConsumption OnStatusChanged :{statusCode}");
        }
}
#else
    public class BandwidthConsumption : MonoBehaviour { }
#endif
}
