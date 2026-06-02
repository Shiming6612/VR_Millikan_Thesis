using Fusion;
using Fusion.Addons.AnchorsAddon;
using Fusion.Addons.AnchorsAddon.Colocalization;
using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Utils;
#if MRUK_AVAILABLE
using Meta.XR.MRUtilityKit;
#endif
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Interface for elements in a IRLRoom which move could trigger a room move (member or move requesters)
/// </summary>
public interface IRLRoomMovingReferenceElement
{
    public Vector3 PositionBeforeMoveToPropagate { get; }
    public Quaternion RotationBeforeMoveToPropagate { get; }
    public Vector3 PositionAfterMoveToPropagate { get; }
    public Quaternion RotationAfterMoveToPropagate { get; }
}

/**
 * Track IRL room members and anchors
 * 
 * Trigger colocation and room changes upon world anchor detection coming from a WorldAnchorTracking
 */
public class IRLRoomManager : MonoBehaviour, IRLAnchorTracking.IIRLAnchorTrackingListener
{
    public interface IEventLogHandler
    {
        public void LogEvent(string log);
    }

    public interface IIRLRoomManagerListener
    {
        public void OnRoomCreate(string roomId);
        public void OnRoomDelete(string roomId);
    }

    [System.Serializable]
    public struct TagRoomMapping
    {
        public string anchorId;
        public string roomId;
    }

    [System.Serializable]
    public class IRLRoom
    {
        public string roomId;
        public List<NetworkIRLRoomMember> members = new List<NetworkIRLRoomMember>();
        public List<NetworkIRLRoomAnchor> anchors = new List<NetworkIRLRoomAnchor>();
        public NetworkIRLRoomMoveRequester moveRequester = null;
        // A counter to track move requests (move requests equal or below this number have already been applied by this local player and can be ignored)
        public int lastAppliedMoveCounter = 0;
    }

    [System.Serializable]
    public class DetectedAnchorInfo
    {
        public IRLAnchorInfo info = null;
        public bool detectedThisFrame = false;
        public float positionError = 0;
        public float angleError = 0;
    }

    public List<IRLRoom> knowRooms = new List<IRLRoom>();
    public Dictionary<string, IRLRoom> knowRoomByRoomIds = new Dictionary<string, IRLRoom>();
    public Dictionary<string, NetworkIRLRoomAnchor> knowNetworkAnchorByAnchorIds = new Dictionary<string, NetworkIRLRoomAnchor>();
    public List<string> knowTagIds = new List<string>();
    public List<NetworkIRLRoomMember> knowMembers = new List<NetworkIRLRoomMember>();
    public List<NetworkIRLRoomMoveRequester> knowMoveRequesters = new List<NetworkIRLRoomMoveRequester>();

    [Tooltip("If not empty, anchorId containing this string will be considered as predefined rooms  - the room name being after this separator")]
    public string predefinedRoomSuffixSeparator = "-Room-";
    [Tooltip("All anchors with the id in this list will have predefined rooms, that can not be changed")]
    public List<TagRoomMapping> predefinedTagRoomMappings = new List<TagRoomMapping>();

    [Tooltip("If not empty, anchor which do not contain this string will be ignored")]
    public string requiredAnchorContentToUseForColocalization = "";

    public NetworkIRLRoomAnchor networkIRLRoomTagPrefab;

    public IRLAnchorTracking worldAnchorTracking;

    public NetworkIRLRoomMember localNetworkIRLRoomMember;

    bool worldAnchorTrackingRegistered = false;

    public IEventLogHandler eventLogHandler;

    [Header("Colocalization option - repositioning effects")]
    [Tooltip("If true, the rig vertical position might be changed, fixing potential incorect boundary floor setup at the same time that the colocalization occurs. Setting it to false will lead to local real life users and their avatars not to be aligned")]
    public bool autofixGroundPosition = true;
    [Tooltip("If false, the rig up direction might be changed. It is highly recommended to set it to true")]
    public bool keepUpDirection = true;

    [Header("Colocalization option - fix passthrough position changes")]
    public bool fixPositionError = false;
    public float minimumPositionErrorToRestartColocalization = 1f;
    public float minimumAngleErrorToRestartColocalization = 30f;

    public enum NetworkIRLRoomAssociatedPartDisplayMode
    { 
        Always,                     
        RemoteRoomOnly,
        MainPlayerInRemoteRoomOnly, // Main player is determined by the lowest Fusion playerId
        Never
    }

    [Header("Room associated part display")]
    [Tooltip("For NetworkIRLRoomAssociatedPart having their adaptRendersToRoomManagerMode set to true, their renderers will follow the mode given here")]
    public NetworkIRLRoomAssociatedPartDisplayMode roomAssociatedPartDisplayMode = IRLRoomManager.NetworkIRLRoomAssociatedPartDisplayMode.MainPlayerInRemoteRoomOnly;

