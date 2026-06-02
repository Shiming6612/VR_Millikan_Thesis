using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;

namespace Fusion.Addons.DataSyncHelpers
{
    /*
     * Use the Fusion data streaming API (https://doc.photonengine.com/fusion/current/manual/data-transfer/data-streaming)
     *  to send data to user, and make sure that late joiners will receive them, by asking other users to share the data they have.
     *  
     * State authority can send data by using Send(byte[] data)
     * See Subclassables callbacks for subclassing options
     **/
    public class StreamSynchedBehaviour : NetworkBehaviour, INetworkRunnerCallbacks
    {
        [Networked]
        public int TotalDataLength { get; set; }
        public bool allowAnyClientEmission = false;

        List<PlayerRef> playersHavingReceivedRecoveryRequests = new List<PlayerRef>();
        List<PlayerRef> playersHavingConfirmedRecoveryRequests = new List<PlayerRef>();

        [System.Serializable]
        public struct Chunk
        {
            public PlayerRef source;
            public float time;
            public byte[] data;
        }
        public List<Chunk> cachedDataChunks = new List<Chunk>();
        protected int totalLocalDataLength = 0;

        const int TIME_PRECISION = 10_000;
        const float MAX_RESPONSE_TIME_TO_LATE_JOINERS = 5;
        const float MAX_DELAY_BEFORE_CONSIDERING_AS_LATE_JOINERS = 1;
        const float MAX_ABSOLUTE_DELAY_BEFORE_CONSIDERING_AS_LATE_JOINERS = 15;

        float initialDataMissingDetectionStart = 0;
        float dataMissingDetectionStart = 0;
        float waitingForDataStart = 0;

        NetworkRunner runner;

        public enum Status
        {
            Normal,
            DataMissingAtSpawnDetected,
            LateJoinersWaitingForData,
            ReceivingData
        }

        public Status status = Status.Normal;
        public bool dataMissing = false;

