using Fusion;
using Fusion.XR.Shared.Core;
using System;
using UnityEngine;
using Fusion.XR.Shared.Tools;

/**
 * Handle IRL room presence of an user. Should be place on a network rig
 * 
 * Requires an IRLRoomManager in the scene
 */
public class NetworkIRLRoomMember : NetworkBehaviour, IColocalizationRoomProvider, IRLRoomMovingReferenceElement
{
    public const int MAX_ROOMID_LENGTH = 32;
    [Networked, OnChangedRender(nameof(OnRoomIdChange))]
    public NetworkString<_32> RoomId { get; set; }

    NetworkIRLRoomAnchor _roomAnchorToFollow = null;
    public NetworkIRLRoomAnchor RoomAnchorToFollow
    {
        get
        {
            return _roomAnchorToFollow;
        }

        set
        {
            roomAnchorToFollowRigOffset = null;
            _roomAnchorToFollow = value;
        }
    }

    public enum RoomPresenceCause
    {
        ExplicitRoomIdChange,   // When the room id is simply changed
        InitializingRoomMerge,  // When the room id is changed due to 2 rooms being merged (during a colocation), and we are doing it
        FollowingRoomMerge,      // When the room id is changed due to 2 rooms being merged (during a colocation), and we are following someone else triggering it
    }

    [Networked]
    public RoomPresenceCause PresenceCause { get; set; }

    [Networked]
    public Vector3 PositionBeforeMergingRoom { get; set; }

    [Networked]
    public Quaternion RotationBeforeMergingRoom { get; set; }
    [Networked]
    public Vector3 PositionAfterMergingRoom { get; set; }

    [Networked]
    public Quaternion RotationAfterMergingRoom { get; set; }

    Pose? roomAnchorToFollowRigOffset = null;

    [Tooltip("When an headset has been removed, its position in space when coming back might be erroneous. Check this to reset the user room (to prevent bad localization).  Should be set to true in most cases")]
    public bool leaveIRLRoomOnHDMReturn = true;

    #region IRLRoomMovingReferenceElement
    public Vector3 PositionBeforeMoveToPropagate => PositionBeforeMergingRoom;
    public Quaternion RotationBeforeMoveToPropagate => RotationBeforeMergingRoom;
    public Vector3 PositionAfterMoveToPropagate => PositionAfterMergingRoom;
    public Quaternion RotationAfterMoveToPropagate => RotationAfterMergingRoom;
    #endregion

    #region IColocalizationRoomProvider
    public string IRLRoomId => RoomId.ToString();
    #endregion

    IRLRoomManager roomManager;

    private void Awake()
    {
        roomManager = FindAnyObjectByType<IRLRoomManager>();
    }

    private void Start()
    {
#if OCULUS_SDK_AVAILABLE
        OVRManager.HMDMounted += OnOVRManagerHMDMounted;
        OVRManager.HMDUnmounted += OnOVRManagerHMDUnmounted;
#endif
    }

    public override void Spawned()
    {
        base.Spawned();
        if (Object.HasStateAuthority)
        {
            RoomId = GenerateGuid();
            PresenceCause = RoomPresenceCause.ExplicitRoomIdChange;
        }
        roomManager.RegisterNetworkIRLRoomMember(this);
        roomManager.OnNetworkIRLRoomMemberRoomChange(this, "");
    }

