using Fusion;
using Fusion.Sockets;
using Fusion.XR.Shared.Locomotion;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Fusion.XR.Shared.Locomotion
{
    /**
 * 
 * Enable the fade of the XR camera during Fusion connection 
 * 
 **/
#if FUSION_WEAVER
    public class ConnectionFadeManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        public NetworkRunner runner;

        [Header("Fusion Callbacks")]
        public UnityEvent onFusionConnectionJoined;

        public bool autoRegisterFadeOutOnFusionConnection = true;

        protected virtual void Awake()
        {
            if (runner == null) runner = GetComponentInParent<NetworkRunner>();
            if (runner == null)
            {
                Debug.LogError("Should be stored under a NetworkRunner to be discoverable");
                return;
            }


            if (autoRegisterFadeOutOnFusionConnection)
            {
                foreach (var fader in FindObjectsByType<Fader>(FindObjectsSortMode.None))
                {
                    fader.startFadeLevel = 1;
                }
            }
        }

        private void Start()
        {
            runner.AddCallbacks(this);
        }

        protected virtual void OnFusionConnectionJoined()
        {
            FadeOutOnFusionConnection();
            if (onFusionConnectionJoined != null) onFusionConnectionJoined.Invoke();
        }

        void FadeOutOnFusionConnection()
        {
            if (!autoRegisterFadeOutOnFusionConnection) return;
            foreach (var fader in FindObjectsByType<Fader>(FindObjectsSortMode.None))
            {
                fader.AnimateFadeOut(1);
            }
        }

        #region INetworkRunnerCallbacks callbacks
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (player == runner.LocalPlayer)
            {
                OnFusionConnectionJoined();
            }
        }
        #endregion

        #region unusued INetworkRunnerCallbacks callbacks
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)    {    }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)    {    }

    
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)    {    }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)    {    }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)    {    }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)    {    }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)    {    }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)    {    }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)    {    }
#if FUSION_2_1_OR_NEWER
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
#endif
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)    {    }

        public void OnInput(NetworkRunner runner, NetworkInput input)    {    }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)    {    }

        public void OnConnectedToServer(NetworkRunner runner)    {    }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)    {    }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)    {    }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)    {    }

        public void OnSceneLoadDone(NetworkRunner runner)    {    }

        public void OnSceneLoadStart(NetworkRunner runner)    {    }
        #endregion


    }
#else
    public class ConnectionFadeManager : MonoBehaviour { }
#endif

}