        bool IsStateAuthorityStillConnected
        {
            get
            {
                foreach (var player in Runner.ActivePlayers)
                {
                    if (player == Object.StateAuthority)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        PlayerRef CurrentRecoveryProvider
        {
            get
            {
                if (playersHavingReceivedRecoveryRequests.Count == 0) return PlayerRef.None;
                return playersHavingReceivedRecoveryRequests[playersHavingReceivedRecoveryRequests.Count - 1];
            }
        }

        #region Log
        [System.Flags]
        public enum LogLevel
        {
            None = 0,
            Issue = 1,
            Relay = 4,
            Progress = 8,
            Reception = 16,
            LateJoinRecovery = 32,
            Details = 256,
            All = ~0,
        }
        public LogLevel logLevel = LogLevel.Issue;

        protected void Log(string log, LogLevel level = LogLevel.Details)
        {
            if ((level & logLevel) == 0)
            {
                return;
            }
            Debug.Log(log);
        }
        #endregion

        #region NetworkBehaviour
        public override void Spawned()
        {
            base.Spawned();
            runner = Runner;
            Runner.AddCallbacks(this);

            if (TotalDataLength != 0)
            {
                StartDataMissingAtSpawnDetected();
            }
        }
        #endregion

        #region Monobehaviour
        protected virtual void OnDestroy()
        {
            if (runner) runner.RemoveCallbacks(this);
        }

        private void Update()
        {
            CheckMissingData();
        }
        #endregion

        #region Subclassables callbacks
        // Provides the 0-1 download progress of a currently received chunk of data
        protected virtual void OnDataProgress(float progress) { }
        // Called on complete reception of a chunk of data
        protected virtual void OnNewBytes(byte[] newData) { }
        // Called on complete reception of a chunk of data (remote source only)
        protected virtual void OnNewRemoteBytes(byte[] newData, PlayerRef source, float time) { }
        // Called to know the client is a late joiner, waiting for data
        protected virtual void OnLateJoinersWaitingForData() { }
        // Called if missing data for a late joiner are unavailable anywhere
        protected virtual void OnMissingData() { }
        #endregion

        #region Late joiner logic to recover missed data
        bool CanProvideData()
        {
            // No missing data, and not receiving some currently: we can share what we have to other users
            bool canProvideData = status == Status.Normal && dataMissing == false;
            return canProvideData;
        }

        void StartDataMissingAtSpawnDetected()
        {
            Log($"[StartDataMissingAtSpawnDetected] TotalDataLength > 0 during spawn: some data are missing, waiting a bit to determine if we are a late joiner (we would then RequestMissingData) or if data are coming ({Time.realtimeSinceStartup})", level: LogLevel.Details);
            initialDataMissingDetectionStart = Time.time;
            dataMissingDetectionStart = Time.time;
            status = Status.DataMissingAtSpawnDetected;
        }

        // Late joiner: might have missed streamed data before connecting
        void StartRequestingMissingData()
        {
            status = Status.LateJoinersWaitingForData;
            Log($"[StartRequestingMissingData] Late joiner, RequestMissingData ({Time.realtimeSinceStartup})", LogLevel.Details);
            OnLateJoinersWaitingForData();
            RequestMissingData();
        }

        void CheckMissingData()
        {
            if (status == Status.DataMissingAtSpawnDetected)
            {
                if ((Time.time - dataMissingDetectionStart) > MAX_DELAY_BEFORE_CONSIDERING_AS_LATE_JOINERS)
                {
                    // The object spawned with some data count in TotalDataLength, but no data was received: we are probably a late joiners, and should request to receive the data
                    StartRequestingMissingData();
                }
                else if ((Time.time - initialDataMissingDetectionStart) > MAX_ABSOLUTE_DELAY_BEFORE_CONSIDERING_AS_LATE_JOINERS)
                {
                    // The object spawned with some data count in TotalDataLength, but no data was received: we are probably a late joiners, and should request to receive the data
                    // The state authority was sending data , so we waited a bit more (dataMissingDetectionStart was offseted in OnReliableDataProgress),
                    // but now, we won't accept any delay postponement before considering we are a late joiner (as the state authority might be sending data all the time)
                    Log($"[DataMissingAtSpawnDetected] Consider as late joiner, no more delyaing acceoted (even if state authority was sending data)", LogLevel.Details);
                    StartRequestingMissingData();
                }
            }
            if (status == Status.LateJoinersWaitingForData)
            {
                if (waitingForDataStart != 0 && (Time.time - waitingForDataStart) < MAX_RESPONSE_TIME_TO_LATE_JOINERS)
                {
                    // Still waiting for a pending request
                    return;
                }

                Log($"[LateJoinersWaitingForData] Late joiner, first provider did not answered in time, asking to next provider ({Time.realtimeSinceStartup})", LogLevel.Details);
                RequestMissingData();
            }
        }

        // Subclass can override it to have their own logic of choosing who will forward data in priority (for instance, adding randomness to avoid hitting the same fallback player all the time)
        protected virtual IEnumerable<PlayerRef> PlayerList()
        {
            return Runner.ActivePlayers;
        }

        protected virtual void RequestMissingData()
        {
            if (playersHavingReceivedRecoveryRequests.Contains(Object.StateAuthority) == false && IsStateAuthorityStillConnected)
            {
                // We first ask to state auth if still connected
                RequestMissingData(Object.StateAuthority);
            }
            else
            {
                // Otherwise, we ask to the first to a player we've not yet asked to (not the state auth, and not us)
                foreach (var player in PlayerList())
                {
                    if (playersHavingReceivedRecoveryRequests.Contains(player) == false && player != Runner.LocalPlayer)
                    {
                        RequestMissingData(player);
                        return;
                    }
                }
                // No player left to ask to
                Log($"No player left to ask to for late join data", LogLevel.LateJoinRecovery);
                dataMissing = true;
                status = Status.Normal;
                OnMissingData();
            }
        }

        void RequestMissingData(PlayerRef player)
        {
            Log($"RequestMissingData to {player}  ({Time.realtimeSinceStartup})", LogLevel.LateJoinRecovery);
            playersHavingReceivedRecoveryRequests.Add(player);
            waitingForDataStart = Time.time;
            RpcDataRequest(Runner.LocalPlayer, target: player);
        }

        [Rpc]
        public void RpcDataRequest(PlayerRef requester, PlayerRef target)
        {
            if (target != Runner.LocalPlayer) return;
            if (CanProvideData())
            {
                Log("Send data to late joiners", LogLevel.LateJoinRecovery);
                SendCachedDataToPlayer(requester);
                // If the transfer is long (due to initialization syncs, ...), we warn the receiver to wait for our answer a bit longer, we do have the data
                RpcConfirmDataAvailability(Runner.LocalPlayer, requester);
            }
        }

        [Rpc]
        public void RpcConfirmDataAvailability(PlayerRef provider, PlayerRef target)
        {
            if (target != Runner.LocalPlayer) return;
            Log("Current recovery provider confirmed having the data we are looking for", LogLevel.LateJoinRecovery);
            playersHavingConfirmedRecoveryRequests.Add(provider);
        }
        #endregion

        #region Reliable data transmission
        ReliableKey GenerateReliableKey()
        {
            ReliableKey key = ReliableKey.FromInts((int)TargetId(), Runner.LocalPlayer.RawEncoded, (int)Time.time, (int)(TIME_PRECISION * (Time.time - (int)Time.time)));
            return key;
        }

        (int, PlayerRef, float) ParseKey(ReliableKey key)
        {
            key.GetInts(out var key0, out var key1, out var key2, out var key3);
            var objectId = key0;
            var source = PlayerRef.FromEncoded(key1);
            var time = (float)key2 + (float)key3 / (float)TIME_PRECISION;
            return (objectId, source, time);
        }

        public virtual void Send(byte[] data)
        {
            if (Object == null || Runner == null)
            {
                Debug.LogError("Not (yet?) connected");
                return;
            }
            bool hasRelevantAuthority = (Runner.Topology == Topologies.Shared && Object.HasStateAuthority == false) || (Runner.Topology == Topologies.ClientServer && Object.HasInputAuthority == false);
            if (allowAnyClientEmission == false && hasRelevantAuthority)
            {
                Debug.LogError("Data cannot be asssured to be in proper order if several players can push data to it. Rejecting send request");
                return;
            }

            ReliableKey key = GenerateReliableKey();
            Log($"[{Time.time}] Sending data (local player:{Runner.LocalPlayer}): {ByteArrayTools.PreviewString(data)}");

            // Directly stores the data localy
            OnDataChunkReceived(data, Runner.LocalPlayer, Time.time);

            if (Runner.Topology == Topologies.Shared || Runner.IsServer)
            {
                // Directly send to clients
                foreach (var player in Runner.ActivePlayers)
                {
                    if (player == Runner.LocalPlayer) continue;
                    Log($" => Sending to {player}{((player == Runner.LocalPlayer) ? " (themselves)" : "")}", LogLevel.Details);
                    Runner.SendReliableDataToPlayer(player, key, data);
                }
            }
            else
            {
                Runner.SendReliableDataToServer(key, data); ;
            }
        }

        void SendCachedDataToPlayer(PlayerRef player)
        {
            if (totalLocalDataLength == 0) return;

            var data = new byte[totalLocalDataLength];
            int cursor = 0;
            foreach (var chunk in cachedDataChunks)
            {
                System.Buffer.BlockCopy(chunk.data, 0, data, cursor, chunk.data.Length);
                cursor += chunk.data.Length;
            }
            ReliableKey key = GenerateReliableKey();
            Log("SendCachedDataToPlayer "+player, level: LogLevel.Details);
            Runner.SendReliableDataToPlayer(player, key, data);
        }

        // Add local data, without transfering it
        public void AddLocalData(byte[] data)
        {
            AddLocalData(data, Runner.LocalPlayer, Time.time);
        }

        // Add local data, without transfering it
        public void AddLocalData(byte[] data, PlayerRef source, float time)
        {
            cachedDataChunks.Add(new Chunk { source = source, time = time, data = data });
            totalLocalDataLength += data.Length;
            if (Object.HasStateAuthority)
            {
                TotalDataLength = totalLocalDataLength;
            }
        }

        public void ClearData(bool allowNonStateAuthorityClear = false)
        {
            if(Object && Object.HasStateAuthority == false && allowNonStateAuthorityClear == false)
            {
                throw new Exception("Not allowed to clear the data: not the state auth");
            }
            // We cannot rely on detecting a TotalDataLength set to 0 by the state auth, has it could have alerady filled it again when we receive the value
            RpcClearData();
        }

        protected virtual void OnDataCleared()
        {
        }

        [Rpc]
        public void RpcClearData()
        {
            LocalClearData();
        }

        public void LocalClearData()
        {
            cachedDataChunks.Clear();
            playersHavingReceivedRecoveryRequests.Clear();
            playersHavingConfirmedRecoveryRequests.Clear();
            totalLocalDataLength = 0;
            if (Object.HasStateAuthority)
            {
                TotalDataLength = 0;
            }
            OnDataCleared();
        }

        public void InsertLocalDataAtStart(byte[] data, PlayerRef source, float time)
        {
            cachedDataChunks.Insert(0, new Chunk { source = source, time = time, data = data });
            totalLocalDataLength += data.Length;
        }

        public virtual void OnDataChunkReceived(byte[] data, PlayerRef source, float time)
        {
            Log($"Added data chunk from {source} at {time}: {ByteArrayTools.PreviewString(data)}", LogLevel.Reception);
            AddLocalData(data, source, time);
            OnNewBytes(data);
            if(source != Runner.LocalPlayer)
            {
                OnNewRemoteBytes(data, source, time);
            }
        }

        #endregion

        #region INetworkRunnerCallbacks callbacks
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {
            if (Object == null)
            {
                Log($"Object destroyed while receiving OnReliableDataProgress", LogLevel.Issue);
                return;
            }

            (var targetId, var source, var time) = ParseKey(key);
            if (IsReliableDataTarget(targetId))
            {
                // This data chunk is for this object
                Log($"([{targetId}, {source}, {time}] OnReliableDataProgress progress:{progress} (local player:{Runner.LocalPlayer}) )", LogLevel.Progress);
                UpdateStatusForDataProgress();
                OnDataProgress(progress);
            }
            else
            {
                if (Runner.Topology == Topologies.Shared && source == Object.StateAuthority) {
                    Log("State authority is currently buzy sending data, so we'll wait a bit more before considering we are a late joiner", LogLevel.Details);
                    dataMissingDetectionStart = Time.time;
                }


                if (Runner.Topology == Topologies.Shared && source == CurrentRecoveryProvider)
                {
                    if (status == Status.LateJoinersWaitingForData && playersHavingConfirmedRecoveryRequests.Contains(source))
                    {
                        Log("Current recovery provider is still connected, confirmed having the data, but is busy sending data for other objects. We make sure to wait a bit longer", LogLevel.LateJoinRecovery);
                        waitingForDataStart = Time.time;
                    }
                }

                Log($"([{targetId}, {source}, {time}] Ignoring OnReliableDataProgress: {(int)Object.Id.Raw}/{TargetId()})", LogLevel.Details);
            }
        }

#if FUSION_2_1_OR_NEWER
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> receivedData) { 
            OnDataReceived(key, receivedData.ToArray());      
        }
#endif
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef targetPlayer, ReliableKey key, ArraySegment<byte> receivedData)
        {
            OnDataReceived(key, receivedData.Array);
        }
        #endregion

        public void OnDataReceived(ReliableKey key, byte[] receivedData)
        {
            if (Object == null)
            {
                Log($"Object destroyed while receiving OnReliableDataReceived", LogLevel.Issue);
                return;
            }
            (var targetId, var source, var time) = ParseKey(key);
            if (IsReliableDataTarget(targetId))
            {
                if (Runner.IsServer)
                {
                    // Received a message as the server: we relay it
                    Log($"[{targetId}, {source}, {time}] Server received OnReliableDataReceived, relaying (local player:{Runner.LocalPlayer}): {ByteArrayTools.PreviewString(receivedData)}", LogLevel.Relay);
                    foreach (var player in Runner.ActivePlayers)
                    {
                        // We do not send the data back to the sending player
                        if (player == source) continue;

                        Log($" => to {player}{((player == Runner.LocalPlayer) ? " (themselves)" : "")}", LogLevel.Details);
                        if (player == Runner.LocalPlayer)
                        {
                            // For the host, store directly the data chunk locally
                            OnDataChunkReceived(receivedData, source, time);
                        }
                        else
                        {
                            Log("[Server] OnReliableDataReceived => forwarding to player " + player, level: LogLevel.Details);
                            Runner.SendReliableDataToPlayer(player, key, receivedData);
                        }
                    }
                }
                else
                {
                    Log($"[{targetId}, {source}, {time}] OnReliableDataReceived (local player:{Runner.LocalPlayer}): {ByteArrayTools.PreviewString(receivedData)}");
                    UpdateStatusForDataCompleted();
                    OnDataChunkReceived(receivedData, source, time);
                }
            }
            else
            {
                Log($"([{targetId}, {source}, {time}] Ignoring OnReliableDataReceived {(int)Object.Id.Raw} / {TargetId()})", LogLevel.Details);
            }
        }


        #region Status
        void UpdateStatusForDataProgress()
        {
            if (status == Status.LateJoinersWaitingForData)
            {
                Log($"[UpdateStatusForDataProgress] We are a late joiner (status was LateJoinersWaitingForData) and the data is being transmitted after our request ({Time.realtimeSinceStartup})", level: LogLevel.Details);
            }
            if (status == Status.DataMissingAtSpawnDetected)
            {
                Log($"[UpdateStatusForDataProgress] We are not a late joiner (status was DataMissingAtSpawnDetected) and we just received the total data byte count directly at spawn but then the actual streaming started ({Time.realtimeSinceStartup})", level: LogLevel.Details);
            }
            status = Status.ReceivingData;
        }

        void UpdateStatusForDataCompleted()
        {
            status = Status.Normal;
        }
        #endregion

        #region Data routing

        protected virtual bool IsReliableDataTarget(int targetId)
        {
            return targetId == TargetId();
        }

        protected virtual int TargetId()
        {
            if (StreamingAPIConfiguration.HandleMultipleStreamingAPIComponentCollisions)
            {
                return Id.GetHashCode();
            }
            else
            {
                return (int)Object.Id.Raw;
            }
        }
        #endregion

        #region Unused INetworkRunnerCallbacks callbacks
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        #endregion

        #region Data parsing
        protected T[] SplitCompleteData<T>() where T : unmanaged, IByteArraySerializable
        {
            var data = new byte[totalLocalDataLength];
            int cursor = 0;
            foreach (var chunk in cachedDataChunks)
            {
                System.Buffer.BlockCopy(chunk.data, 0, data, cursor, chunk.data.Length);
                cursor += chunk.data.Length;
            }

            ByteArrayTools.Split<T>(data, out var newPaddingStartBytes, out var entries);

            return entries;
        }
        #endregion
    }

}
