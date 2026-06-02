using Fusion.Sockets;
using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Core.Interaction;
using Fusion.XR.Shared.Locomotion;
using Fusion.XR.Shared.Rig;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

/**
* 
* The DisconnectionManager is in charge to display error messages when an error occurs.
* So, the application manager listens the SessionEventsManager to get informed when a network error occurs.
* 
**/

namespace Fusion.XR.Shared
{
    public class DisconnectionManager : ApplicationLifeCycleManager, INetworkRunnerCallbacks
    {
        public GameObject staticLevel;
        public GameObject interactableObjects;
        const string shutdownErrorMessage = "Look like we have a connection issue.";
        const string disconnectedFromServerErrorMessage = "Sorry, you have been disconnected ! \n\n Please restart the application.";

        [SerializeField] private Material materialForOfflineHands;

        [SerializeField] private RigInfo _rigInfo;

        RigInfo RigInfo
        {
            get
            {
                if (_rigInfo == null)
                {
                    _rigInfo = RigInfo.FindRigInfo(runner);
                }
                return _rigInfo;
            }
        }

        [Header("Desktop Rig settings")]
        public GameObject desktopErrorMessageGO;
        public TextMeshPro desktopErrorMessageTMP;

        [Header("XR Rig settings")]
        public GameObject hardwareRigErrorMessageGO;
        public TextMeshPro hardwareRigErrorMessageTMP;

        // Set to true to warn that we are disconnecting on purpose, preventing any unneeded warnings
        public bool isQuitting = false;

        public NetworkRunner runner;

        IHandRepresentationHandler leftHandHardwareHandRepresentationManager;
        IHandRepresentationHandler rightHandHardwareHandRepresentationManager;

        private void Awake()
        {
            if (runner == null) runner = GetComponentInParent<NetworkRunner>();
            if (runner == null)
            {
                Debug.LogError("ApplicationManager should be placed under a NetworkRunner hierarchy");
            }
        }

        private void Start()
        {
            if (!RigInfo)
                Debug.LogError("RigInfo not found !");
            if (runner && new List<NetworkRunner>(GetComponentsInParent<NetworkRunner>()).Contains(runner) == false)
            {
                // The DisconnectionManager is not in the hierarchy of the runner, so it has not been automatically subscribed to its callbacks
                runner.AddCallbacks(this);
            }
        }

        // ShutdownWithError is called when the application is launched without an active network connection (network interface disabled or no link for example) or if an network interface failure occurs at run
        private void Shutdown(ShutdownReason shutdownReason)
        {
            if (isQuitting) return;
            Debug.LogError($" ApplicationManager Shutdown : {shutdownReason} ");
            string details = shutdownReason.ToString();
            if (details == "Ok") details = "Connection lost";
            // The runner will be destroyed, as we launch a coroutine, we want to survive :)
            transform.parent = null;

            UpdateErrorMessage(shutdownErrorMessage + $"\n\nCause: {details}");
            StartCoroutine(CleanUpScene());
            DisplayHardwareHands();

        }

        // DisconnectedFromServer is called when the internet connection is lost.
        private void DisconnectedFromServer()
        {
            UpdateErrorMessage(disconnectedFromServerErrorMessage);
            StartCoroutine(CleanUpScene());
            DisplayHardwareHands();
        }

        // DestroyNetworkedObjects is called when the connection erros occurs in order to delete spawned objects
        private void DestroyNetworkedObjects()
        {
            // Destroy the runner to delete Network objects (bots)
            if (runner)
            {
                GameObject.Destroy(runner);
            }
        }

        // UpdateErrorMessage update the error message on the UI
        private void UpdateErrorMessage(string shutdownErrorMessage)
        {
            Debug.LogError($"UpdateErrorMessage : {shutdownErrorMessage} ");
            if (desktopErrorMessageTMP) desktopErrorMessageTMP.text = shutdownErrorMessage;
            if (hardwareRigErrorMessageTMP) hardwareRigErrorMessageTMP.text = shutdownErrorMessage;
        }

        // DisplayErrorMessage is in charge to hide all scene objects and display the error message
        private IEnumerator CleanUpScene()
        {
            GameObject errorGO = null;
            ICameraFader fader = null;


            if (_rigInfo.localHardwareRigKind == RigInfo.RigKind.Desktop)
            {
                errorGO = desktopErrorMessageGO;
            }
            if (_rigInfo.localHardwareRigKind == RigInfo.RigKind.VR)
            {
                errorGO = hardwareRigErrorMessageGO;
            }
            if(_rigInfo.localHardwareRig.Headset is IFadeable fadeable && fadeable.Fader != null)
            {
                fader = fadeable.Fader;
            }

            yield return Fadeout(fader);
            ConfigureSceneForOfflineMode();
            yield return DisplayErrorMessage(fader, errorGO);

        }

        private IEnumerator Fadeout(ICameraFader fader)
        {
            // display black screen
            if (fader != null) yield return fader.FadeIn();
            yield return new WaitForSeconds(1);
        }

        void ConfigureSceneForOfflineMode()
        {
            // Hide all scene
            HideScene();
            // Destroy spawned objects
            DestroyNetworkedObjects();
        }

        private IEnumerator DisplayErrorMessage(ICameraFader fader, GameObject errorMessage)
        {
            // Display error message UI
            if (errorMessage) errorMessage.SetActive(true);
            // remove black screen
            if (fader != null) yield return fader.FadeOut();
        }

        // HideScene is in charge to hide the scene 
        private void HideScene()
        {
            if (staticLevel) staticLevel.SetActive(false);
            if (interactableObjects) interactableObjects.SetActive(false);
            RenderSettings.skybox = null;
        }


        // QuitApplication is called when the user push the UI Exit button 
        public void QuitApplication()
        {
            Debug.LogError("User exit the application");

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#endif
            Application.Quit();
        }

        public override void OnApplicationQuitRequest()
        {
            isQuitting = true;
            Destroy(this);
        }

        #region INetworkRunnerCallbacks
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Shutdown(shutdownReason);
        }
        public void OnDisconnectedFromServer(NetworkRunner runner)
        {
            DisconnectedFromServer();
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }

        public void OnInput(NetworkRunner runner, NetworkInput input) { }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

        public void OnConnectedToServer(NetworkRunner runner) { }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }

        public void OnSceneLoadDone(NetworkRunner runner) { }

        public void OnSceneLoadStart(NetworkRunner runner) { }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
#if FUSION_2_1_OR_NEWER
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
#endif
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        #endregion

        #region IHandRepresentationHandler
        public void DisplayHardwareHands()
        {
            if (RigInfo && RigInfo.localHardwareRig != null)
            {
                foreach (var rigPart in RigInfo.localHardwareRig.RigParts)
                {
                    foreach(var representationManager in rigPart.gameObject.GetComponentsInChildren<IHandRepresentationHandler>())
                    {
                        representationManager.RestoreHandInitialMaterial();
                    }
                }
            }
        }
        #endregion
    }
}