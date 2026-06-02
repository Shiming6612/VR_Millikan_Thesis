using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Fusion.Addons.HapticAndAudioFeedback
{
    /**
     * 
     * Can be used to be notified of session events (Fusion connexion, Voice connection, interactions, ...)
     * Should be stored under a NetworkRunner to be discoverable
     * 
     * Similar to NetworkEvents, but manage by default a few sounds with the  SoundManager 
     * 
     **/
    public class SessionEventsManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        public NetworkRunner runner;
        public SoundManager soundManager;
        [Header("Callbacks")]
        public UnityEvent onWillConnectEvent;
        public UnityEvent onWillSpawnLocalUserEvent;
        public UnityEvent onLocalUserSpawnedEvent;
        public UnityEvent onConnectedToServer;
        public UnityEvent onConnectFailed;
        public UnityEvent onDisconnectedFromServer;
        public UnityEvent<ShutdownReason> onShutdown = new UnityEvent<ShutdownReason>();
        public ReliableDataEvent onReliableData;

        [Serializable]
        public class ReliableDataEvent : UnityEvent<PlayerRef, byte[]> { }

        public static SessionEventsManager FindSessionEventsManager(NetworkRunner runner)
        {
            return runner.GetComponentInChildren<SessionEventsManager>();
        }

        protected virtual void Awake()
        {
            // Find the associated runner, if not defined
            if (runner == null) runner = GetComponentInParent<NetworkRunner>();
            if (runner == null)
            {
                Debug.LogError("Should be stored under a NetworkRunner to be discoverable");
                return;
            }
        }

        private void Start()
        {
            runner.AddCallbacks(this);

            // Find the SoundManager, if not defined
            if (soundManager == null) soundManager = SoundManager.FindInstance(runner);

        }

        #region INetworkRunnerCallbacks
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log("OnPlayerJoined");
            if (soundManager)
            {
                soundManager.PlayOneShot("OnPlayerJoined", 2f);
            }

            if (player == runner.LocalPlayer)
            {
                if (onWillSpawnLocalUserEvent != null) onWillSpawnLocalUserEvent.Invoke();
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            if (soundManager) soundManager.PlayOneShot("OnPlayerLeft");
        }

        public void OnInput(NetworkRunner runner, NetworkInput input) { }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Debug.LogError($"Shutdown : { shutdownReason} ");
            soundManager.PlayOneShot("OnShutdown");
            if (onShutdown != null) onShutdown.Invoke(shutdownReason);
        }

        public void OnConnectedToServer(NetworkRunner runner)
        {
            if (onConnectedToServer != null) onConnectedToServer.Invoke();
            if (soundManager) soundManager.PlayOneShot("OnConnectedToServer");
        }

        public void OnDisconnectedFromServer(NetworkRunner runner)
        {
            if (onDisconnectedFromServer != null) onDisconnectedFromServer.Invoke();
            Debug.LogError($"Disconnected From Server: {runner.SessionInfo} ");
            if (soundManager) soundManager.PlayOneShot("OnDisconnectedFromServer");
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            if (onConnectFailed != null) onConnectFailed.Invoke();
            Debug.LogError($"Connect Failed : { reason} ");
            if (soundManager) soundManager.PlayOneShot("OnConnectFailed");
        }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> receivedData)
        {
            if (onReliableData != null)
            {
                onReliableData.Invoke(player, receivedData.Array);
            }
        }

        public void OnSceneLoadDone(NetworkRunner runner) { }

        public void OnSceneLoadStart(NetworkRunner runner) { }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
      
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> receivedData) {
            if (onReliableData != null)
            {
                onReliableData.Invoke(player, receivedData.Array);
            }
        }

#if FUSION_2_1_OR_NEWER
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> receivedData) { 
            if (onReliableData != null)
            {
                onReliableData.Invoke(player, receivedData.ToArray());
            }
        }

#endif
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

        #endregion
    }

}