    public enum AnchorFollowLogic
    {
        RoomMerge, // [Recommended] As soon as a member joined a room, merges the rooms 
        NoFollow, // Warning, this will lead to user switching between rooms quite often, with no ability to join room parts
    }

    [Header("Anchor logic")]
    public AnchorFollowLogic anchorFollowLogic = AnchorFollowLogic.RoomMerge;

    [Header("Debug")]
    public bool debugLog = false;
    public List<DetectedAnchorInfo> visibleLongStableAnchorsInfo = new List<DetectedAnchorInfo>();

    [HideInInspector]
    public int detectedAnchors = 0;

    bool colocalizationTriggeredThisFrame = false;

    public List<IIRLRoomManagerListener> listeners = new List<IIRLRoomManagerListener>();

    public bool IsPredefinedRoomAnchorId(string anchorId, out string roomId)
    {
        roomId = null;
        foreach (var mapping in predefinedTagRoomMappings)
        {
            if (mapping.anchorId == anchorId)
            {
                roomId = mapping.roomId;
                return true;
            }
        }
        if (string.IsNullOrEmpty(predefinedRoomSuffixSeparator) == false && anchorId.Contains(predefinedRoomSuffixSeparator))
        {
            var parts = anchorId.Split(new string[] { predefinedRoomSuffixSeparator }, System.StringSplitOptions.None);
            if (parts.Length > 1)
            {
                roomId = anchorId.Substring(parts[0].Length + predefinedRoomSuffixSeparator.Length);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Return the main member of a room (the member with the lowest player id)
    /// Useful to have a unique reference member in a given room, common for all players
    /// </summary>
    /// <returns></returns>
    public NetworkIRLRoomMember RoomMainMember(string roomId)
    {
        NetworkIRLRoomMember mainMember = null;
        if (knowRoomByRoomIds.ContainsKey(roomId))
        {
            int lowestId = int.MaxValue;
            foreach(var m in knowRoomByRoomIds[roomId].members)
            {
                if(m.Object.StateAuthority.RawEncoded < lowestId)
                {
                    mainMember = m;
                    lowestId = m.Object.StateAuthority.RawEncoded;
                }
            }
        }
        return mainMember;
    }

    void FindWorldAnchorTracking()
    {
        if (worldAnchorTracking == null)
        {
            worldAnchorTracking = FindAnyObjectByType<IRLAnchorTracking>();
        }
        if (worldAnchorTracking && worldAnchorTrackingRegistered == false)
        {
            worldAnchorTrackingRegistered = true;
            worldAnchorTracking.listeners.Add(this);
        }
    }

    private void Awake()
    {
        FindWorldAnchorTracking();
    }

    private void Start()
    {
#if MRUK_AVAILABLE
        if (MRUK.Instance)
        {
            // We will move users, so we need to prevent MRUK from locking the users world position
            MRUK.Instance.EnableWorldLock = false;
        }
#endif
    }

    #region IWorldAnchorTrackingListener
    public void OnIRLAnchorDetectedThisFrame(IRLAnchorTracking worldAnchorTracking, IRLAnchorInfo anchor)
    {
        if (string.IsNullOrEmpty(requiredAnchorContentToUseForColocalization) == false && anchor.anchorId.Contains(requiredAnchorContentToUseForColocalization) == false)
        {
            return;
        }
        detectedAnchors++;
        if (localNetworkIRLRoomMember == null) return;

        // If a colocalization occured this frame, the anchors detection position might not be relevant anymore: skipping next tags
        if (colocalizationTriggeredThisFrame) return;

        // We only manipulate stable anchor (stable for long enough)
        if (anchor.hasLongStability == false) return;

        if (knowTagIds.Contains(anchor.anchorId) == false)
        {
            SpawnNetworkIRLRoomAnchor(anchor);
        }
        else
        {
            if (knowNetworkAnchorByAnchorIds.ContainsKey(anchor.anchorId) == false)
            {
                Debug.LogError("[IRLRoomManager] Incoherent state");
                return;
            }
            var networkAnchor = knowNetworkAnchorByAnchorIds[anchor.anchorId];
            OnExistingNetworkAnchorDetected(networkAnchor, anchor);
        }
    }

    public void OnIRLAnchorSpawn(IRLAnchorTracking worldAnchorTracking, string anchorId) { }
    public void OnDetectionStarted(IRLAnchorTracking worldAnchorTracking)
    {
        detectedAnchors = 0;
        colocalizationTriggeredThisFrame = false;
        foreach (var d in visibleLongStableAnchorsInfo)
        {
            d.detectedThisFrame = false;
        }
    }

    public void OnDetectionFinished(IRLAnchorTracking worldAnchorTracking)
    {
        for (int i = visibleLongStableAnchorsInfo.Count - 1; i >= 0; i--)
        {
            if (visibleLongStableAnchorsInfo[i].detectedThisFrame == false)
            {
                visibleLongStableAnchorsInfo.RemoveAt(i);
            }
        }
    }
    #endregion

    #region Detection logic
    public void SpawnNetworkIRLRoomAnchor(IRLAnchorInfo anchor)
    {

        var position = anchor.stablePose.position;
        var rotation = anchor.stablePose.rotation;
        var runner = NetworkRunner.GetRunnerForGameObject(gameObject);

        var anchorRoomId = localNetworkIRLRoomMember.RoomId.ToString();
        bool isChangingRoomForbidden = false;
        if (IsPredefinedRoomAnchorId(anchor.anchorId, out var anchorPredefinedRoomId) && anchorPredefinedRoomId != anchorRoomId)
        {
            // This is a predefined anchor. If the predefined room has no anchors yet, we can put this anchor in its predefined room
            // But if it is not empty, we cannot add it yet to this room (we don't know how the anchors are positionned relatively to each others)
            // In this case, we keep it in our room, until we actually see the other one
            bool canUsePredefinedroomId = true;
            if (knowRoomByRoomIds.ContainsKey(anchorPredefinedRoomId) && knowRoomByRoomIds[anchorPredefinedRoomId].anchors.Count != 0)
            {
                var logEvent = $"Unable to use predefined room id {anchorPredefinedRoomId} for anchor id {anchor.anchorId}: room not new. Using user default room {localNetworkIRLRoomMember.RoomId}";
                LogEvent(logEvent);
                canUsePredefinedroomId = false;
            }
            if (canUsePredefinedroomId)
            {
                var logEvent = $"Found predefined room id {anchorPredefinedRoomId} for anchor id {anchor.anchorId}. Moving user to this room";
                LogEvent(logEvent);
                anchorRoomId = anchorPredefinedRoomId;
                localNetworkIRLRoomMember.ChangeRoomId(anchorRoomId);
                isChangingRoomForbidden = true;
            }
        }

        var eventText = $"New anchor visible {anchor.anchorId}: spawning its network anchor (room {anchorRoomId})";
        LogEvent(eventText);


        var networkAnchor = runner.Spawn(networkIRLRoomTagPrefab, position, rotation, onBeforeSpawned: (r, o) => {
            var na = o.GetComponentInChildren<NetworkIRLRoomAnchor>();
            na.SetAnchorId(anchor.anchorId);
            na.ChangeRoomId(anchorRoomId);
        });
        networkAnchor.IsChangingRoomForbidden = isChangingRoomForbidden;
        if (localNetworkIRLRoomMember.RoomAnchorToFollow == null)
        {
            localNetworkIRLRoomMember.RoomAnchorToFollow = networkAnchor;
        }
    }

    public void OnExistingNetworkAnchorDetected(NetworkIRLRoomAnchor networkAnchor, IRLAnchorInfo worldAnchor)
    {
        foreach (var room in knowRooms)
        {
            if (room.anchors.Contains(networkAnchor) && room.roomId != networkAnchor.RoomId.ToString())
            {
                // Anchor roomID does not match what we know about it: it is changing its room, and we are waiting for an OnChange that will occur during the next Render: ignore it for now
                Debug.Log($"[Skip existing anchor detection] Anchor {networkAnchor.AnchorId} roomId {networkAnchor.RoomId} changed (was {room.roomId}) ...");
                return;
            }
        }

        float positionError = 0;
        float angleError = 0;
        if (networkAnchor.RoomId.ToString() != localNetworkIRLRoomMember.RoomId.ToString())
        {
            // Other room
            MatchNetworkAnchorPosition(networkAnchor, worldAnchor);
            colocalizationTriggeredThisFrame = true;
        }
        else
        {
            // TODO Check Anchor expected position, to see if a correction would be needed
            var rig = HardwareRigsRegistry.GetHardwareRig();
            var rigPose = DetermineRigPositionToMoveCurrentIRLPoseToTargetPose(worldAnchor.stablePose, networkAnchor.AnchorPose);
            positionError = Vector3.Distance(rigPose.position, rig.transform.position);
            angleError = Quaternion.Angle(rigPose.rotation, rig.transform.rotation);

            if (fixPositionError)
            {
                if (positionError > minimumPositionErrorToRestartColocalization || angleError > minimumAngleErrorToRestartColocalization)
                {
                    var eventText = $"[Colocation triggered] Large error on anchor {networkAnchor.RoomId} position ({(int)(positionError * 100)}cm, {(int)angleError}°): repositioning user ";
                    MoveRigPositionToMoveCurrentIRLPoseToTargetPose(worldAnchor.stablePose, networkAnchor.AnchorPose);

                    LogEvent(eventText);
                }
            }
        }

        // Log anchor pose error
        DetectedAnchorInfo visibleAnchorInfo = null;
        foreach (var v in visibleLongStableAnchorsInfo)
        {
            if (v.info.anchorId == worldAnchor.anchorId)
            {
                visibleAnchorInfo = v;
                break;
            }
        }
        if (visibleAnchorInfo == null)
        {
            visibleAnchorInfo = new DetectedAnchorInfo { info = worldAnchor };
            visibleLongStableAnchorsInfo.Add(visibleAnchorInfo);
        }
        visibleAnchorInfo.detectedThisFrame = true;
        visibleAnchorInfo.positionError = positionError;
        visibleAnchorInfo.angleError = angleError;
        if (positionError > 0.01 || angleError > 1)
        {
            // TODO Suggest way to fix moving anchors (add a callback, ...)
            // Debug.LogError($"Error: {(positionError * 100):0.0}cm {angleError:0} delta realtime/stabilized:{Vector3.Distance(worldAnchor.stablePose.position, worldAnchor.detectedIrlAnchorTag.transform.position)}");
        }
    }
    #endregion

    public void LogEvent(string eventText)
    {
        if (eventLogHandler != null)
        {
            eventLogHandler.LogEvent(eventText);
        }
        else
        {
            Debug.Log("[IRLRoomManager] " + eventText);
        }
    }

    public void ConsoleLog(string text)
    {
        if (debugLog) Debug.Log($"[IRLRoomManager] {text}");
    }

    #region Colocation
    void MatchNetworkAnchorPosition(NetworkIRLRoomAnchor networkAnchor, IRLAnchorInfo worldAnchor)
    {
        ConsoleLog($"MatchNetworkAnchorPosition {worldAnchor.anchorId} {networkAnchor.RoomId} ({localNetworkIRLRoomMember.Object.StateAuthority}/{localNetworkIRLRoomMember.RoomId})");
        var rig = HardwareRigsRegistry.GetHardwareRig();
        var currentRoomId = localNetworkIRLRoomMember.RoomId.ToString();

        var positionBeforeMergingRoom = rig.transform.position;
        var rotationBeforeMergingRoom = rig.transform.rotation;

        MoveRigPositionToMoveCurrentIRLPoseToTargetPose(worldAnchor.stablePose, networkAnchor.AnchorPose);

        var positionAfterMergingRoom = rig.transform.position;
        var rotationAfterMergingRoom = rig.transform.rotation;

        // Will trigger on change, that may read merge position info, hence the need to do it before changing room
        localNetworkIRLRoomMember.InitializingRoomMerge(networkAnchor.RoomId.ToString(), positionBeforeMergingRoom, rotationBeforeMergingRoom, positionAfterMergingRoom, rotationAfterMergingRoom);

        var eventText = $"[Colocation triggered] Moving (due to anchor {worldAnchor.anchorId}) local user to room {networkAnchor.RoomId} ({localNetworkIRLRoomMember.PositionBeforeMergingRoom} -> {localNetworkIRLRoomMember.PositionAfterMergingRoom} // {localNetworkIRLRoomMember.RotationBeforeMergingRoom.eulerAngles} -> {localNetworkIRLRoomMember.RotationAfterMergingRoom.eulerAngles})";
        LogEvent(eventText);

        localNetworkIRLRoomMember.RoomAnchorToFollow = networkAnchor;
    }

    public Pose DetermineRigPositionToMoveCurrentIRLPoseToTargetPose(Pose currentIRLPose, Pose targetPose)
    {
        var rig = HardwareRigsRegistry.GetHardwareRig();
        (var rigPosition, var rigRotation) = TransformManipulations.DetermineNewRigPositionToMovePositionToTargetPosition(
                currentIRLPose.position, currentIRLPose.rotation,
                targetPose.position, targetPose.rotation,
                rig.transform,
                rig.Headset.transform,
                ignoreYAxisMove: !autofixGroundPosition, keepUpDirection: keepUpDirection
        );
        return new Pose(rigPosition, rigRotation);
    }

    public void MoveRigPositionToMoveCurrentIRLPoseToTargetPose(Pose currentIRLPose, Pose targetPose)
    {
        var rig = HardwareRigsRegistry.GetHardwareRig();
        var rigPose = DetermineRigPositionToMoveCurrentIRLPoseToTargetPose(currentIRLPose, targetPose);

        ConsoleLog($"MoveRigPositionToMoveCurrentIRLPoseToTargetPose: {rig.transform.position}->{rigPose.position} // {rig.transform.rotation.eulerAngles} -> {rigPose.rotation.eulerAngles}");
        rig.transform.rotation = rigPose.rotation;
        rig.transform.position = rigPose.position;
    }

    #endregion

    #region Room anchors
    public void RegisterNetworkIRLRoomAnchor(NetworkIRLRoomAnchor networkAnchor)
    {
        var anchorId = networkAnchor.AnchorId.ToString();

        if (knowNetworkAnchorByAnchorIds.ContainsKey(anchorId) == false) knowNetworkAnchorByAnchorIds[anchorId] = networkAnchor;
        if (knowTagIds.Contains(anchorId) == false) knowTagIds.Add(anchorId);
    }

    public void UnregisterNetworkIRLRoomAnchor(NetworkIRLRoomAnchor anchor)
    {
        var anchorId = anchor.AnchorId.ToString();
        knowNetworkAnchorByAnchorIds.Remove(anchorId);
        knowTagIds.Remove(anchorId);
        var roomId = anchor.RoomId.ToString();
        if (string.IsNullOrEmpty(roomId) == false)
            OnAnchorLeavingRoom(anchor, roomId);
    }

    public void OnNetworkIRLRoomAnchorRoomChange(NetworkIRLRoomAnchor anchor, string previousRoomId)
    {
        var roomId = anchor.RoomId.ToString();
        ConsoleLog($"OnNetworkIRLRoomAnchorRoomChange {anchor.AnchorId} :  {previousRoomId} -> {roomId}");
        if (roomId != previousRoomId)
            OnAnchorLeavingRoom(anchor, previousRoomId);
        OnAnchorJoiningRoom(anchor, roomId);
    }

    public void OnAnchorJoiningRoom(NetworkIRLRoomAnchor anchor, string roomId)
    {
        if (string.IsNullOrEmpty(roomId)) return;

        CreateRoomIfneeded(roomId);

        if (knowRoomByRoomIds[roomId].anchors.Contains(anchor) == false)
        {
            ConsoleLog($"OnAnchorJoiningRoom {anchor.AnchorId} {roomId}");
            knowRoomByRoomIds[roomId].anchors.Add(anchor);
        }
    }

    public void OnAnchorLeavingRoom(NetworkIRLRoomAnchor anchor, string roomId)
    {
        if (string.IsNullOrEmpty(roomId)) return;

        if (knowRoomByRoomIds.ContainsKey(roomId))
        {
            ConsoleLog($"OnAnchorLeavingRoom {anchor.AnchorId} {roomId}");
            knowRoomByRoomIds[roomId].anchors.Remove(anchor);
            RemoveRoomIfEmpty(roomId);
        }
    }
    #endregion

    #region Room members
    public void RegisterNetworkIRLRoomMember(NetworkIRLRoomMember member)
    {
        var roomId = member.RoomId.ToString();
        if (knowMembers.Contains(member)) return;

        if (member.Object.HasStateAuthority)
        {
            localNetworkIRLRoomMember = member;
        }
        knowMembers.Add(member);

        OnMemberJoiningRoom(member, roomId);
    }

    public void UnregisterNetworkIRLRoomMember(NetworkIRLRoomMember member)
    {
        var roomId = member.RoomId.ToString();

        OnMemberLeavingRoom(member, roomId);

        if (knowMembers.Contains(member))
        {
            knowMembers.Remove(member);
        }
    }

    public void OnMemberJoiningRoom(NetworkIRLRoomMember member, string roomId)
    {
        if (string.IsNullOrEmpty(roomId)) return;

        CreateRoomIfneeded(roomId);

        if (knowRoomByRoomIds[roomId].members.Contains(member) == false)
        {
            knowRoomByRoomIds[roomId].members.Add(member);
            ConsoleLog("Add member to room " + roomId);
        }
    }

    public void OnMemberLeavingRoom(NetworkIRLRoomMember member, string roomId)
    {
        if (string.IsNullOrEmpty(roomId)) return;

        if (knowRoomByRoomIds.ContainsKey(roomId))
        {
            ConsoleLog($"OnMemberLeavingRoom {member.Object.StateAuthority} {roomId}");
            knowRoomByRoomIds[roomId].members.Remove(member);
            RemoveRoomIfEmpty(roomId);
        }
    }

    public void OnNetworkIRLRoomMemberRoomChange(NetworkIRLRoomMember member, string previousRoomId)
    {
        var roomId = member.RoomId.ToString();

        if (string.IsNullOrEmpty(previousRoomId) == false && roomId != previousRoomId && knowRoomByRoomIds.ContainsKey(previousRoomId) && knowRoomByRoomIds[previousRoomId].members.Contains(member))
        {
            // Make anchors follow the member room change if this members is not doing a room merge
            if (member.PresenceCause == NetworkIRLRoomMember.RoomPresenceCause.InitializingRoomMerge)
            {
                if (anchorFollowLogic != AnchorFollowLogic.NoFollow)
                {
                    bool canPreviousAnchorFollowMember = IsRoomMergeAllowed(previousRoomId, roomId);

                    if (canPreviousAnchorFollowMember)
                    {
                        switch (anchorFollowLogic)
                        {
                            case AnchorFollowLogic.RoomMerge:
                                MergeRooms(previousRoomId, roomId, member);
                                break;
                        }
                    }
                }

            }

            OnMemberLeavingRoom(member, previousRoomId);

            if (member.PresenceCause == NetworkIRLRoomMember.RoomPresenceCause.ExplicitRoomIdChange && knowRoomByRoomIds.ContainsKey(previousRoomId) && knowRoomByRoomIds[previousRoomId].members.Count == 0)
            {
                // The last user of a room left it: we should destroy remaining anchors and move requester
                ConsoleLog($"The last user of room {previousRoomId} left, removing the room");
                var anchorsToDelete = new List<NetworkIRLRoomAnchor>();
                var moveRequesterToDelete = knowRoomByRoomIds[previousRoomId].moveRequester;
                anchorsToDelete.AddRange(knowRoomByRoomIds[previousRoomId].anchors);
                foreach(var anchor in anchorsToDelete)
                {
                    if (anchor.HasStateAuthority)
                    {
                        anchor.Runner.Despawn(anchor.Object);
                    }
                }
                if (moveRequesterToDelete)
                {
                    moveRequesterToDelete.Runner.Despawn(moveRequesterToDelete.Object);
                }
            }
        }

        if (string.IsNullOrEmpty(roomId) == false && (knowRoomByRoomIds.ContainsKey(roomId) == false || knowRoomByRoomIds[roomId].members.Contains(member) == false))
        {
            ConsoleLog($"User changes room {previousRoomId} to {member.RoomId}");
            OnMemberJoiningRoom(member, roomId);
        }
    }

    public void NetworkIRLRoomMoveRequesterMoveCounterChange(NetworkIRLRoomMoveRequester requester)
    {
        var roomId = requester.RoomId.ToString();

        // This user triggered a room move. 
        if (string.IsNullOrEmpty(roomId) == false && knowRoomByRoomIds.ContainsKey(roomId))
        {
            // We check if we were already aware of this move (by comparing MaxMoveCounter and the member MoveCounter)
            if (knowRoomByRoomIds[roomId].lastAppliedMoveCounter < requester.MoveCounter)
            {
                knowRoomByRoomIds[roomId].lastAppliedMoveCounter = requester.MoveCounter;

                // We check if we are in this room too (it won't be an issue for later joiners receiving the room change notification, as they are not already in a room)
                if (knowRoomByRoomIds[roomId].members.Contains(localNetworkIRLRoomMember))
                {
                    // This move was unknow, we apply it to ourselves and to our anchors
                    MoveRoom(requesterTriggeringRoomMove: requester);
                }
            }
        }
    }

    public int MaxMoveCounterForRoomId(string roomId)
    {
        if (string.IsNullOrEmpty(roomId) == false && knowRoomByRoomIds.ContainsKey(roomId))
        {
            return knowRoomByRoomIds[roomId].lastAppliedMoveCounter;
        }
        return 0;
    }
    #endregion

    #region NetworkIRLRoomMoveRequester
    public void UnregisterNetworkIRLMoveRequester(NetworkIRLRoomMoveRequester requester)
    {
        var roomId = requester.RoomId.ToString();

        OnMoveRequesterLeavingRoom(requester, roomId);

        if (knowMoveRequesters.Contains(requester))
        {
            knowMoveRequesters.Remove(requester);
        }
    }

    public void RegisterNetworkIRLMoveRequester(NetworkIRLRoomMoveRequester requester)
    {
        var roomId = requester.RoomId.ToString();

        OnMoveRequesterJoiningRoom(requester, roomId);

        if (knowMoveRequesters.Contains(requester) == false)
        {
            knowMoveRequesters.Add(requester);
        }
    }

    public void OnNetworkIRLRoomMoveRequesterRoomChange(NetworkIRLRoomMoveRequester requester, string previousRoomId)
    {
        var roomId = requester.RoomId.ToString();
        ConsoleLog($"OnNetworkIRLRoomMoveRequester :  {previousRoomId} -> {roomId}");
        if (roomId != previousRoomId)
            OnMoveRequesterLeavingRoom(requester, previousRoomId);
        OnMoveRequesterJoiningRoom(requester, roomId);
    }

    public virtual void OnMoveRequesterJoiningRoom(NetworkIRLRoomMoveRequester requester, string roomId)
    {
        if (string.IsNullOrEmpty(roomId)) return;

        CreateRoomIfneeded(roomId);

        if (knowRoomByRoomIds[roomId].moveRequester == requester) return;

        if (knowRoomByRoomIds[roomId].moveRequester != null)
        {
            requester.OnRoomAlreadyContainsARequester(existingRequester: knowRoomByRoomIds[roomId].moveRequester);
            return;
        }
        knowRoomByRoomIds[roomId].moveRequester = requester;
        if (knowRoomByRoomIds[roomId].lastAppliedMoveCounter < requester.MoveCounter)
        {
            knowRoomByRoomIds[roomId].lastAppliedMoveCounter = requester.MoveCounter;
        }
        ConsoleLog("Add requester to room " + roomId);
    }

    public void OnMoveRequesterLeavingRoom(NetworkIRLRoomMoveRequester requester, string roomId)
    {
        if (string.IsNullOrEmpty(roomId)) return;

        if (knowRoomByRoomIds.ContainsKey(roomId) && knowRoomByRoomIds[roomId].moveRequester == requester)
        {
            ConsoleLog($"OnMoveRequesterLeavingRoom {roomId}");
            knowRoomByRoomIds[roomId].moveRequester = null;
        }
    }
    #endregion

    #region Room modifications triggered by room members
    bool IsRoomMergeAllowed(string previousRoomId, string roomId)
    {
        bool canPreviousAnchorFollowMember = true;

        // We do not allow predefined anchors to change rooms, so merging/following is not allowed in this case
        foreach (var anchor in knowRoomByRoomIds[previousRoomId].anchors)
        {
            if (IsPredefinedRoomAnchorId(anchor.AnchorId.ToString(), out _))
            {
                var logEvent = $"Previous room {previousRoomId} contains a predefined anchors {anchor.AnchorId}. Anchors from this room won't move to the new one {roomId}";
                LogEvent(logEvent);
                canPreviousAnchorFollowMember = false;
                break;
            }
            else if (anchor.IsChangingRoomForbidden)
            {
                var logEvent = $"Previous room {previousRoomId} contains an anchor than cannot be moved {anchor.AnchorId}. Anchors from this room won't move to the new one {roomId}";
                LogEvent(logEvent);
                canPreviousAnchorFollowMember = false;
                break;
            }
        }
        return canPreviousAnchorFollowMember;
    }

    public void MergeRooms(string roomIdToDelete, string targetRoomId, NetworkIRLRoomMember memberTriggeringMerge)
    {
        if (string.IsNullOrEmpty(roomIdToDelete) == false && knowRoomByRoomIds.ContainsKey(roomIdToDelete))
        {
            var logEvent = $"Merge room {roomIdToDelete} into {targetRoomId}";
            var previousRoom = knowRoomByRoomIds[roomIdToDelete];
            var anchors = previousRoom.anchors.ToArray();
            var members = previousRoom.members.ToArray();
            var moveRequester = previousRoom.moveRequester;
            foreach (var anchor in anchors)
            {
                // If we have authority on the anchor, and it has not moved to another room (we could receive several notifications of room change not in order), we merge it to the new room
                if (anchor.Object.HasStateAuthority && anchor.RoomId.ToString() == roomIdToDelete)
                {
                    anchor.ChangeRoomId(targetRoomId);

                    LogEvent($"Preparing to move anchor {anchor.AnchorId} as player {memberTriggeringMerge.Object.StateAuthority} has merged rooms");
                    MoveTransformToFollowSameRoomReferenceElementMove(memberTriggeringMerge, anchor.transform);
                    LogEvent($"Moving anchor {anchor.AnchorId} as player {memberTriggeringMerge.Object.StateAuthority} has merged rooms");
                }
            }

            if (moveRequester != null)
            {
                if (moveRequester.Object.HasStateAuthority && moveRequester.RoomId.ToString() == roomIdToDelete)
                {
                    // Move 
                    MoveTransformToFollowSameRoomReferenceElementMove(memberTriggeringMerge, moveRequester.transform);
                    // Prevent unsollicted move
                    moveRequester.DidMoveWithoutRequest();
                    // Change id
                    moveRequester.ChangeRoomId(targetRoomId);
                }

            }

            foreach (var member in members)
            {
                // If we have authority on the member, and it has not moved to another room (we could receive several notifications of room change not in order), we merge it to the new room
                if (member.Object.HasStateAuthority && member.RoomId.ToString() == roomIdToDelete)
                    member.FollowingRoomMerge(targetRoomId);
            }
            LogEvent(logEvent);
        }
    }

    public void MoveRoom(NetworkIRLRoomMoveRequester requesterTriggeringRoomMove)
    {
        var roomId = requesterTriggeringRoomMove.RoomId.ToString();

        // 1 - Relocate user

        // Anchors in the room will move. If we follow an anchor, some one will move it, and our local user will follow
        // Otherwise, we need to move ourselves
        if (localNetworkIRLRoomMember.RoomAnchorToFollow == null)
        {
            var rig = HardwareRigsRegistry.GetHardwareRig();
            MoveTransformToFollowSameRoomReferenceElementMove(requesterTriggeringRoomMove, rig.transform);
            LogEvent($"Moving local user {localNetworkIRLRoomMember.Object.StateAuthority} (with no anchors) as player {requesterTriggeringRoomMove.Object.StateAuthority} has moved room (new local member position: {rig.transform.position})");
        }

        // 2 - Relocate all anchors controlled by user
        var anchors = knowRoomByRoomIds[roomId].anchors.ToArray();
        foreach (var anchor in anchors)
        {
            // If we have authority on the anchor, and it has not moved to another room (we could receive several notifications of room change not in order), we merge it to the new room
            if (anchor.Object.HasStateAuthority)
            {
                LogEvent($"Preparing to move anchor {anchor.AnchorId} as player {requesterTriggeringRoomMove.Object.StateAuthority} has moved room");
                MoveTransformToFollowSameRoomReferenceElementMove(requesterTriggeringRoomMove, anchor.transform);
                LogEvent($"Moving anchor {anchor.AnchorId} as player {requesterTriggeringRoomMove.Object.StateAuthority} has moved room");
            }
        }
    }

    public void MoveTransformToFollowSameRoomReferenceElementMove(IRLRoomMovingReferenceElement referenceElement, Transform transformToMove)
    {
        var memberPositionBeforeMerge = referenceElement.PositionBeforeMoveToPropagate;
        var memberRotationBeforeMerge = referenceElement.RotationBeforeMoveToPropagate;
        var memberPositionAfterMerge = referenceElement.PositionAfterMoveToPropagate;
        var memberRotationAfterMerge = referenceElement.RotationAfterMoveToPropagate;

        (var offsetToMemberPosition, var offsetToMemberRotation) = TransformManipulations.UnscaledOffset(memberPositionBeforeMerge, memberRotationBeforeMerge, transformToMove);
        (var newAnchorPosition, var newAnchorRotation) = TransformManipulations.ApplyUnscaledOffset(memberPositionAfterMerge, memberRotationAfterMerge, offsetToMemberPosition, offsetToMemberRotation);

        transformToMove.position = newAnchorPosition;
        transformToMove.rotation = newAnchorRotation;
    }
    #endregion

    #region Room
    void CreateRoomIfneeded(string roomId)
    {
        if (knowRoomByRoomIds.ContainsKey(roomId)) return;

        ConsoleLog("Creating room " + roomId);
        knowRoomByRoomIds[roomId] = new IRLRoom();
        knowRoomByRoomIds[roomId].roomId = roomId;
        knowRooms.Add(knowRoomByRoomIds[roomId]);

        NotifyRoomCreation(roomId);
    }

    void RemoveRoomIfEmpty(string roomId)
    {
        if (knowRoomByRoomIds.ContainsKey(roomId) == false) return;

        if (knowRoomByRoomIds[roomId].members.Count == 0 && knowRoomByRoomIds[roomId].anchors.Count == 0)
        {
            ConsoleLog("Deleting room " + roomId);
            if (knowRoomByRoomIds[roomId].moveRequester)
            {
                knowRoomByRoomIds[roomId].moveRequester.OnRoomEmpty();
            }
            knowRooms.Remove(knowRoomByRoomIds[roomId]);
            knowRoomByRoomIds.Remove(roomId);

            NotifyRoomDelete(roomId);
        }
    }
    #endregion

    #region IIRLRoomManagerListener handling
    public void RegisterListener(IIRLRoomManagerListener listener)
    {
        if (listeners.Contains(listener) == false)
        {
            listeners.Add(listener);
        }
    }

    public void UnregisterListener(IIRLRoomManagerListener listener)
    {
        if (listeners.Contains(listener))
        {
            listeners.Remove(listener);
        }
    }

    public void NotifyRoomCreation(string roomId)
    {
        foreach (var listener in listeners) listener.OnRoomCreate(roomId);
    }

    public void NotifyRoomDelete(string roomId)
    {
        foreach (var listener in listeners) listener.OnRoomDelete(roomId);
    }
    #endregion
}