    public float minDistanceChangeToFollowAnchor = 0.01f;
    public float minAngleChangeToFollowAnchor = 1f;

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if(RoomAnchorToFollow != null)
        {
            var rig = HardwareRigsRegistry.GetHardwareRig();
            if(rig != null)
            {
                if (RoomAnchorToFollow.ShouldNotBeFollowed)
                {
                    roomAnchorToFollowRigOffset = null;
                }

                if (roomAnchorToFollowRigOffset is Pose rigOffset)
                {
                    // Check distance, correct rig position only if error too large
                    var rigRotation = RoomAnchorToFollow.transform.rotation * rigOffset.rotation;
                    var rigPosition = RoomAnchorToFollow.transform.TransformPoint(rigOffset.position);
                    if (Vector3.Distance(rigPosition, rig.transform.position) > minDistanceChangeToFollowAnchor || Quaternion.Angle(rigRotation, rig.transform.rotation) > minAngleChangeToFollowAnchor)
                    {
                        roomManager.LogEvent($"[NetworkIRLRoomMember] Reference anchor {RoomAnchorToFollow.AnchorId} " +
                            $"moved to {RoomAnchorToFollow.transform.position}/{RoomAnchorToFollow.transform.rotation}. " +
                            $"Changing rig position to follow it (ofset: {rigOffset.position}/{rigOffset.rotation})");
                        rig.transform.rotation = rigRotation;
                        rig.transform.position = rigPosition;
                    }
                }
                else
                {
                    //Store Rig offset to anchor
                    var positionOffset = RoomAnchorToFollow.transform.InverseTransformPoint(rig.transform.position);
                    var rotationOffset = Quaternion.Inverse(RoomAnchorToFollow.transform.rotation) * rig.transform.rotation;
                    roomAnchorToFollowRigOffset = new Pose(positionOffset, rotationOffset);
                }
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
        roomManager?.UnregisterNetworkIRLRoomMember(this);
    }

    public string GenerateGuid()
    {
        // 32 characters
        var guid = Guid.NewGuid().ToString("N");
        return guid;
    }

    void OnRoomIdChange(NetworkBehaviourBuffer previous)
    {
        string previousRoomId = GetPropertyReader<NetworkString<_32>>(nameof(RoomId)).Read(previous).ToString();

        roomManager?.OnNetworkIRLRoomMemberRoomChange(this, previousRoomId);
    }

    #region Room change actions
    public void InitializingRoomMerge(string newRoomId, Vector3 positionBeforeMergingRoom, Quaternion rotationBeforeMergingRoom, Vector3 positionAfterMergingRoom, Quaternion rotationAfterMergingRoom)
    {
        PositionBeforeMergingRoom = positionBeforeMergingRoom;
        RotationBeforeMergingRoom = rotationBeforeMergingRoom;
        PositionAfterMergingRoom = positionAfterMergingRoom;
        RotationAfterMergingRoom = rotationAfterMergingRoom;
        // Will trigger on change, that may read merge position info, hence the need to do it before changing room
        ChangeRoomId(newRoomId, changeCause: NetworkIRLRoomMember.RoomPresenceCause.InitializingRoomMerge);
    }

    public void FollowingRoomMerge(string targetRoomId)
    {
        ChangeRoomId(targetRoomId, changeCause: NetworkIRLRoomMember.RoomPresenceCause.FollowingRoomMerge);

    }
    #endregion

    public void ChangeRoomId(NetworkString<_32> roomId, RoomPresenceCause changeCause = RoomPresenceCause.ExplicitRoomIdChange)
    {
        if (Object.HasStateAuthority == false)
        {
            Debug.LogError("Cannot set RoomId on anchors not owned");
            return;
        }

        var previousRoomId = RoomId;
        RoomId = roomId;

        PresenceCause = changeCause;

        // We immediatly notify the manager, without waiting for the change event
        // This way, it can move anchors only related to this user to the same room, to avoid, when all anchors are visible at the same time, going back and forth between a previous room (that should in fact be merged with the new one) and the new one
        roomManager?.OnNetworkIRLRoomMemberRoomChange(this, previousRoomId.ToString());
    }


#if OCULUS_SDK_AVAILABLE
    bool roomResetRequired = false;


    private void OnOVRManagerHMDMounted()
    {
        if (roomResetRequired && leaveIRLRoomOnHDMReturn)
        {
            if (Object.HasStateAuthority)
            {
                var roomId = RoomId.ToString();
                Debug.Log("OnOVRManagerHMDMounted");
                roomManager.LogEvent("The headset has been removed. reseting the user room (to prevent bad localization due to the headset recomputing its position)");
                RoomId = GenerateGuid();

                if (string.IsNullOrEmpty(roomId) == false)
                {
                    PresenceCause = RoomPresenceCause.ExplicitRoomIdChange;
                }
            }
        }
    }

    private void OnOVRManagerHMDUnmounted()
    {
        Debug.Log("OnOVRManagerHMDUnmounted: we will reset the user room on user return, as the MR positioning might be lost, hence invalidating the colocalization");
        roomResetRequired = true;
    }
#endif
}
